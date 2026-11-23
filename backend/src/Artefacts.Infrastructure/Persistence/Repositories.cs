using Artefacts.Application.Abstractions;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Lists;
using Artefacts.Domain.Measures;
using Artefacts.Domain.Media;
using Artefacts.Domain.Parties;
using Artefacts.Domain.Regions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Artefacts.Infrastructure.Persistence;

internal sealed class ArtefactRepository(CollectionDbContext db) : IArtefactRepository
{
    public Task<Artefact?> GetAsync(ArtefactId id, CancellationToken ct) =>
        db.Artefacts.SingleOrDefaultAsync(a => a.Id == id, ct);

    public void Add(Artefact artefact) => db.Artefacts.Add(artefact);

    public Task<bool> InventoryNumberExistsAsync(string inventoryNumber, ArtefactId? excluding, CancellationToken ct) =>
        excluding is { } ex
            ? db.Artefacts.AnyAsync(a => a.InventoryNumber == inventoryNumber && a.Id != ex, ct)
            : db.Artefacts.AnyAsync(a => a.InventoryNumber == inventoryNumber, ct);

    public async Task<IReadOnlySet<string>> ExistingInventoryNumbersAsync(IReadOnlyCollection<string> inventoryNumbers, CancellationToken ct)
    {
        if (inventoryNumbers.Count == 0) return new HashSet<string>();
        var list = inventoryNumbers.Distinct().ToList();
        return (await db.Artefacts.Where(a => list.Contains(a.InventoryNumber)).Select(a => a.InventoryNumber).ToListAsync(ct)).ToHashSet();
    }

    public Task<bool> AnyReferencesAsync(RegionId regionId, CancellationToken ct) =>
        db.Artefacts.AnyAsync(a =>
            a.Provenance.Any(p => p.RegionId == regionId) ||
            (a.PlaceOfProduction != null && a.PlaceOfProduction.RegionId == regionId), ct);

    public Task<bool> AnyReferencesAsync(ObjectTypeId objectTypeId, CancellationToken ct) =>
        db.Artefacts.AnyAsync(a => a.ObjectTypeId == objectTypeId, ct);

    public async Task<IReadOnlyDictionary<ObjectTypeId, int>> CountByObjectTypeAsync(CancellationToken ct) =>
        await db.Artefacts.Where(a => a.ObjectTypeId != null)
            .GroupBy(a => a.ObjectTypeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlyDictionary<UnitOfMeasureId, int>> CountByUnitAsync(CancellationToken ct)
    {
        var pairs = await db.Artefacts
            .Select(a => new
            {
                a.Id,
                W = a.Width != null ? (UnitOfMeasureId?)a.Width.UnitId : null,
                H = a.Height != null ? (UnitOfMeasureId?)a.Height.UnitId : null,
                L = a.Length != null ? (UnitOfMeasureId?)a.Length.UnitId : null,
                D = a.Diameter != null ? (UnitOfMeasureId?)a.Diameter.UnitId : null,
            })
            .Where(x => x.W != null || x.H != null || x.L != null || x.D != null)
            .ToListAsync(ct);
        return pairs
            .SelectMany(x => new[] { x.W, x.H, x.L, x.D }.Where(u => u is not null).Select(u => (x.Id, Unit: u!.Value)).Distinct())
            .GroupBy(x => x.Unit)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}

internal sealed class UnitOfMeasureRepository(CollectionDbContext db) : IUnitOfMeasureRepository
{
    public Task<UnitOfMeasure?> GetAsync(UnitOfMeasureId id, CancellationToken ct) =>
        db.UnitsOfMeasure.SingleOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<UnitOfMeasure>> ListAsync(CancellationToken ct) =>
        await db.UnitsOfMeasure.OrderBy(u => u.Factor).ThenBy(u => u.NormalizedAbbreviation).ToListAsync(ct);

    public Task<UnitOfMeasure?> FindByAbbreviationAsync(string normalizedAbbreviation, CancellationToken ct) =>
        db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.NormalizedAbbreviation == normalizedAbbreviation, ct);

    public Task<UnitOfMeasure?> FindByNameAsync(string language, string normalizedName, CancellationToken ct) =>
        db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Names.Any(n => n.Language == language && n.NormalizedName == normalizedName), ct);

    public void Add(UnitOfMeasure unit) => db.UnitsOfMeasure.Add(unit);
    public void Remove(UnitOfMeasure unit) => db.UnitsOfMeasure.Remove(unit);
}

internal sealed class ObjectTypeRepository(CollectionDbContext db) : IObjectTypeRepository
{
    public Task<ObjectType?> GetAsync(ObjectTypeId id, CancellationToken ct) =>
        db.ObjectTypes.SingleOrDefaultAsync(t => t.Id == id, ct);

    public Task<ObjectType?> FindByNameAsync(string language, string normalizedName, CancellationToken ct) =>
        db.ObjectTypes.FirstOrDefaultAsync(t => t.Names.Any(n => n.Language == language && n.NormalizedName == normalizedName), ct);

    public async Task<IReadOnlyList<ObjectType>> SearchAsync(string? text, CancellationToken ct)
    {
        var query = db.ObjectTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{Like.Escape(LocalizedName.Normalize(text))}%";
            query = query.Where(t => t.Names.Any(n => EF.Functions.Like(n.NormalizedName, pattern, Like.EscapeChar)));
        }

        return await query.ToListAsync(ct);
    }

    public void Add(ObjectType objectType) => db.ObjectTypes.Add(objectType);
    public void Remove(ObjectType objectType) => db.ObjectTypes.Remove(objectType);
}

internal sealed class ReferenceEntryRepository(CollectionDbContext db) : IReferenceEntryRepository
{
    public Task<ReferenceEntry?> GetAsync(ListKind kind, ReferenceEntryId id, CancellationToken ct) =>
        db.ReferenceEntries.SingleOrDefaultAsync(e => e.Id == id && e.Kind == kind, ct);

    public async Task<IReadOnlyDictionary<ReferenceEntryId, ReferenceEntry>> GetManyAsync(IEnumerable<ReferenceEntryId> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<ReferenceEntryId, ReferenceEntry>();
        return await db.ReferenceEntries.Where(e => list.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
    }

    public Task<ReferenceEntry?> FindByNameAsync(ListKind kind, string? language, string normalizedName, CancellationToken ct) =>
        kind.IsTranslated()
            ? db.ReferenceEntries.FirstOrDefaultAsync(e => e.Kind == kind &&
                e.Names.Any(n => (language == null || n.Language == language) && n.NormalizedName == normalizedName), ct)
            : db.ReferenceEntries.FirstOrDefaultAsync(e => e.Kind == kind && e.NormalizedName == normalizedName, ct);

    public async Task<IReadOnlyList<ReferenceEntry>> SearchAsync(ListKind kind, string? text, CancellationToken ct)
    {
        var query = db.ReferenceEntries.Where(e => e.Kind == kind);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{Like.Escape(LocalizedName.Normalize(text))}%";
            query = query.Where(e =>
                EF.Functions.Like(e.NormalizedName!, pattern, Like.EscapeChar) ||
                e.Names.Any(n => EF.Functions.Like(n.NormalizedName, pattern, Like.EscapeChar)));
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<ReferenceEntryId, int>> CountUsesAsync(ListKind kind, CancellationToken ct)
    {
        var artefacts = db.Artefacts.AsQueryable();
        var ids = kind switch
        {
            ListKind.Dynasty => artefacts.Where(a => a.DynastyId != null).Select(a => a.DynastyId!.Value),
            ListKind.CollectionGroup => artefacts.Where(a => a.CollectionGroupId != null).Select(a => a.CollectionGroupId!.Value),
            _ => artefacts.Where(a => a.StorageLocationId != null).Select(a => a.StorageLocationId!.Value),
        };
        return await ids.GroupBy(id => id).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public void Add(ReferenceEntry entry) => db.ReferenceEntries.Add(entry);
    public void Remove(ReferenceEntry entry) => db.ReferenceEntries.Remove(entry);
}

internal sealed class PartyRepository(CollectionDbContext db) : IPartyRepository
{
    public Task<Party?> GetAsync(PartyId id, CancellationToken ct) => db.Parties.SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyDictionary<PartyId, Party>> GetManyAsync(IEnumerable<PartyId> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<PartyId, Party>();
        return await db.Parties.Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }

    public Task<Party?> FindByNameAsync(string normalizedName, CancellationToken ct) =>
        db.Parties.FirstOrDefaultAsync(p => p.NormalizedName == normalizedName, ct);

    public async Task<IReadOnlyList<Party>> SearchAsync(string? text, PartyKind? kind, CancellationToken ct)
    {
        var query = db.Parties.AsQueryable();
        if (kind is { } k) query = query.Where(p => p.Kind == k);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{Like.Escape(LocalizedName.Normalize(text))}%";
            query = query.Where(p => EF.Functions.Like(p.NormalizedName, pattern, Like.EscapeChar));
        }

        return await query.OrderBy(p => p.NormalizedName).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<PartyId, PartyUses>> CountUsesAsync(CancellationToken ct)
    {
        var sources = await db.Artefacts.Where(a => a.Acquisition != null && a.Acquisition.SourceId != null)
            .GroupBy(a => a.Acquisition!.SourceId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var holders = await db.Artefacts.Where(a => a.HolderId != null)
            .GroupBy(a => a.HolderId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var result = new Dictionary<PartyId, PartyUses>();
        foreach (var s in sources) result[s.Key] = new PartyUses(s.Count, 0);
        foreach (var h in holders)
            result[h.Key] = result.TryGetValue(h.Key, out var u) ? u with { Holder = h.Count } : new PartyUses(0, h.Count);
        return result;
    }

    public void Add(Party party) => db.Parties.Add(party);
    public void Remove(Party party) => db.Parties.Remove(party);
}

internal sealed class RegionRepository(CollectionDbContext db) : IRegionRepository
{
    public Task<Region?> GetAsync(RegionId id, CancellationToken ct) =>
        db.Regions.SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyDictionary<RegionId, Region>> GetManyAsync(IEnumerable<RegionId> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        if (list.Count == 0) return new Dictionary<RegionId, Region>();
        return await db.Regions.Where(r => list.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
    }

    public Task<Region?> FindByNameAsync(string language, string normalizedName, CancellationToken ct) =>
        db.Regions.FirstOrDefaultAsync(r => r.Names.Any(n => n.Language == language && n.NormalizedName == normalizedName), ct);

    public async Task<IReadOnlyList<Region>> SearchAsync(string? text, CancellationToken ct)
    {
        var query = db.Regions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{Like.Escape(LocalizedName.Normalize(text))}%";
            query = query.Where(r => r.Names.Any(n => EF.Functions.Like(n.NormalizedName, pattern, Like.EscapeChar)));
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<RegionId, RegionId?>> GetParentMapAsync(CancellationToken ct) =>
        await db.Regions.ToDictionaryAsync(r => r.Id, r => r.ParentId, ct);

    public Task<bool> HasChildrenAsync(RegionId id, CancellationToken ct) =>
        db.Regions.AnyAsync(r => r.ParentId == id, ct);

    public void Add(Region region) => db.Regions.Add(region);
    public void Remove(Region region) => db.Regions.Remove(region);
}

internal sealed class LanguageRepository(CollectionDbContext db) : ILanguageRepository
{
    public async Task<IReadOnlyList<Language>> ListAsync(CancellationToken ct) =>
        await db.Languages.OrderBy(l => l.Position).ThenBy(l => l.Code).ToListAsync(ct);

    public Task<Language?> GetAsync(string code, CancellationToken ct) =>
        db.Languages.SingleOrDefaultAsync(l => l.Code == code, ct);

    public async Task<bool> InUseAsync(string code, CancellationToken ct) =>
        await db.Regions.AnyAsync(r => r.Names.Any(n => n.Language == code), ct) ||
        await db.ObjectTypes.AnyAsync(t => t.Names.Any(n => n.Language == code), ct) ||
        await db.UnitsOfMeasure.AnyAsync(u => u.Names.Any(n => n.Language == code), ct) ||
        await db.ReferenceEntries.AnyAsync(e => e.Names.Any(n => n.Language == code), ct) ||
        await db.Artefacts.AnyAsync(a => a.Translations.Any(t => t.Language == code), ct);

    public void Add(Language language) => db.Languages.Add(language);
    public void Remove(Language language) => db.Languages.Remove(language);
}

/// <summary>The content languages in fallback order, read once and kept until a change invalidates them.</summary>
internal sealed class ContentLanguages(IServiceScopeFactory scopes) : IContentLanguages
{
    private volatile IReadOnlyList<Application.Contracts.LanguageDto>? _cached;

    public async Task<IReadOnlyList<Application.Contracts.LanguageDto>> ListAsync(CancellationToken ct)
    {
        if (_cached is { } cached) return cached;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        var list = await db.Languages.AsNoTracking().OrderBy(l => l.Position).ThenBy(l => l.Code)
            .Select(l => new Application.Contracts.LanguageDto(l.Code, l.Name, l.Position)).ToListAsync(ct);
        _cached = list;
        return list;
    }

    public void Invalidate() => _cached = null;
}

internal sealed class MediaAssetRepository(CollectionDbContext db) : IMediaAssetRepository
{
    public Task<MediaAsset?> GetAsync(MediaAssetId id, CancellationToken ct) =>
        db.MediaAssets.SingleOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyDictionary<MediaAssetId, MediaAsset>> GetManyAsync(IEnumerable<MediaAssetId> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        if (list.Count == 0) return new Dictionary<MediaAssetId, MediaAsset>();
        return await db.MediaAssets.Where(m => list.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
    }

    public void Add(MediaAsset asset) => db.MediaAssets.Add(asset);
    public void Remove(MediaAsset asset) => db.MediaAssets.Remove(asset);
}

/// <summary>Commits changes and translates database constraint violations into domain errors.</summary>
internal sealed class UnitOfWork(CollectionDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException pg)
        {
            throw pg switch
            {
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.InventoryNumberIndex } =>
                    new DomainValidationException("inventoryNumber", "This inventory number is already used by another artefact."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.RegionNameIndex } =>
                    new DomainValidationException("names", "Another region already has this name in the same language."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.ImportTemplateNameIndex } =>
                    new DomainValidationException("name", "Another import template already has this name."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.ObjectTypeNameIndex } =>
                    new DomainValidationException("names", "Another object type already has this name in the same language."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.ReferenceEntryNameIndex } =>
                    new DomainValidationException("names", "Another entry of this list already has this name in the same language."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.ReferenceEntrySingleNameIndex } =>
                    new DomainValidationException("name", "Another entry of this list already has this name."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.UnitNameIndex } =>
                    new DomainValidationException("names", "Another unit already has this name in the same language."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.UnitAbbreviationIndex } =>
                    new DomainValidationException("abbreviation", "Another unit already has this abbreviation."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.ConstraintName?.StartsWith("fk_artefacts_units_of_measure_") == true =>
                    new ConflictException("unit_in_use", "The unit is used by measurements and cannot be deleted."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.PartyNameIndex } =>
                    new DomainValidationException("name", "Another party already has this name."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.StoredQueryNameIndex } =>
                    new DomainValidationException("name", "Another stored query already has this name."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.StoredPageNameIndex } =>
                    new DomainValidationException("name", "Another stored page already has this name."),
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.StoredPageSlugIndex } =>
                    new DomainValidationException("slug", "Another stored page already has this slug."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.ConstraintName?.StartsWith(CollectionDbContext.LanguageForeignKeyPrefix) == true =>
                    new ConflictException("language_in_use", "The language is used by names or descriptions."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.TableName == "object_types" ||
                                                                          pg.ConstraintName?.Contains("object_types") == true =>
                    new ConflictException("object_type_in_use", "The object type is in use and cannot be deleted."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.TableName == "regions" ||
                                                                          pg.ConstraintName?.Contains("regions") == true =>
                    new ConflictException("region_in_use", "The region is in use and cannot be deleted."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.TableName == "reference_entries" ||
                                                                          pg.ConstraintName?.Contains("_reference_entries_") == true =>
                    new ConflictException("entry_in_use", "The list entry is in use and cannot be deleted."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.TableName == "parties" ||
                                                                          pg.ConstraintName?.Contains("_parties_") == true =>
                    new ConflictException("party_in_use", "The party is in use and cannot be deleted."),
                _ => e,
            };
        }
    }
}

internal static class Like
{
    public const string EscapeChar = "\\";

    public static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
