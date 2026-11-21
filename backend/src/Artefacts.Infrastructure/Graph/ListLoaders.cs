using Artefacts.Infrastructure.Read;
using GreenDonut;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// Reads the rows of one of an artefact's lists (provenance, images, exhibitions, descriptions) for every artefact
/// of a request at once, so a page of artefacts costs one query per list, not one per artefact.
/// </summary>
public abstract class ArtefactListDataLoader<TRow>(
    IDbContextFactory<ReadDbContext> dbFactory,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options) : GroupedDataLoader<Guid, TRow>(batchScheduler, options)
{
    /// <summary>The rows of the given artefacts, in the order the list is shown.</summary>
    protected abstract IQueryable<TRow> Rows(ReadDbContext db, Guid[] artefactIds);

    protected abstract Guid ArtefactOf(TRow row);

    protected override async Task<ILookup<Guid, TRow>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await Rows(db, [.. keys]).ToListAsync(cancellationToken);
        return rows.ToLookup(ArtefactOf);
    }
}

public sealed class ProvenanceDataLoader(IDbContextFactory<ReadDbContext> dbFactory, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : ArtefactListDataLoader<ProvenanceRow>(dbFactory, batchScheduler, options)
{
    protected override IQueryable<ProvenanceRow> Rows(ReadDbContext db, Guid[] artefactIds) =>
        db.Provenance.Where(r => artefactIds.Contains(r.ArtefactId)).OrderBy(r => r.ArtefactId).ThenBy(r => r.Position);

    protected override Guid ArtefactOf(ProvenanceRow row) => row.ArtefactId;
}

/// <summary>Images in the order they were attached, the first attached being shown first.</summary>
public sealed class ImagesDataLoader(IDbContextFactory<ReadDbContext> dbFactory, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : ArtefactListDataLoader<ImageRow>(dbFactory, batchScheduler, options)
{
    protected override IQueryable<ImageRow> Rows(ReadDbContext db, Guid[] artefactIds) =>
        db.Images.Where(r => artefactIds.Contains(r.ArtefactId)).OrderBy(r => r.ArtefactId).ThenBy(r => r.AttachedAt).ThenBy(r => r.MediaAssetId);

    protected override Guid ArtefactOf(ImageRow row) => row.ArtefactId;
}

public sealed class ExhibitionsDataLoader(IDbContextFactory<ReadDbContext> dbFactory, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : ArtefactListDataLoader<ExhibitionRow>(dbFactory, batchScheduler, options)
{
    protected override IQueryable<ExhibitionRow> Rows(ReadDbContext db, Guid[] artefactIds) =>
        db.Exhibitions.Where(r => artefactIds.Contains(r.ArtefactId)).OrderBy(r => r.ArtefactId).ThenBy(r => r.Position);

    protected override Guid ArtefactOf(ExhibitionRow row) => row.ArtefactId;
}

public sealed class DescriptionsDataLoader(IDbContextFactory<ReadDbContext> dbFactory, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : ArtefactListDataLoader<DescriptionRow>(dbFactory, batchScheduler, options)
{
    protected override IQueryable<DescriptionRow> Rows(ReadDbContext db, Guid[] artefactIds) =>
        db.Descriptions.Where(r => artefactIds.Contains(r.ArtefactId)).OrderBy(r => r.ArtefactId).ThenBy(r => r.Language);

    protected override Guid ArtefactOf(DescriptionRow row) => row.ArtefactId;
}

public sealed class CatalogueEntriesDataLoader(IDbContextFactory<ReadDbContext> dbFactory, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : ArtefactListDataLoader<CatalogueEntryRow>(dbFactory, batchScheduler, options)
{
    protected override IQueryable<CatalogueEntryRow> Rows(ReadDbContext db, Guid[] artefactIds) =>
        db.CatalogueEntries.Where(r => artefactIds.Contains(r.ArtefactId)).OrderBy(r => r.ArtefactId).ThenBy(r => r.Language);

    protected override Guid ArtefactOf(CatalogueEntryRow row) => row.ArtefactId;
}
