using Artefacts.Infrastructure.Read;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artefacts.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates <c>v_artefact</c>. Convention: a view's SQL file is never edited once released; a change
    /// adds a new file (e.g. <c>v_artefact.v2.sql</c>) and a new migration, so old migrations stay replayable.
    /// </summary>
    public partial class ArtefactReadView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ViewSql.Load("v_artefact"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_artefact;");
        }
    }
}
