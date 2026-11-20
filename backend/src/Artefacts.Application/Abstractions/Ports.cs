using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Media;
using Artefacts.Domain.Regions;

namespace Artefacts.Application.Abstractions;

public interface IArtefactRepository
{
    Task<Artefact?> GetAsync(ArtefactId id, CancellationToken ct);
    void Add(Artefact artefact);
    Task<bool> InventoryNumberExistsAsync(string inventoryNumber, ArtefactId? excluding, CancellationToken ct);

    /// <summary>True when a provenance entry or a place of production references the region.</summary>
    Task<bool> AnyReferencesAsync(RegionId regionId, CancellationToken ct);

    Task<bool> AnyReferencesAsync(ObjectTypeId objectTypeId, CancellationToken ct);

    Task<IReadOnlyDictionary<ObjectTypeId, int>> CountByObjectTypeAsync(CancellationToken ct);
}

public interface IObjectTypeRepository
{
    Task<ObjectType?> GetAsync(ObjectTypeId id, CancellationToken ct);

    /// <summary>The object type whose name in <paramref name="language"/> normalises to <paramref name="normalizedName"/>.</summary>
    Task<ObjectType?> FindByNameAsync(string language, string normalizedName, CancellationToken ct);

    /// <summary>Object types with a name in any language containing <paramref name="text"/> (all when empty).</summary>
    Task<IReadOnlyList<ObjectType>> SearchAsync(string? text, CancellationToken ct);
    void Add(ObjectType objectType);
    void Remove(ObjectType objectType);
}

public interface IRegionRepository
{
    Task<Region?> GetAsync(RegionId id, CancellationToken ct);
    Task<IReadOnlyDictionary<RegionId, Region>> GetManyAsync(IEnumerable<RegionId> ids, CancellationToken ct);

    /// <summary>The region whose name in <paramref name="language"/> normalises to <paramref name="normalizedName"/>.</summary>
    Task<Region?> FindByNameAsync(string language, string normalizedName, CancellationToken ct);

    /// <summary>Regions with a name in any language containing <paramref name="text"/> (all when empty).</summary>
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

public interface ILanguageRepository
{
    Task<IReadOnlyList<Language>> ListAsync(CancellationToken ct);
    Task<Language?> GetAsync(string code, CancellationToken ct);

    /// <summary>True when any translated value (name or description) is recorded in the language.</summary>
    Task<bool> InUseAsync(string code, CancellationToken ct);

    void Add(Language language);
    void Remove(Language language);
}

/// <summary>The content languages in fallback order, cached; invalidated whenever the list changes.</summary>
public interface IContentLanguages
{
    Task<IReadOnlyList<LanguageDto>> ListAsync(CancellationToken ct);
    void Invalidate();
}

/// <summary>
/// The preferred languages of the current request: its requested languages, then every remaining content language
/// in fallback order. Translated values resolve to the first of these that has a value.
/// </summary>
public interface ILanguagePreferences
{
    IReadOnlyList<string> Current { get; }
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

/// <summary>Read-side artefact listing (search, sort, grouping, paging) over the read model.</summary>
public interface IArtefactListQuery
{
    Task<ArtefactListPage> ListAsync(ArtefactListRequest request, CancellationToken ct);
}

/// <summary>The field catalog describing the read model.</summary>
public interface IFieldCatalog
{
    FieldCatalogDto Get();
}
