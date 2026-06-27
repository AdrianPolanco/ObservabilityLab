using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObservabilityLab.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ChangeColumNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "orders",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "orders",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "order_items",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "invoices",
                newName: "file_path");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_amount",
                table: "orders",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "order_items",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "orders",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "orders",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "order_items",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "file_path",
                table: "invoices",
                newName: "FilePath");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "orders",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "order_items",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }
    }
}
