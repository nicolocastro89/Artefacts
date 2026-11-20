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
                    acquisition_price_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artefacts", x => x.id);
                });

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
                name: "ix_regions_parent_id",
                table: "regions",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_regions_normalized_name",
                table: "regions",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artefact_images");

            migrationBuilder.DropTable(
                name: "artefact_provenance");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "artefacts");

            migrationBuilder.DropTable(
                name: "regions");
        }
    }
}
