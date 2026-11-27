using Artefacts.Application.Abstractions;
using Artefacts.Domain.Imports;
using Artefacts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Imports;

internal sealed class ImportRepository(CollectionDbContext db) : IImportRepository
{
    public Task<ImportJob?> GetJobAsync(Guid id, CancellationToken ct) => db.ImportJobs.SingleOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IReadOnlyList<ImportJob>> ListJobsAsync(CancellationToken ct) =>
        await db.ImportJobs.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);

    public void AddJob(ImportJob job) => db.ImportJobs.Add(job);

    public async Task<IReadOnlyList<ImportRow>> GetRowsAsync(Guid jobId, CancellationToken ct) =>
        await db.ImportRows.Where(r => r.JobId == jobId).OrderBy(r => r.SheetRow).ToListAsync(ct);

    public Task<ImportRow?> GetRowAsync(Guid rowId, CancellationToken ct) => db.ImportRows.SingleOrDefaultAsync(r => r.Id == rowId, ct);

    public void AddRows(IEnumerable<ImportRow> rows) => db.ImportRows.AddRange(rows);

    public Task DeleteRowsAsync(Guid jobId, CancellationToken ct) =>
        db.ImportRows.Where(r => r.JobId == jobId).ExecuteDeleteAsync(ct);

    public async Task<Guid?> ClaimAsync(DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken ct)
    {
        var reading = nameof(ImportJobState.Reading);
        var importing = nameof(ImportJobState.Importing);
        var claimed = await db.Database.SqlQuery<Guid>($"""
            UPDATE import_jobs SET lease_until = {leaseUntil}
            WHERE id = (
                SELECT id FROM import_jobs
                WHERE state IN ({reading}, {importing}) AND (lease_until IS NULL OR lease_until < {now})
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING id AS "Value"
            """).ToListAsync(ct);
        return claimed.Count == 0 ? null : claimed[0];
    }

    public Task RenewLeaseAsync(Guid jobId, DateTimeOffset leaseUntil, CancellationToken ct) =>
        db.ImportJobs.Where(j => j.Id == jobId && j.LeaseUntil != null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseUntil, leaseUntil), ct);

    public async Task<IReadOnlyList<ImportJob>> AbandonedAsync(DateTimeOffset before, CancellationToken ct) =>
        await db.ImportJobs
            .Where(j => (j.State == ImportJobState.AwaitingMapping || j.State == ImportJobState.ReadyForReview ||
                         j.State == ImportJobState.Failed) && j.UpdatedAt < before)
            .ToListAsync(ct);
}

internal sealed class ImportTemplateRepository(CollectionDbContext db) : IImportTemplateRepository
{
    public Task<ImportTemplate?> GetAsync(Guid id, CancellationToken ct) => db.ImportTemplates.SingleOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ImportTemplate>> ListAsync(CancellationToken ct) => await db.ImportTemplates.ToListAsync(ct);

    public void Add(ImportTemplate template) => db.ImportTemplates.Add(template);

    public void Remove(ImportTemplate template) => db.ImportTemplates.Remove(template);
}

/// <summary>Wakes the worker as soon as a job is uploaded or committed.</summary>
internal sealed class ImportSignal : IImportSignal
{
    private readonly SemaphoreSlim _signal = new(0);

    public void Notify()
    {
        // Several notifications before the worker looks are worth one wake-up.
        if (_signal.CurrentCount == 0) _signal.Release();
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct) => await _signal.WaitAsync(timeout, ct);
}

internal sealed class ImportDetectorRepository(CollectionDbContext db) : IImportDetectorRepository
{
    public Task<ImportDetector?> GetAsync(Guid id, CancellationToken ct) => db.ImportDetectors.SingleOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<ImportDetector>> ListAsync(CancellationToken ct) => await db.ImportDetectors.ToListAsync(ct);

    public void Add(ImportDetector detector) => db.ImportDetectors.Add(detector);

    public void Remove(ImportDetector detector) => db.ImportDetectors.Remove(detector);
}

internal sealed class ImportLookupRepository(CollectionDbContext db) : IImportLookupRepository
{
    public Task<ImportLookupTable?> GetAsync(string tableId, CancellationToken ct) => db.ImportLookupTables.SingleOrDefaultAsync(t => t.TableId == tableId, ct);

    public async Task<IReadOnlyList<ImportLookupTable>> ListAsync(CancellationToken ct) => await db.ImportLookupTables.ToListAsync(ct);

    public void Add(ImportLookupTable table) => db.ImportLookupTables.Add(table);

    public void Remove(ImportLookupTable table) => db.ImportLookupTables.Remove(table);
}
