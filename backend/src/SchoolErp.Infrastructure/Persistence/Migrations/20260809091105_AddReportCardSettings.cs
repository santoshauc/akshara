using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportCardSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "report_card_show_attendance",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "report_card_show_remarks",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "report_card_signatories",
                table: "tenants",
                type: "text",
                nullable: true);

            // 2 = ReportCardTemplate.MarksAndGrades. The CLR default (0) is not
            // a member of the enum, so existing schools must be backfilled with
            // the real default or their report cards render an unknown layout.
            migrationBuilder.AddColumn<int>(
                name: "report_card_template",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "report_card_show_attendance",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "report_card_show_remarks",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "report_card_signatories",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "report_card_template",
                table: "tenants");
        }
    }
}
