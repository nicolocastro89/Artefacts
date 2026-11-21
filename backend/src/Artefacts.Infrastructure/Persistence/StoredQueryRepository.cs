using Artefacts.Application.Abstractions;
using Artefacts.Domain.Queries;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Persistence;

internal sealed class StoredQueryRepository(CollectionDbContext db) : IStoredQueryRepository
{
    public Task<StoredQuery?> GetAsync(string id, CancellationToken ct) => db.StoredQueries.SingleOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<StoredQuery>> ListAsync(CancellationToken ct) =>
        await db.StoredQueries.OrderByDescending(q => q.IsSystem).ThenBy(q => q.NormalizedName).ToListAsync(ct);

    public void Add(StoredQuery query) => db.StoredQueries.Add(query);

    public void Remove(StoredQuery query) => db.StoredQueries.Remove(query);
}
