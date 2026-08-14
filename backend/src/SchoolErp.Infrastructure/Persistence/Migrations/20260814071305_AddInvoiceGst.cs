using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceGst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gstin",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer_gstin",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cgst_amount",
                table: "invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "igst_amount",
                table: "invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "place_of_supply",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sac_code",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sgst_amount",
                table: "invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "supplier_gstin",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate_percent",
                table: "invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gstin",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "buyer_gstin",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "cgst_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "igst_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "place_of_supply",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "sac_code",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "sgst_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "supplier_gstin",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "tax_rate_percent",
                table: "invoices");
        }
    }
}
