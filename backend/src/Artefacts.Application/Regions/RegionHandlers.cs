using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Regions;

namespace Artefacts.Application.Regions;

public sealed class RegionHandler(
    IRegionRepository regions,
    IArtefactRepository artefacts,
    IContentLanguages contentLanguages,
    ILanguagePreferences preferences,
    IUnitOfWork unitOfWork)
{
    /// <summary>Regions whose name in any language contains <paramref name="text"/>, ordered by the resolved name.</summary>
    public async Task<IReadOnlyList<RegionDto>> SearchAsync(string? text, CancellationToken ct)
    {
        var found = await regions.SearchAsync(text, ct);
        var parents = await regions.GetManyAsync(found.Where(r => r.ParentId is not null).Select(r => r.ParentId!.Value).Distinct(), ct);
        return found.Select(r => ToDto(r, parents))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<RegionDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    /// <summary>
    /// Adds a region, or returns the existing one when one of the given names already names a region in the same
    /// language.
    /// </summary>
    public async Task<AddRegionResult> AddAsync(AddRegionRequest request, CancellationToken ct)
    {
        var v = new ValidationCollector();
        var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
        v.ThrowIfAny();

        foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
        {
            var existing = await regions.FindByNameAsync(language, LocalizedName.Normalize(name), ct);
            if (existing is not null) return new AddRegionResult(await ToDtoAsync(existing, ct), Created: false);
        }

        var region = Region.Create(names);
        if (request.ParentId is { } parentId)
        {
            await EnsureExistsAsync(parentId, ct);
            region.SetParent(new RegionId(parentId), _ => null); // a brand-new region has no descendants
        }

        regions.Add(region);
        await unitOfWork.SaveChangesAsync(ct);
        return new AddRegionResult(await ToDtoAsync(region, ct), Created: true);
    }

    /// <summary>Replaces the region's names; each must be unique within its language.</summary>
    public async Task<RegionDto> SetNamesAsync(Guid id, SetNamesRequest request, CancellationToken ct)
    {
        var region = await LoadAsync(id, ct);
        var v = new ValidationCollector();
        var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
        foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
        {
            var clash = await regions.FindByNameAsync(language, LocalizedName.Normalize(name), ct);
            if (clash is not null && clash.Id != region.Id)
                v.Add($"names.{language}", $"Another region is already named \"{name!.Trim()}\" in this language.");
        }
        v.Try("", () => region.SetNames(names));
        v.ThrowIfAny();

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(region, ct);
    }

    public async Task<RegionDto> SetParentAsync(Guid id, SetRegionParentRequest request, CancellationToken ct)
    {
        var region = await LoadAsync(id, ct);
        if (request.ParentId is { } parentId) await EnsureExistsAsync(parentId, ct);

        var parentMap = await regions.GetParentMapAsync(ct);
        region.SetParent(request.ParentId is { } p ? new RegionId(p) : null,
            r => parentMap.TryGetValue(r, out var parent) ? parent : null);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(region, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var region = await LoadAsync(id, ct);
        var name = NameOf(region);
        if (await artefacts.AnyReferencesAsync(region.Id, ct))
            throw new ConflictException("region_in_use",
                $"\"{name}\" is used by artefacts (provenance or place of production) and cannot be deleted.");
        if (await regions.HasChildrenAsync(region.Id, ct))
            throw new ConflictException("region_in_use",
                $"\"{name}\" has child regions and cannot be deleted.");

        regions.Remove(region);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Region> LoadAsync(Guid id, CancellationToken ct) =>
        await regions.GetAsync(new RegionId(id), ct) ?? throw new NotFoundException("Region", id);

    private async Task EnsureExistsAsync(Guid parentId, CancellationToken ct)
    {
        if (await regions.GetAsync(new RegionId(parentId), ct) is null)
            throw new DomainValidationException("parentId", "The parent region does not exist.");
    }

    private string NameOf(Region r) => r.NameIn(preferences.Current)?.Text ?? "";

    private async Task<RegionDto> ToDtoAsync(Region r, CancellationToken ct)
    {
        var parents = r.ParentId is { } p
            ? await regions.GetManyAsync([p], ct)
            : new Dictionary<RegionId, Region>();
        return ToDto(r, parents);
    }

    private RegionDto ToDto(Region r, IReadOnlyDictionary<RegionId, Region> parents)
    {
        var name = r.NameIn(preferences.Current);
        return new RegionDto(
            r.Id.Value,
            name?.Text ?? "",
            name?.Language ?? "",
            r.Names.ToDictionary(n => n.Language, n => n.Name),
            r.ParentId?.Value,
            r.ParentId is { } p && parents.TryGetValue(p, out var parent) ? NameOf(parent) : null);
    }
}
