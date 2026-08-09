using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A school can be affiliated to several boards at once (CBSE plus a State
    /// stream is ordinary in India), each with its own affiliation number, so
    /// the single pair of columns on <c>tenants</c> becomes a child table.
    /// <para>
    /// Order matters and is NOT what EF scaffolded: create and BACKFILL first,
    /// drop the old columns last. Scaffolding put the drops first, which would
    /// have thrown away every school's existing affiliation.
    /// </para>
    /// </summary>
    public partial class AddTenantAffiliations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_affiliations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    affiliation_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_affiliations", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_affiliations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_affiliations_tenant_id_board",
                table: "tenant_affiliations",
                columns: new[] { "tenant_id", "board" },
                unique: true);

            // Carry every existing affiliation across before the columns go.
            // gen_random_uuid() is built in from PostgreSQL 13; the platform
            // runs 16.
            migrationBuilder.Sql(
                """
                INSERT INTO tenant_affiliations (id, tenant_id, board, affiliation_number)
                SELECT gen_random_uuid(), id, btrim(affiliation_board), affiliation_number
                FROM tenants
                WHERE affiliation_board IS NOT NULL AND btrim(affiliation_board) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "affiliation_board",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "affiliation_number",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "affiliation_board",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "affiliation_number",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Only one can survive going back; keep the alphabetically first
            // so the result is deterministic rather than whatever the table
            // happens to return.
            migrationBuilder.Sql(
                """
                UPDATE tenants t
                SET affiliation_board = a.board,
                    affiliation_number = a.affiliation_number
                FROM (
                    SELECT DISTINCT ON (tenant_id) tenant_id, board, affiliation_number
                    FROM tenant_affiliations
                    ORDER BY tenant_id, board
                ) a
                WHERE a.tenant_id = t.id;
                """);

            migrationBuilder.DropTable(
                name: "tenant_affiliations");
        }
    }
}
