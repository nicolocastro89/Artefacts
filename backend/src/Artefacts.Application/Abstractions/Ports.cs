using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Media;
using Artefacts.Domain.Regions;

namespace Artefacts.Application.Abstractions;

public interface IArtefactRepository
{
    Task<Artefact?> GetAsync(ArtefactId id, CancellationToken ct);
    void Add(Artefact artefact);
    Task<bool> InventoryNumberExistsAsync(string inventoryNumber, ArtefactId? excluding, CancellationToken ct);
    Task<bool> AnyProvenanceReferencesAsync(RegionId regionId, CancellationToken ct);
}

public interface IRegionRepository
{
    Task<Region?> GetAsync(RegionId id, CancellationToken ct);
    Task<IReadOnlyDictionary<RegionId, Region>> GetManyAsync(IEnumerable<RegionId> ids, CancellationToken ct);
    Task<Region?> FindByNormalizedNameAsync(string normalizedName, CancellationToken ct);
    Task<IReadOnlyList<Region>> SearchAsync(string? text, CancellationToken ct);
    Task<IReadOnlyDictionary<RegionId, RegionId?>> GetParentMapAsync(CancellationToken ct);
    Task<bool> HasChildrenAsync(RegionId id, CancellationToken ct);
    void Add(Region region);
    void Remove(Region region);
}

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetAsync(MediaAssetId id, CancellationToken ct);
    Task<IReadOnlyDictionary<MediaAssetId, MediaAsset>> GetManyAsync(IEnumerable<MediaAssetId> ids, CancellationToken ct);
    void Add(MediaAsset asset);
    void Remove(MediaAsset asset);
}

/// <summary>Commits all pending changes atomically. Unique-key violations surface as <see cref="DomainValidationException"/>.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Object storage for original media files.</summary>
public interface IMediaStorage
{
    Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Produces signed URLs for the sized renditions of a stored image.</summary>
public interface IImageUrlBuilder
{
    ImageUrls Build(string storageKey);
}

/// <summary>Read-side artefact listing (search, sort, paging) over the read model.</summary>
public interface IArtefactListQuery
{
    Task<PagedResult<ArtefactListItem>> ListAsync(ArtefactListRequest request, CancellationToken ct);
}

/// <summary>The field catalog describing the read model.</summary>
public interface IFieldCatalog
{
    FieldCatalogDto Get();
}
