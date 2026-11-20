using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Imports;

/// <summary>Named mappings, with their place decisions and date resolutions, reused across workbooks.</summary>
public sealed class ImportTemplateHandler(
    IImportTemplateRepository templates,
    IImportRepository imports,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ImportHandler importHandler)
{
    public async Task<IReadOnlyList<ImportTemplateDto>> ListAsync(CancellationToken ct) =>
        (await templates.ListAsync(ct)).Select(ToDto).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    public async Task<ImportTemplateDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await LoadAsync(id, ct));

    public async Task<ImportTemplateDto> CreateAsync(SaveImportTemplateRequest request, CancellationToken ct)
    {
        var (mapping, decisions) = await ContentAsync(request, ct);
        var template = ImportTemplate.Create(request.Name, mapping, decisions, clock.GetUtcNow());
        templates.Add(template);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<ImportTemplateDto> UpdateAsync(Guid id, SaveImportTemplateRequest request, CancellationToken ct)
    {
        var template = await LoadAsync(id, ct);
        var (mapping, decisions) = await ContentAsync(request, ct, template);
        template.Update(request.Name, mapping, decisions, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        templates.Remove(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>The mapping and decisions to save: copied from a job, given in the request, or kept from the template.</summary>
    private async Task<(string Mapping, string Decisions)> ContentAsync(SaveImportTemplateRequest request, CancellationToken ct, ImportTemplate? existing = null)
    {
        if (request.FromJobId is { } jobId)
        {
            var job = await imports.GetJobAsync(jobId, ct) ?? throw new NotFoundException("Import", jobId);
            if (job.Mapping is null) throw new DomainValidationException("fromJobId", "The import has no mapping yet.");
            return (job.Mapping, ImportJson.Write(ImportHandler.Decisions(job)));
        }

        if (request.Mapping is { } mapping)
        {
            await importHandler.ValidateMappingAsync(mapping, new Dictionary<int, string>(), ct);
            return (ImportJson.Write(mapping), ImportJson.Write(request.Decisions ?? ImportDecisions.Empty));
        }

        if (existing is not null)
            return (existing.Mapping, request.Decisions is { } d ? ImportJson.Write(d) : existing.Decisions);
        throw new DomainValidationException("mapping", "Give a mapping, or the import to take it from.");
    }

    private async Task<ImportTemplate> LoadAsync(Guid id, CancellationToken ct) =>
        await templates.GetAsync(id, ct) ?? throw new NotFoundException("Import template", id);

    private static ImportTemplateDto ToDto(ImportTemplate t) =>
        new(t.Id, t.Name, ImportJson.Read<ImportMapping>(t.Mapping), ImportHandler.ReadDecisions(t.Decisions), t.UpdatedAt);
}
