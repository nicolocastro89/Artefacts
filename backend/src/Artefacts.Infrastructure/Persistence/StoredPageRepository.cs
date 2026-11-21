using Artefacts.Application.Abstractions;
using Artefacts.Domain.Pages;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Persistence;

internal sealed class StoredPageRepository(CollectionDbContext db) : IStoredPageRepository
{
    public Task<StoredPage?> GetAsync(string id, CancellationToken ct) => db.StoredPages.SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<StoredPage?> GetBySlugAsync(string slug, CancellationToken ct) => db.StoredPages.SingleOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<IReadOnlyList<StoredPage>> ListAsync(bool navigationOnly, CancellationToken ct) =>
        await db.StoredPages.Where(p => !navigationOnly || p.ShowInNavigation)
            .OrderByDescending(p => p.IsSystem).ThenBy(p => p.NormalizedName).ToListAsync(ct);

    public void Add(StoredPage page) => db.StoredPages.Add(page);

    public void Remove(StoredPage page) => db.StoredPages.Remove(page);
}
