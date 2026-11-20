using Artefacts.Application.Abstractions;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Media;
using Artefacts.Domain.Regions;
using Microsoft.EntityFrameworkCore;
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

    public Task<bool> AnyProvenanceReferencesAsync(RegionId regionId, CancellationToken ct) =>
        db.Artefacts.AnyAsync(a => a.Provenance.Any(p => p.RegionId == regionId), ct);
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

    public Task<Region?> FindByNormalizedNameAsync(string normalizedName, CancellationToken ct) =>
        db.Regions.SingleOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);

    public async Task<IReadOnlyList<Region>> SearchAsync(string? text, CancellationToken ct)
    {
        var query = db.Regions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{Like.Escape(text.Trim().ToLowerInvariant())}%";
            query = query.Where(r => EF.Functions.Like(r.NormalizedName, pattern, Like.EscapeChar));
        }

        return await query.OrderBy(r => r.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<RegionId, RegionId?>> GetParentMapAsync(CancellationToken ct) =>
        await db.Regions.ToDictionaryAsync(r => r.Id, r => r.ParentId, ct);

    public Task<bool> HasChildrenAsync(RegionId id, CancellationToken ct) =>
        db.Regions.AnyAsync(r => r.ParentId == id, ct);

    public void Add(Region region) => db.Regions.Add(region);
    public void Remove(Region region) => db.Regions.Remove(region);
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
                    new DomainValidationException("name", "A region with this name already exists."),
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
