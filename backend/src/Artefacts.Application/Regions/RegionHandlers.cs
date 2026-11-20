using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Regions;

namespace Artefacts.Application.Regions;

public sealed class RegionHandler(IRegionRepository regions, IArtefactRepository artefacts, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<RegionDto>> SearchAsync(string? text, CancellationToken ct)
    {
        var found = await regions.SearchAsync(text, ct);
        var parents = await regions.GetManyAsync(found.Where(r => r.ParentId is not null).Select(r => r.ParentId!.Value).Distinct(), ct);
        return found.Select(r => ToDto(r, parents)).ToList();
    }

    public async Task<RegionDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    /// <summary>Adds a region, or returns the existing one when the normalised name already exists.</summary>
    public async Task<AddRegionResult> AddAsync(AddRegionRequest request, CancellationToken ct)
    {
        var existing = await regions.FindByNormalizedNameAsync(Region.Normalize(request.Name), ct);
        if (existing is not null)
            return new AddRegionResult(await ToDtoAsync(existing, ct), Created: false);

        var region = Region.Create(request.Name);
        if (request.ParentId is { } parentId)
        {
            await EnsureExistsAsync(parentId, ct);
            region.SetParent(new RegionId(parentId), _ => null); // a brand-new region has no descendants
        }

        regions.Add(region);
        await unitOfWork.SaveChangesAsync(ct);
        return new AddRegionResult(await ToDtoAsync(region, ct), Created: true);
    }

    public async Task<RegionDto> RenameAsync(Guid id, RenameRegionRequest request, CancellationToken ct)
    {
        var region = await LoadAsync(id, ct);
        var clash = await regions.FindByNormalizedNameAsync(Region.Normalize(request.Name), ct);
        if (clash is not null && clash.Id != region.Id)
            throw new DomainValidationException("name", $"A region named \"{clash.Name}\" already exists.");

        region.Rename(request.Name);
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
        if (await artefacts.AnyProvenanceReferencesAsync(region.Id, ct))
            throw new ConflictException("region_in_use",
                $"\"{region.Name}\" is used in artefact provenance and cannot be deleted.");
        if (await regions.HasChildrenAsync(region.Id, ct))
            throw new ConflictException("region_in_use",
                $"\"{region.Name}\" has child regions and cannot be deleted.");

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

    private async Task<RegionDto> ToDtoAsync(Region r, CancellationToken ct)
    {
        var parents = r.ParentId is { } p
            ? await regions.GetManyAsync([p], ct)
            : new Dictionary<RegionId, Region>();
        return ToDto(r, parents);
    }

    private static RegionDto ToDto(Region r, IReadOnlyDictionary<RegionId, Region> parents) =>
        new(r.Id.Value, r.Name, r.ParentId?.Value,
            r.ParentId is { } p && parents.TryGetValue(p, out var parent) ? parent.Name : null);
}
