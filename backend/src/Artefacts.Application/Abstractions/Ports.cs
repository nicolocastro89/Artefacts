using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Imports;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Lists;
using Artefacts.Domain.Media;
using Artefacts.Domain.Parties;
using Artefacts.Domain.Regions;

namespace Artefacts.Application.Abstractions;

public interface IArtefactRepository
{
    Task<Artefact?> GetAsync(ArtefactId id, CancellationToken ct);
    void Add(Artefact artefact);
    Task<bool> InventoryNumberExistsAsync(string inventoryNumber, ArtefactId? excluding, CancellationToken ct);

    /// <summary>Which of <paramref name="inventoryNumbers"/> are already used by an artefact.</summary>
    Task<IReadOnlySet<string>> ExistingInventoryNumbersAsync(IReadOnlyCollection<string> inventoryNumbers, CancellationToken ct);

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

public interface IReferenceEntryRepository
{
    Task<ReferenceEntry?> GetAsync(ListKind kind, ReferenceEntryId id, CancellationToken ct);
    Task<IReadOnlyDictionary<ReferenceEntryId, ReferenceEntry>> GetManyAsync(IEnumerable<ReferenceEntryId> ids, CancellationToken ct);

    /// <summary>
    /// The entry of the list whose name normalises to <paramref name="normalizedName"/>: in <paramref name="language"/>
    /// (any language when null) for translated lists, its single name otherwise.
    /// </summary>
    Task<ReferenceEntry?> FindByNameAsync(ListKind kind, string? language, string normalizedName, CancellationToken ct);

    /// <summary>Entries of the list with a name in any language containing <paramref name="text"/> (all when empty).</summary>
    Task<IReadOnlyList<ReferenceEntry>> SearchAsync(ListKind kind, string? text, CancellationToken ct);

    /// <summary>The number of artefacts that use each entry of the list.</summary>
    Task<IReadOnlyDictionary<ReferenceEntryId, int>> CountUsesAsync(ListKind kind, CancellationToken ct);
    void Add(ReferenceEntry entry);
    void Remove(ReferenceEntry entry);
}

/// <summary>How many artefacts use a party as acquisition source and as holder.</summary>
public readonly record struct PartyUses(int Source, int Holder);

public interface IPartyRepository
{
    Task<Party?> GetAsync(PartyId id, CancellationToken ct);
    Task<IReadOnlyDictionary<PartyId, Party>> GetManyAsync(IEnumerable<PartyId> ids, CancellationToken ct);
    Task<Party?> FindByNameAsync(string normalizedName, CancellationToken ct);

    /// <summary>Parties whose name contains <paramref name="text"/>, optionally of one kind, ordered by name.</summary>
    Task<IReadOnlyList<Party>> SearchAsync(string? text, PartyKind? kind, CancellationToken ct);
    Task<IReadOnlyDictionary<PartyId, PartyUses>> CountUsesAsync(CancellationToken ct);
    void Add(Party party);
    void Remove(Party party);
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

    /// <summary>Copies an object within the store (server-side where the store supports it).</summary>
    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct);

    /// <summary>Deletes every object whose key starts with <paramref name="prefix"/>.</summary>
    Task DeletePrefixAsync(string prefix, CancellationToken ct);
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

public interface IImportRepository
{
    Task<ImportJob?> GetJobAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ImportJob>> ListJobsAsync(CancellationToken ct);
    void AddJob(ImportJob job);

    /// <summary>The job's staged rows in sheet order (tracked).</summary>
    Task<IReadOnlyList<ImportRow>> GetRowsAsync(Guid jobId, CancellationToken ct);
    Task<ImportRow?> GetRowAsync(Guid rowId, CancellationToken ct);
    void AddRows(IEnumerable<ImportRow> rows);
    Task DeleteRowsAsync(Guid jobId, CancellationToken ct);

    /// <summary>
    /// Claims the oldest job that is waiting to be read or imported and not held by another worker
    /// (<c>FOR UPDATE SKIP LOCKED</c>), holding it until <paramref name="leaseUntil"/>.
    /// </summary>
    Task<Guid?> ClaimAsync(DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken ct);

    Task RenewLeaseAsync(Guid jobId, DateTimeOffset leaseUntil, CancellationToken ct);

    /// <summary>Jobs never committed and untouched since <paramref name="before"/>.</summary>
    Task<IReadOnlyList<ImportJob>> AbandonedAsync(DateTimeOffset before, CancellationToken ct);
}

public interface IImportTemplateRepository
{
    Task<ImportTemplate?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ImportTemplate>> ListAsync(CancellationToken ct);
    void Add(ImportTemplate template);
    void Remove(ImportTemplate template);
}

/// <summary>Wakes the import worker when there is new work, instead of waiting for its next poll.</summary>
public interface IImportSignal
{
    void Notify();
    Task WaitAsync(TimeSpan timeout, CancellationToken ct);
}

/// <summary>Writes the import report as a spreadsheet.</summary>
public interface IImportReportWriter
{
    void Write(ImportReportDto report, Stream output);
}
