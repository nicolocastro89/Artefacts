using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Imports;

/// <summary>Loads the detectors a mapping can use: the built-in ones and the user's stored ones.</summary>
public static class DetectorCatalogue
{
    /// <summary>The built-in registry with every stored detector whose definition still holds.</summary>
    public static async Task<DetectorRegistry> LoadAsync(IImportDetectorRepository detectors, CancellationToken ct) =>
        DetectorRegistry.BuiltIn.With((await detectors.ListAsync(ct)).Select(UserDetector.From).OfType<ILineDetector>());
}

/// <summary>
/// The detectors line rules refer to: the built-in ones (read-only, written in code) and the user ones people define
/// (a pattern with named captures, or keywords). A detector in use by a template or an unfinished import is not deleted.
/// </summary>
public sealed class ImportDetectorHandler(
    IImportDetectorRepository detectors,
    IImportTemplateRepository templates,
    IImportRepository imports,
    IImportLookupRepository lookupTables,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ImportDetectorDto>> ListAsync(CancellationToken ct)
    {
        var system = DetectorRegistry.BuiltIn.Detectors.Select(d => new ImportDetectorDto(d.Id, d.Label, d.Example, Definition: d.Definition));
        var user = (await detectors.ListAsync(ct)).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToDto);
        return [.. system, .. user];
    }

    public async Task<ImportDetectorDto> GetAsync(string id, CancellationToken ct) => ToDto(await LoadAsync(id, ct));

    public async Task<ImportDetectorDto> CreateAsync(SaveImportDetectorRequest request, CancellationToken ct)
    {
        var detector = ImportDetector.Create(request.Name, Definition(request), clock.GetUtcNow());
        detectors.Add(detector);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(detector);
    }

    public async Task<ImportDetectorDto> UpdateAsync(string id, SaveImportDetectorRequest request, CancellationToken ct)
    {
        var detector = await LoadAsync(id, ct);
        detector.Update(request.Name, Definition(request), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(detector);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var detector = await LoadAsync(id, ct);
        var identifier = detector.Identifier;
        var usedBy = (await templates.ListAsync(ct)).Where(t => t.Mapping.Contains(identifier, StringComparison.Ordinal)).Select(t => $"template \"{t.Name}\"")
            .Concat((await imports.ListJobsAsync(ct))
                .Where(j => j.State is not (ImportJobState.Finished or ImportJobState.Discarded) && j.Mapping?.Contains(identifier, StringComparison.Ordinal) == true)
                .Select(j => $"import of {j.FileName}"))
            .ToList();
        if (usedBy.Count > 0)
            throw new ConflictException("detector_in_use", $"\"{detector.Name}\" is used by {string.Join(", ", usedBy)}. Change those line rules first.");

        detectors.Remove(detector);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A new detector with the definition of a stored one, or of a built-in one that has a definition. The other built-in
    /// detectors only exist as code, so they cannot be copied.
    /// </summary>
    public async Task<ImportDetectorDto> DuplicateAsync(string id, DuplicateImportDetectorRequest request, CancellationToken ct)
    {
        var copy = DetectorRegistry.BuiltIn.Detectors.FirstOrDefault(d => d.Id == id) is { } builtIn
            ? ImportDetector.Create(request.Name, ImportJson.Write(builtIn.Definition
                ?? throw new ConflictException("system_detector", $"\"{builtIn.Label}\" is written in code and cannot be copied.")), clock.GetUtcNow())
            : (await LoadAsync(id, ct)).Duplicate(request.Name, clock.GetUtcNow());
        detectors.Add(copy);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(copy);
    }

    public const int MaxTryLines = 50;

    /// <summary>Runs a definition, saved or not, against sample lines, with the lookup tables imports use. Nothing is stored.</summary>
    public async Task<TryImportDetectorResult> TryAsync(TryImportDetectorRequest request, CancellationToken ct)
    {
        var problems = request.Definition is null
            ? new Dictionary<string, string> { ["definition"] = "Is required." }
            : request.Definition.Problems().ToDictionary(p => p.Key, p => p.Value);
        var lines = request.Lines ?? [];
        if (lines.Count > MaxTryLines) problems["lines"] = $"At most {MaxTryLines} lines.";
        if (problems.Count > 0) throw new DomainValidationException(problems.ToDictionary(p => p.Key, p => new[] { p.Value }));

        var detector = new UserDetector("try", "Try", request.Definition!);
        var context = new DetectionContext(Detectors.DefaultLocationPatterns, await LookupTableCatalogue.LoadAsync(lookupTables, ct));
        return new TryImportDetectorResult(lines
            .Select(line => detector.Detect(line ?? "", context) is { } d
                ? new TryImportDetectorLine(line ?? "", true, d.Text, d.Parsed, d.Remainder, d.ItemNumber, d.Guessed)
                : new TryImportDetectorLine(line ?? "", false, null, null, null, null, false))
            .ToList());
    }

    /// <summary>The definition as stored text, or the validation problems that stop it being stored.</summary>
    private static string Definition(SaveImportDetectorRequest request)
    {
        var problems = request.Definition is null
            ? new Dictionary<string, string> { ["definition"] = "Is required." }
            : request.Definition.Problems();
        if (problems.Count > 0) throw new DomainValidationException(problems.ToDictionary(p => p.Key, p => new[] { p.Value }));
        return ImportJson.Write(request.Definition!);
    }

    private async Task<ImportDetector> LoadAsync(string id, CancellationToken ct)
    {
        if (DetectorRegistry.BuiltIn.Contains(id))
            throw new ConflictException("system_detector", "Built-in detectors cannot be changed, deleted or copied.");
        return (ImportDetector.IdOf(id) is { } guid ? await detectors.GetAsync(guid, ct) : null) ?? throw new NotFoundException("Import detector", id);
    }

    private static ImportDetectorDto ToDto(ImportDetector d)
    {
        var definition = ImportJson.Read<DetectorDefinition>(d.Definition);
        return new ImportDetectorDto(d.Identifier, d.Name, definition.Example, false, definition, d.UpdatedAt);
    }
}
