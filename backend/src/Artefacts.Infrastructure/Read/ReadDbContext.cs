using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Read;

/// <summary>
/// Read-side context over PostgreSQL views (no tracking, keyless rows). Views are created by
/// <see cref="Persistence.CollectionDbContext"/> migrations from the SQL in <c>Read/Views</c>.
/// Later views (loans, status) add navigations here for the query engine to traverse.
/// </summary>
public sealed class ReadDbContext(DbContextOptions<ReadDbContext> options) : DbContext(options)
{
    public IQueryable<ArtefactRow> Artefacts => Set<ArtefactRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ArtefactRow>(b =>
        {
            b.HasNoKey();
            b.ToView("v_artefact");
            b.Property(r => r.AcquisitionPriceAmount).HasPrecision(19, 4);
        });
    }
}
