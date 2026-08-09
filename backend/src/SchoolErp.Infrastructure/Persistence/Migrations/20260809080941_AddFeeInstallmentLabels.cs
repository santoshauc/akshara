using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeInstallmentLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "fee_structure_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "label",
                table: "fee_structure_items");
        }
    }
}
