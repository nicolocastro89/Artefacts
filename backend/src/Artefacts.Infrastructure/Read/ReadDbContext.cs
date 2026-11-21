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
    public IQueryable<ProvenanceRow> Provenance => Set<ProvenanceRow>();
    public IQueryable<ImageRow> Images => Set<ImageRow>();
    public IQueryable<ExhibitionRow> Exhibitions => Set<ExhibitionRow>();
    public IQueryable<DescriptionRow> Descriptions => Set<DescriptionRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDbFunction(typeof(LocalizedSql).GetMethod(nameof(LocalizedSql.RegionName))!).HasName("region_name");
        model.HasDbFunction(typeof(LocalizedSql).GetMethod(nameof(LocalizedSql.ObjectTypeName))!).HasName("object_type_name");
        model.HasDbFunction(typeof(LocalizedSql).GetMethod(nameof(LocalizedSql.ReferenceEntryName))!).HasName("reference_entry_name");
        model.HasDbFunction(typeof(LocalizedSql).GetMethod(nameof(LocalizedSql.ReferenceEntryMatches))!).HasName("reference_entry_matches");
        model.HasDbFunction(typeof(LocalizedSql).GetMethod(nameof(LocalizedSql.PartyName))!).HasName("party_name");

        model.Entity<ArtefactRow>(b =>
        {
            b.HasNoKey();
            b.ToView("v_artefact");
            b.Property(r => r.AcquisitionPriceAmount).HasPrecision(19, 4);
            b.Property(r => r.ValuationAmount).HasPrecision(19, 4);
        });

        model.Entity<ProvenanceRow>(b => { b.HasNoKey(); b.ToView("v_artefact_provenance"); });
        model.Entity<ImageRow>(b => { b.HasNoKey(); b.ToView("v_artefact_image"); });
        model.Entity<ExhibitionRow>(b => { b.HasNoKey(); b.ToView("v_artefact_exhibition"); });
        model.Entity<DescriptionRow>(b => { b.HasNoKey(); b.ToView("v_artefact_description"); });
    }
}
