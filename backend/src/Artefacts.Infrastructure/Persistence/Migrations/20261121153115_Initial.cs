using System;
using Artefacts.Infrastructure.Read;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Artefacts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mapping = table.Column<string>(type: "jsonb", nullable: false),
                    decisions = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_languages", x => x.code);
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
                name: "object_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reference_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "stored_queries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    document = table.Column<string>(type: "text", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_queries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stored_pages",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    definition = table.Column<string>(type: "json", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    show_in_navigation = table.Column<bool>(type: "boolean", nullable: false),
                    navigation_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_pages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    object_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sheet_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sheet_names = table.Column<string>(type: "jsonb", nullable: false),
                    headings = table.Column<string>(type: "jsonb", nullable: false),
                    mapping = table.Column<string>(type: "jsonb", nullable: true),
                    decisions = table.Column<string>(type: "jsonb", nullable: false),
                    warnings = table.Column<string>(type: "jsonb", nullable: false),
                    group_headings = table.Column<int>(type: "integer", nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    processed_rows = table.Column<int>(type: "integer", nullable: false),
                    imported_rows = table.Column<int>(type: "integer", nullable: false),
                    skipped_rows = table.Column<int>(type: "integer", nullable: false),
                    problem_rows = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_jobs_import_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "import_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_import_jobs_object_types_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "object_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "object_type_names",
                columns: table => new
                {
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    object_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_type_names", x => new { x.object_type_id, x.language });
                    table.ForeignKey(
                        name: "fk_language_object_type_names",
                        column: x => x.language,
                        principalTable: "languages",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_object_type_names_object_types_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "object_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reference_entry_names",
                columns: table => new
                {
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reference_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_entry_names", x => new { x.reference_entry_id, x.language });
                    table.ForeignKey(
                        name: "fk_language_reference_entry_names",
                        column: x => x.language,
                        principalTable: "languages",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reference_entry_names_reference_entries_reference_entry_id",
                        column: x => x.reference_entry_id,
                        principalTable: "reference_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    acquisition_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: true),
                    acquisition_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acquisition_price_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    acquisition_price_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    acquisition_lot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acquisition_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    object_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collection_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    place_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    place_region_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dynasty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    holder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_location_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                        name: "fk_artefacts_parties_acquisition_source_id",
                        column: x => x.acquisition_source_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_artefacts_parties_holder_id",
                        column: x => x.holder_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_artefacts_reference_entries_collection_group_id",
                        column: x => x.collection_group_id,
                        principalTable: "reference_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_artefacts_reference_entries_dynasty_id",
                        column: x => x.dynasty_id,
                        principalTable: "reference_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_artefacts_reference_entries_storage_location_id",
                        column: x => x.storage_location_id,
                        principalTable: "reference_entries",
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
                name: "region_names",
                columns: table => new
                {
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    region_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_region_names", x => new { x.region_id, x.language });
                    table.ForeignKey(
                        name: "fk_language_region_names",
                        column: x => x.language,
                        principalTable: "languages",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_region_names_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sheet_row = table.Column<int>(type: "integer", nullable: false),
                    group_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    group_label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cells = table.Column<string>(type: "jsonb", nullable: false),
                    pictures = table.Column<string>(type: "jsonb", nullable: false),
                    warnings = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    problems = table.Column<string>(type: "jsonb", nullable: false),
                    inventory_number = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    artefact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_rows_import_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "import_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "artefact_translations",
                columns: table => new
                {
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    artefact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    catalogue_entry = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artefact_translations", x => new { x.artefact_id, x.language });
                    table.ForeignKey(
                        name: "fk_artefact_translations_artefacts_artefact_id",
                        column: x => x.artefact_id,
                        principalTable: "artefacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_language_artefact_translations",
                        column: x => x.language,
                        principalTable: "languages",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "languages",
                columns: new[] { "code", "name", "position" },
                values: new object[,]
                {
                    { "en", "English", 1 },
                    { "it", "Italian", 2 }
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
                name: "ix_artefact_translations_language",
                table: "artefact_translations",
                column: "language");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_acquisition_source_id",
                table: "artefacts",
                column: "acquisition_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_collection_group_id",
                table: "artefacts",
                column: "collection_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_dynasty_id",
                table: "artefacts",
                column: "dynasty_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_holder_id",
                table: "artefacts",
                column: "holder_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_object_type_id",
                table: "artefacts",
                column: "object_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_place_region_id",
                table: "artefacts",
                column: "place_region_id");

            migrationBuilder.CreateIndex(
                name: "ix_artefacts_storage_location_id",
                table: "artefacts",
                column: "storage_location_id");

            migrationBuilder.CreateIndex(
                name: "ux_artefacts_inventory_number",
                table: "artefacts",
                column: "inventory_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_object_type_id",
                table: "import_jobs",
                column: "object_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_state_created_at",
                table: "import_jobs",
                columns: new[] { "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_template_id",
                table: "import_jobs",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_rows_job_id_sheet_row",
                table: "import_rows",
                columns: new[] { "job_id", "sheet_row" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_rows_job_id_status",
                table: "import_rows",
                columns: new[] { "job_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_import_templates_normalized_name",
                table: "import_templates",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_storage_key",
                table: "media_assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_object_type_names_normalized_name",
                table: "object_type_names",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ux_object_type_names_language_normalized_name",
                table: "object_type_names",
                columns: new[] { "language", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_parties_normalized_name",
                table: "parties",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_entries_kind",
                table: "reference_entries",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ux_reference_entries_kind_normalized_name",
                table: "reference_entries",
                columns: new[] { "kind", "normalized_name" },
                unique: true,
                filter: "normalized_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_reference_entry_names_language",
                table: "reference_entry_names",
                column: "language");

            migrationBuilder.CreateIndex(
                name: "ix_reference_entry_names_normalized_name",
                table: "reference_entry_names",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ux_reference_entry_names_kind_language_normalized_name",
                table: "reference_entry_names",
                columns: new[] { "kind", "language", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_region_names_normalized_name",
                table: "region_names",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ux_region_names_language_normalized_name",
                table: "region_names",
                columns: new[] { "language", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regions_parent_id",
                table: "regions",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_stored_queries_normalized_name",
                table: "stored_queries",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stored_pages_normalized_name",
                table: "stored_pages",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stored_pages_slug",
                table: "stored_pages",
                column: "slug",
                unique: true);

            // The name-resolving functions and the read view.
            migrationBuilder.Sql(ViewSql.Load("localized_names"));
            migrationBuilder.Sql(ViewSql.Load("v_artefact"));
            migrationBuilder.Sql(ViewSql.Load("v_artefact_lists"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_artefact_catalogue_entry; DROP VIEW IF EXISTS v_artefact_description; DROP VIEW IF EXISTS v_artefact_exhibition; " +
                                 "DROP VIEW IF EXISTS v_artefact_image; DROP VIEW IF EXISTS v_artefact_provenance;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_artefact;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS region_name(uuid, text[]); DROP FUNCTION IF EXISTS object_type_name(uuid, text[]); " +
                                 "DROP FUNCTION IF EXISTS reference_entry_name(uuid, text[]); DROP FUNCTION IF EXISTS reference_entry_matches(uuid, text); " +
                                 "DROP FUNCTION IF EXISTS party_name(uuid);");
            migrationBuilder.DropTable(
                name: "artefact_exhibitions");

            migrationBuilder.DropTable(
                name: "artefact_images");

            migrationBuilder.DropTable(
                name: "artefact_provenance");

            migrationBuilder.DropTable(
                name: "artefact_translations");

            migrationBuilder.DropTable(
                name: "import_rows");

            migrationBuilder.DropTable(
                name: "object_type_names");

            migrationBuilder.DropTable(
                name: "reference_entry_names");

            migrationBuilder.DropTable(
                name: "region_names");

            migrationBuilder.DropTable(
                name: "stored_pages");

            migrationBuilder.DropTable(
                name: "stored_queries");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "artefacts");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "parties");

            migrationBuilder.DropTable(
                name: "reference_entries");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropTable(
                name: "import_templates");

            migrationBuilder.DropTable(
                name: "object_types");
        }
    }
}
