using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Media;
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

    public DbSet<Artefact> Artefacts => Set<Artefact>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ObjectType> ObjectTypes => Set<ObjectType>();
    public DbSet<Language> Languages => Set<Language>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<ArtefactId>().HaveConversion<ArtefactIdConverter>();
        builder.Properties<RegionId>().HaveConversion<RegionIdConverter>();
        builder.Properties<MediaAssetId>().HaveConversion<MediaAssetIdConverter>();
        builder.Properties<ObjectTypeId>().HaveConversion<ObjectTypeIdConverter>();
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

            b.Property(a => a.ObjectTypeId).HasConversion(new ObjectTypeIdConverter());
            b.HasOne<ObjectType>().WithMany().HasForeignKey(a => a.ObjectTypeId).OnDelete(DeleteBehavior.Restrict);
            b.Property(a => a.CollectionGroup).HasMaxLength(Artefact.MaxShortTextLength);
            b.Property(a => a.Dynasty).HasMaxLength(Artefact.MaxShortTextLength);
            b.Property(a => a.CatalogueEntryEn).HasColumnName("catalogue_entry_en").HasMaxLength(Artefact.MaxCatalogueEntryLength);
            b.Property(a => a.Holder).HasMaxLength(Artefact.MaxShortTextLength);
            b.Property(a => a.StorageLocation).HasMaxLength(Artefact.MaxShortTextLength);

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
                q.Property(x => x.Source).HasColumnName("acquisition_source").HasMaxLength(300);
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
    }

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
    private sealed class MediaAssetIdConverter() : ValueConverter<MediaAssetId, Guid>(id => id.Value, v => new MediaAssetId(v));
}
