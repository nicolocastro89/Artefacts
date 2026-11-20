using Artefacts.Infrastructure.Read;
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Artefacts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "object_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                    table.ForeignKey(
                        name: "fk_regions_regions_parent_id",
                        column: x => x.parent_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artefacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    maker = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creation_date_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    creation_date_earliest_year = table.Column<int>(type: "integer", nullable: true),
                    creation_date_latest_year = table.Column<int>(type: "integer", nullable: true),
                    creation_date_precision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    medium = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dimensions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    acquisition_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: true),
                    acquisition_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    acquisition_price_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    acquisition_price_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    acquisition_lot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acquisition_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    object_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collection_group = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    place_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    place_region_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dynasty = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    catalogue_entry_en = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    holder = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    storage_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    valuation_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    valuation_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artefacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_artefacts_object_types_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "object_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_artefacts_regions_place_region_id",
                        column: x => x.place_region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artefact_exhibitions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    artefact_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artefact_exhibitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_artefact_exhibitions_artefacts_artefact_id",
                        column: x => x.artefact_id,
                        principalTable: "artefacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artefact_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    artefact_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artefact_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_artefact_images_artefacts_artefact_id",
                        column: x => x.artefact_id,
                        principalTable: "artefacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artefact_images_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artefact_provenance",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    region_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    period_earliest_year = table.Column<int>(type: "integer", nullable: true),
                    period_latest_year = table.Column<int>(type: "integer", nullable: true),
                    period_precision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    artefact_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artefact_provenance", x => x.id);
                    table.ForeignKey(
                        name: "fk_artefact_provenance_artefacts_artefact_id",
                        column: x => x.artefact_id,
                        principalTable: "artefacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artefact_provenance_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_artefact_exhibitions_artefact_id",
                table: "artefact_exhibitions",
                column: "artefact_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefact_images_artefact_id",
                table: "artefact_images",
                column: "artefact_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefact_images_media_asset_id",
                table: "artefact_images",
                column: "media_asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artefact_provenance_artefact_id",
                table: "artefact_provenance",
                column: "artefact_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefact_provenance_region_id",
                table: "artefact_provenance",
                column: "region_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_object_type_id",
                table: "artefacts",
                column: "object_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_place_region_id",
                table: "artefacts",
                column: "place_region_id");

            migrationBuilder.CreateIndex(
                name: "ux_artefacts_inventory_number",
                table: "artefacts",
                column: "inventory_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_storage_key",
                table: "media_assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_object_types_normalized_name",
                table: "object_types",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regions_parent_id",
                table: "regions",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_regions_normalized_name",
                table: "regions",
                column: "normalized_name",
                unique: true);

            // Grouping keys for free-text groupings (see the view) and the read view itself.
            migrationBuilder.Sql(
                @"CREATE INDEX ix_artefacts_collection_group_key ON artefacts (lower(regexp_replace(btrim(collection_group), '\s+', ' ', 'g')));");
            migrationBuilder.Sql(
                @"CREATE INDEX ix_artefacts_dynasty_key ON artefacts (lower(regexp_replace(btrim(dynasty), '\s+', ' ', 'g')));");
            migrationBuilder.Sql(ViewSql.Load("v_artefact"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_artefact;");

            migrationBuilder.DropTable(
                name: "artefact_exhibitions");

            migrationBuilder.DropTable(
                name: "artefact_images");

            migrationBuilder.DropTable(
                name: "artefact_provenance");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "artefacts");

            migrationBuilder.DropTable(
                name: "object_types");

            migrationBuilder.DropTable(
                name: "regions");
        }
    }
}
