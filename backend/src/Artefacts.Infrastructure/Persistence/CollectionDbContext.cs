using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Imports;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Lists;
using Artefacts.Domain.Measures;
using Artefacts.Domain.Media;
using Artefacts.Domain.Pages;
using Artefacts.Domain.Parties;
using Artefacts.Domain.Queries;
using Artefacts.Domain.Regions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Artefacts.Infrastructure.Persistence;

/// <summary>Write-side context: maps every aggregate. Aggregates reference each other by id only.</summary>
public sealed class CollectionDbContext(DbContextOptions<CollectionDbContext> options) : DbContext(options)
{
    public const string InventoryNumberIndex = "ux_artefacts_inventory_number";
    public const string RegionNameIndex = "ux_region_names_language_normalized_name";
    public const string ObjectTypeNameIndex = "ux_object_type_names_language_normalized_name";
    public const string LanguageForeignKeyPrefix = "fk_language_";
    public const string ImportTemplateNameIndex = "ux_import_templates_normalized_name";
    public const string ImportDetectorNameIndex = "ux_import_detectors_normalized_name";
    public const string ReferenceEntryNameIndex = "ux_reference_entry_names_kind_language_normalized_name";
    public const string ReferenceEntrySingleNameIndex = "ux_reference_entries_kind_normalized_name";
    public const string PartyNameIndex = "ux_parties_normalized_name";
    public const string StoredQueryNameIndex = "ux_stored_queries_normalized_name";
    public const string StoredPageNameIndex = "ux_stored_pages_normalized_name";
    public const string StoredPageSlugIndex = "ux_stored_pages_slug";
    public const string UnitNameIndex = "ux_unit_of_measure_names_language_normalized_name";
    public const string UnitAbbreviationIndex = "ux_units_of_measure_normalized_abbreviation";

    public DbSet<Artefact> Artefacts => Set<Artefact>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ObjectType> ObjectTypes => Set<ObjectType>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<ReferenceEntry> ReferenceEntries => Set<ReferenceEntry>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportTemplate> ImportTemplates => Set<ImportTemplate>();
    public DbSet<ImportDetector> ImportDetectors => Set<ImportDetector>();
    public DbSet<ImportLookupTable> ImportLookupTables => Set<ImportLookupTable>();
    public DbSet<StoredQuery> StoredQueries => Set<StoredQuery>();
    public DbSet<StoredPage> StoredPages => Set<StoredPage>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<ArtefactId>().HaveConversion<ArtefactIdConverter>();
        builder.Properties<RegionId>().HaveConversion<RegionIdConverter>();
        builder.Properties<MediaAssetId>().HaveConversion<MediaAssetIdConverter>();
        builder.Properties<ObjectTypeId>().HaveConversion<ObjectTypeIdConverter>();
        builder.Properties<ReferenceEntryId>().HaveConversion<ReferenceEntryIdConverter>();
        builder.Properties<PartyId>().HaveConversion<PartyIdConverter>();
        builder.Properties<UnitOfMeasureId>().HaveConversion<UnitOfMeasureIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Language>(b =>
        {
            b.ToTable("languages");
            b.HasKey(l => l.Code);
            b.Property(l => l.Code).HasMaxLength(10);
            b.Property(l => l.Name).HasMaxLength(Language.MaxNameLength).IsRequired();
            // Initial content languages; more are added as data.
            b.HasData(
                new { Code = "en", Name = "English", Position = 1 },
                new { Code = "it", Name = "Italian", Position = 2 });
        });

        model.Entity<Region>(b =>
        {
            b.ToTable("regions");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedNever();
            b.Property(r => r.ParentId).HasConversion(new RegionIdConverter());
            b.HasOne<Region>().WithMany().HasForeignKey(r => r.ParentId).OnDelete(DeleteBehavior.Restrict);
            b.OwnsMany(r => r.Names, n => MapNames(n, "region_names", "RegionId", Region.MaxNameLength, RegionNameIndex));
            b.Navigation(r => r.Names).HasField("_names").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        model.Entity<ObjectType>(b =>
        {
            b.ToTable("object_types");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).ValueGeneratedNever();
            b.OwnsMany(t => t.Names, n => MapNames(n, "object_type_names", "ObjectTypeId", ObjectType.MaxNameLength, ObjectTypeNameIndex));
            b.Navigation(t => t.Names).HasField("_names").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        model.Entity<UnitOfMeasure>(b =>
        {
            b.ToTable("units_of_measure");
            b.HasKey(u => u.Id);
            b.Property(u => u.Id).ValueGeneratedNever();
            b.Property(u => u.Abbreviation).HasMaxLength(UnitOfMeasure.MaxAbbreviationLength).IsRequired();
            b.Property(u => u.NormalizedAbbreviation).HasMaxLength(UnitOfMeasure.MaxAbbreviationLength).IsRequired();
            b.HasIndex(u => u.NormalizedAbbreviation).IsUnique().HasDatabaseName(UnitAbbreviationIndex);
            b.Property(u => u.Factor).HasPrecision(28, 12);
            b.OwnsMany(u => u.Names, n =>
            {
                MapNames(n, "unit_of_measure_names", "UnitOfMeasureId", UnitOfMeasure.MaxNameLength, UnitNameIndex);
                n.HasData(SeededUnits.All.SelectMany(u => new object[]
                {
                    new { UnitOfMeasureId = u.Id, Language = "en", Name = u.English, NormalizedName = u.English.ToLowerInvariant() },
                    new { UnitOfMeasureId = u.Id, Language = "it", Name = u.Italian, NormalizedName = u.Italian.ToLowerInvariant() },
                }));
            });
            b.Navigation(u => u.Names).HasField("_names").UsePropertyAccessMode(PropertyAccessMode.Field);
            // Initial units; more are added as data.
            b.HasData(SeededUnits.All.Select(u => new { u.Id, u.Abbreviation, NormalizedAbbreviation = u.Abbreviation, u.Factor }));
        });

        model.Entity<ReferenceEntry>(b =>
        {
            b.ToTable("reference_entries");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(24);
            b.HasIndex(e => e.Kind);
            // Untranslated lists (storage locations) keep their single name here, unique per list.
            b.Property(e => e.Name).HasMaxLength(ReferenceEntry.MaxNameLength);
            b.Property(e => e.NormalizedName).HasMaxLength(ReferenceEntry.MaxNameLength);
            b.HasIndex(e => new { e.Kind, e.NormalizedName }).IsUnique()
                .HasFilter("normalized_name IS NOT NULL").HasDatabaseName(ReferenceEntrySingleNameIndex);
            // Translated lists keep a name per language, unique per list and language.
            b.OwnsMany(e => e.Names, n =>
            {
                n.ToTable("reference_entry_names");
                n.WithOwner().HasForeignKey("ReferenceEntryId");
                n.HasKey("ReferenceEntryId", nameof(ReferenceEntryName.Language));
                n.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
                n.Property(x => x.Language).HasMaxLength(10);
                n.Property(x => x.Name).HasMaxLength(ReferenceEntry.MaxNameLength).IsRequired();
                n.Property(x => x.NormalizedName).HasMaxLength(ReferenceEntry.MaxNameLength).IsRequired();
                n.HasIndex(x => new { x.Kind, x.Language, x.NormalizedName }).IsUnique().HasDatabaseName(ReferenceEntryNameIndex);
                n.HasIndex(x => x.NormalizedName);
                n.HasOne<Language>().WithMany().HasForeignKey(x => x.Language)
                    .HasConstraintName($"{LanguageForeignKeyPrefix}reference_entry_names").OnDelete(DeleteBehavior.Restrict);
            });
            b.Navigation(e => e.Names).HasField("_names").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        model.Entity<Party>(b =>
        {
            b.ToTable("parties");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.Name).HasMaxLength(Party.MaxNameLength).IsRequired();
            b.Property(p => p.NormalizedName).HasMaxLength(Party.MaxNameLength).IsRequired();
            b.HasIndex(p => p.NormalizedName).IsUnique().HasDatabaseName(PartyNameIndex);
            b.Property(p => p.Kind).HasConversion<string>().HasMaxLength(16);
        });

        model.Entity<MediaAsset>(b =>
        {
            b.ToTable("media_assets");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(m => m.StorageKey).HasMaxLength(300).IsRequired();
            b.HasIndex(m => m.StorageKey).IsUnique();
            b.Property(m => m.OriginalFileName).HasMaxLength(MediaAsset.MaxFileNameLength).IsRequired();
            b.Property(m => m.Format).HasConversion<string>().HasMaxLength(16);
        });

        model.Entity<Artefact>(b =>
        {
            b.ToTable("artefacts");
            b.HasKey(a => a.Id);
            b.Property(a => a.Id).ValueGeneratedNever();
            b.Property(a => a.InventoryNumber).HasMaxLength(Artefact.MaxInventoryNumberLength).IsRequired();
            b.HasIndex(a => a.InventoryNumber).IsUnique().HasDatabaseName(InventoryNumberIndex);
            b.Property(a => a.Title).HasMaxLength(Artefact.MaxTitleLength).IsRequired();
            b.Property(a => a.Maker).HasMaxLength(Artefact.MaxShortTextLength);
            b.Property(a => a.Medium).HasMaxLength(Artefact.MaxShortTextLength);
            b.Property(a => a.Dimensions).HasMaxLength(Artefact.MaxShortTextLength);
            b.Ignore(a => a.PrimaryImage);
            b.Ignore(a => a.Measurements);
            MapMeasurement(b, a => a.Width, "width");
            MapMeasurement(b, a => a.Height, "height");
            MapMeasurement(b, a => a.Length, "length");
            MapMeasurement(b, a => a.Diameter, "diameter");

            b.Property(a => a.ObjectTypeId).HasConversion(new ObjectTypeIdConverter());
            b.HasOne<ObjectType>().WithMany().HasForeignKey(a => a.ObjectTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ReferenceEntry>().WithMany().HasForeignKey(a => a.CollectionGroupId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ReferenceEntry>().WithMany().HasForeignKey(a => a.DynastyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ReferenceEntry>().WithMany().HasForeignKey(a => a.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Party>().WithMany().HasForeignKey(a => a.HolderId).OnDelete(DeleteBehavior.Restrict);

            b.OwnsOne(a => a.PlaceOfProduction, p =>
            {
                p.Property(x => x.Text).HasColumnName("place_text").HasMaxLength(PlaceOfProduction.MaxTextLength);
                p.Property(x => x.RegionId).HasColumnName("place_region_id").HasConversion(new RegionIdConverter());
                p.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
                p.HasIndex(x => x.RegionId);
            });

            b.OwnsOne(a => a.Valuation, m =>
            {
                m.Property(x => x.Amount).HasColumnName("valuation_amount").HasPrecision(19, 4);
                m.Property(x => x.Currency).HasColumnName("valuation_currency").HasMaxLength(3).IsFixedLength();
            });

            b.OwnsMany(a => a.Exhibitions, e =>
            {
                e.ToTable("artefact_exhibitions");
                e.WithOwner().HasForeignKey("ArtefactId");
                e.Property<int>("Id").UseIdentityAlwaysColumn();
                e.HasKey("Id");
                e.Property(x => x.Position);
                e.Property(x => x.Text).HasMaxLength(Exhibition.MaxTextLength).IsRequired();
            });
            b.Navigation(a => a.Exhibitions).HasField("_exhibitions").UsePropertyAccessMode(PropertyAccessMode.Field);

            b.OwnsMany(a => a.Translations, t =>
            {
                t.ToTable("artefact_translations");
                t.WithOwner().HasForeignKey("ArtefactId");
                t.HasKey("ArtefactId", nameof(ArtefactTranslation.Language));
                t.Property(x => x.Language).HasMaxLength(10);
                t.Property(x => x.Description).HasMaxLength(Artefact.MaxDescriptionLength);
                t.Property(x => x.CatalogueEntry).HasMaxLength(Artefact.MaxCatalogueEntryLength);
                t.HasOne<Language>().WithMany().HasForeignKey(x => x.Language)
                    .HasConstraintName($"{LanguageForeignKeyPrefix}artefact_translations").OnDelete(DeleteBehavior.Restrict);
            });
            b.Navigation(a => a.Translations).HasField("_translations").UsePropertyAccessMode(PropertyAccessMode.Field);

            b.OwnsOne(a => a.CreationDate, d =>
            {
                d.Property(x => x.Display).HasColumnName("creation_date_display").HasMaxLength(200);
                d.Property(x => x.EarliestYear).HasColumnName("creation_date_earliest_year");
                d.Property(x => x.LatestYear).HasColumnName("creation_date_latest_year");
                d.Property(x => x.Precision).HasColumnName("creation_date_precision").HasConversion<string>().HasMaxLength(16);
                d.Ignore(x => x.SortYear);
            });

            b.OwnsOne(a => a.Acquisition, q =>
            {
                q.Property(x => x.Method).HasColumnName("acquisition_method").HasConversion<string>().HasMaxLength(16);
                q.Property(x => x.Date).HasColumnName("acquisition_date");
                q.Property(x => x.SourceId).HasColumnName("acquisition_source_id");
                q.HasOne<Party>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
                q.HasIndex(x => x.SourceId);
                q.Property(x => x.Lot).HasColumnName("acquisition_lot").HasMaxLength(Acquisition.MaxLotLength);
                q.Property(x => x.Note).HasColumnName("acquisition_note").HasMaxLength(Acquisition.MaxNoteLength);
                q.OwnsOne(x => x.Price, m =>
                {
                    m.Property(x => x.Amount).HasColumnName("acquisition_price_amount").HasPrecision(19, 4);
                    m.Property(x => x.Currency).HasColumnName("acquisition_price_currency").HasMaxLength(3).IsFixedLength();
                });
            });

            b.OwnsMany(a => a.Provenance, p =>
            {
                p.ToTable("artefact_provenance");
                p.WithOwner().HasForeignKey("ArtefactId");
                p.Property<int>("Id").UseIdentityAlwaysColumn();
                p.HasKey("Id");
                p.Property(x => x.Position);
                p.Property(x => x.Note).HasMaxLength(2000);
                p.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
                p.OwnsOne(x => x.Period, d =>
                {
                    d.Property(x => x.Display).HasColumnName("period_display").HasMaxLength(200);
                    d.Property(x => x.EarliestYear).HasColumnName("period_earliest_year");
                    d.Property(x => x.LatestYear).HasColumnName("period_latest_year");
                    d.Property(x => x.Precision).HasColumnName("period_precision").HasConversion<string>().HasMaxLength(16);
                    d.Ignore(x => x.SortYear);
                });
                p.Navigation(x => x.Period).IsRequired();
            });
            b.Navigation(a => a.Provenance).HasField("_provenance").UsePropertyAccessMode(PropertyAccessMode.Field);

            b.OwnsMany(a => a.Images, i =>
            {
                i.ToTable("artefact_images");
                i.WithOwner().HasForeignKey("ArtefactId");
                i.Property<int>("Id").UseIdentityAlwaysColumn();
                i.HasKey("Id");
                i.Property(x => x.Caption).HasMaxLength(1000);
                i.HasIndex(x => x.MediaAssetId).IsUnique();
                i.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
            });
            b.Navigation(a => a.Images).HasField("_images").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        MapImports(model);
    }

    /// <summary>Import jobs, their staged rows and saved templates. Mappings and staged values are JSON documents.</summary>
    private static void MapImports(ModelBuilder model)
    {
        model.Entity<ImportJob>(b =>
        {
            b.ToTable("import_jobs");
            b.HasKey(j => j.Id);
            b.Property(j => j.FileName).HasMaxLength(ImportJob.MaxFileNameLength).IsRequired();
            b.Property(j => j.State).HasConversion<string>().HasMaxLength(24);
            b.Property(j => j.SheetName).HasMaxLength(200);
            foreach (var json in new[] { nameof(ImportJob.SheetNames), nameof(ImportJob.Headings), nameof(ImportJob.Mapping), nameof(ImportJob.Decisions), nameof(ImportJob.Warnings) })
                b.Property(json).HasColumnType("jsonb");
            b.Property(j => j.Error).HasMaxLength(2000);
            b.HasIndex(j => new { j.State, j.CreatedAt });
            b.HasOne<ObjectType>().WithMany().HasForeignKey(j => j.ObjectTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ImportTemplate>().WithMany().HasForeignKey(j => j.TemplateId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<ImportRow>(b =>
        {
            b.ToTable("import_rows");
            b.HasKey(r => r.Id);
            b.Property(r => r.GroupNumber).HasMaxLength(20);
            b.Property(r => r.GroupLabel).HasMaxLength(500);
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(r => r.InventoryNumber).HasMaxLength(500);
            foreach (var json in new[] { nameof(ImportRow.Cells), nameof(ImportRow.Pictures), nameof(ImportRow.Warnings), nameof(ImportRow.Problems) })
                b.Property(json).HasColumnType("jsonb");
            b.HasIndex(r => new { r.JobId, r.Status });
            b.HasIndex(r => new { r.JobId, r.SheetRow }).IsUnique();
            b.HasOne<ImportJob>().WithMany().HasForeignKey(r => r.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<StoredQuery>(b =>
        {
            b.ToTable("stored_queries");
            b.HasKey(q => q.Id);
            b.Property(q => q.Id).HasMaxLength(64).ValueGeneratedNever();
            b.Property(q => q.Name).HasMaxLength(StoredQuery.MaxNameLength).IsRequired();
            b.Property(q => q.NormalizedName).HasMaxLength(StoredQuery.MaxNameLength).IsRequired();
            b.HasIndex(q => q.NormalizedName).IsUnique().HasDatabaseName(StoredQueryNameIndex);
            b.Property(q => q.Document).IsRequired();
        });

        model.Entity<StoredPage>(b =>
        {
            b.ToTable("stored_pages");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).HasMaxLength(64).ValueGeneratedNever();
            b.Property(p => p.Slug).HasMaxLength(StoredPage.MaxSlugLength).IsRequired();
            b.Property(p => p.Name).HasMaxLength(StoredPage.MaxNameLength).IsRequired();
            b.Property(p => p.NormalizedName).HasMaxLength(StoredPage.MaxNameLength).IsRequired();
            b.Property(p => p.NavigationLabel).HasMaxLength(StoredPage.MaxNavigationLabelLength);
            // json, not jsonb: jsonb reorders object keys, and the order of a widget's bindings is the order its fields are shown in.
            b.Property(p => p.Definition).HasColumnType("json").IsRequired();
            b.HasIndex(p => p.Slug).IsUnique().HasDatabaseName(StoredPageSlugIndex);
            b.HasIndex(p => p.NormalizedName).IsUnique().HasDatabaseName(StoredPageNameIndex);
        });

        model.Entity<ImportTemplate>(b =>
        {
            b.ToTable("import_templates");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).HasMaxLength(ImportTemplate.MaxNameLength).IsRequired();
            b.Property(t => t.NormalizedName).HasMaxLength(ImportTemplate.MaxNameLength).IsRequired();
            b.HasIndex(t => t.NormalizedName).IsUnique().HasDatabaseName(ImportTemplateNameIndex);
            b.Property(t => t.Mapping).HasColumnType("jsonb");
            b.Property(t => t.Decisions).HasColumnType("jsonb");
        });

        model.Entity<ImportDetector>(b =>
        {
            b.ToTable("import_detectors");
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).HasMaxLength(ImportDetector.MaxNameLength).IsRequired();
            b.Property(d => d.NormalizedName).HasMaxLength(ImportDetector.MaxNameLength).IsRequired();
            b.HasIndex(d => d.NormalizedName).IsUnique().HasDatabaseName(ImportDetectorNameIndex);
            b.Property(d => d.Definition).HasColumnType("jsonb");
        });

        model.Entity<ImportLookupTable>(b =>
        {
            b.ToTable("import_lookup_tables");
            b.HasKey(t => t.TableId);
            b.Property(t => t.TableId).HasMaxLength(ImportLookupTable.MaxTableIdLength);
            b.Property(t => t.Entries).HasColumnType("jsonb");
        });
    }

    /// <summary>One measurement as <c>{prefix}_value</c> and <c>{prefix}_unit_id</c>; a unit in use cannot be deleted.</summary>
    private static void MapMeasurement(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Artefact> b,
        System.Linq.Expressions.Expression<Func<Artefact, Measurement?>> navigation, string prefix) =>
        b.OwnsOne(navigation, m =>
        {
            m.Property(x => x.Value).HasColumnName($"{prefix}_value").HasPrecision(19, 4);
            m.Property(x => x.UnitId).HasColumnName($"{prefix}_unit_id");
            m.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => x.UnitId)
                .HasConstraintName($"fk_artefacts_units_of_measure_{prefix}").OnDelete(DeleteBehavior.Restrict);
            m.HasIndex(x => x.UnitId);
        });

    /// <summary>Per-language names of a reference entry: one row per (owner, language), unique name per language.</summary>
    private static void MapNames<TOwner>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<TOwner, LocalizedName> n,
        string table, string ownerKey, int maxLength, string uniqueIndex) where TOwner : class
    {
        n.ToTable(table);
        n.WithOwner().HasForeignKey(ownerKey);
        n.HasKey(ownerKey, nameof(LocalizedName.Language));
        n.Property(x => x.Language).HasMaxLength(10);
        n.Property(x => x.Name).HasMaxLength(maxLength).IsRequired();
        n.Property(x => x.NormalizedName).HasMaxLength(maxLength).IsRequired();
        n.HasIndex(x => new { x.Language, x.NormalizedName }).IsUnique().HasDatabaseName(uniqueIndex);
        n.HasIndex(x => x.NormalizedName);
        n.HasOne<Language>().WithMany().HasForeignKey(x => x.Language)
            .HasConstraintName($"{LanguageForeignKeyPrefix}{table}").OnDelete(DeleteBehavior.Restrict);
    }

    private sealed class ArtefactIdConverter() : ValueConverter<ArtefactId, Guid>(id => id.Value, v => new ArtefactId(v));
    private sealed class RegionIdConverter() : ValueConverter<RegionId, Guid>(id => id.Value, v => new RegionId(v));
    private sealed class ObjectTypeIdConverter() : ValueConverter<ObjectTypeId, Guid>(id => id.Value, v => new ObjectTypeId(v));
    private sealed class ReferenceEntryIdConverter() : ValueConverter<ReferenceEntryId, Guid>(id => id.Value, v => new ReferenceEntryId(v));
    private sealed class PartyIdConverter() : ValueConverter<PartyId, Guid>(id => id.Value, v => new PartyId(v));
    private sealed class UnitOfMeasureIdConverter() : ValueConverter<UnitOfMeasureId, Guid>(id => id.Value, v => new UnitOfMeasureId(v));
    private sealed class MediaAssetIdConverter() : ValueConverter<MediaAssetId, Guid>(id => id.Value, v => new MediaAssetId(v));
}
