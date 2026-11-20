using Artefacts.Application.Abstractions;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Media;
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
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CollectionDbContext.ObjectTypeNameIndex } =>
                    new DomainValidationException("names", "Another object type already has this name in the same language."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.ConstraintName?.StartsWith(CollectionDbContext.LanguageForeignKeyPrefix) == true =>
                    new ConflictException("language_in_use", "The language is used by names or descriptions."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.TableName == "object_types" ||
                                                                          pg.ConstraintName?.Contains("object_types") == true =>
                    new ConflictException("object_type_in_use", "The object type is in use and cannot be deleted."),
                { SqlState: PostgresErrorCodes.ForeignKeyViolation } when pg.TableName == "regions" ||
                                                                          pg.ConstraintName?.Contains("regions") == true =>
                    new ConflictException("region_in_use", "The region is in use and cannot be deleted."),
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
