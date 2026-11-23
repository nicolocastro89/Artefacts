using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Measures;

namespace Artefacts.Application.Units;

public sealed class UnitOfMeasureHandler(
    IUnitOfMeasureRepository units,
    IArtefactRepository artefacts,
    IContentLanguages contentLanguages,
    ILanguagePreferences preferences,
    IUnitOfWork unitOfWork)
{
    /// <summary>Every unit with its usage, ordered by factor (smallest unit first).</summary>
    public async Task<IReadOnlyList<UnitOfMeasureDto>> ListAsync(CancellationToken ct)
    {
        var counts = await artefacts.CountByUnitAsync(ct);
        return (await units.ListAsync(ct)).Select(u => ToDto(u, counts)).ToList();
    }

    public async Task<UnitOfMeasureDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    public async Task<UnitOfMeasureDto> AddAsync(UnitOfMeasureRequest request, CancellationToken ct)
    {
        var (names, v) = await CheckAsync(request, null, ct);
        var unit = v.Try("", () => UnitOfMeasure.Create(names, request.Abbreviation, request.Factor ?? 0m));
        v.ThrowIfAny();

        units.Add(unit!);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(unit!, new Dictionary<UnitOfMeasureId, int>());
    }

    /// <summary>
    /// Replaces names, abbreviation and factor. A new factor changes the value in metres of every measurement in the
    /// unit; the measured numbers are kept.
    /// </summary>
    public async Task<UnitOfMeasureDto> UpdateAsync(Guid id, UnitOfMeasureRequest request, CancellationToken ct)
    {
        var unit = await LoadAsync(id, ct);
        var (names, v) = await CheckAsync(request, unit.Id, ct);
        v.Try("", () => unit.Update(names, request.Abbreviation, request.Factor ?? 0m));
        v.ThrowIfAny();

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(unit, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var unit = await LoadAsync(id, ct);
        var count = (await artefacts.CountByUnitAsync(ct)).GetValueOrDefault(unit.Id);
        if (count > 0)
            throw new ConflictException("unit_in_use",
                $"\"{unit.Abbreviation}\" is used by the measurements of {count} artefact{(count == 1 ? "" : "s")} and cannot be deleted.");

        units.Remove(unit);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Checks languages and uniqueness of names and abbreviation; <paramref name="self"/> is the unit being updated.</summary>
    private async Task<(Dictionary<string, string?> Names, ValidationCollector V)> CheckAsync(
        UnitOfMeasureRequest request, UnitOfMeasureId? self, CancellationToken ct)
    {
        var v = new ValidationCollector();
        var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
        foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
        {
            var clash = await units.FindByNameAsync(language, LocalizedName.Normalize(name), ct);
            if (clash is not null && clash.Id != self)
                v.Add($"names.{language}", $"Another unit is already named \"{name!.Trim()}\" in this language.");
        }

        if (!string.IsNullOrWhiteSpace(request.Abbreviation))
        {
            var clash = await units.FindByAbbreviationAsync(UnitOfMeasure.NormalizeAbbreviation(request.Abbreviation), ct);
            if (clash is not null && clash.Id != self)
                v.Add("abbreviation", $"Another unit already has the abbreviation \"{clash.Abbreviation}\".");
        }

        if (request.Factor is null) v.Add("factor", "A factor is required.");
        return (names, v);
    }

    private async Task<UnitOfMeasure> LoadAsync(Guid id, CancellationToken ct) =>
        await units.GetAsync(new UnitOfMeasureId(id), ct) ?? throw new NotFoundException("Unit of measure", id);

    private async Task<UnitOfMeasureDto> ToDtoAsync(UnitOfMeasure u, CancellationToken ct) =>
        ToDto(u, await artefacts.CountByUnitAsync(ct));

    private UnitOfMeasureDto ToDto(UnitOfMeasure u, IReadOnlyDictionary<UnitOfMeasureId, int> counts)
    {
        var name = u.NameIn(preferences.Current);
        return new UnitOfMeasureDto(
            u.Id.Value,
            name?.Text ?? "",
            name?.Language ?? "",
            u.Names.ToDictionary(n => n.Language, n => n.Name),
            u.Abbreviation,
            u.Factor,
            counts.GetValueOrDefault(u.Id));
    }
}
