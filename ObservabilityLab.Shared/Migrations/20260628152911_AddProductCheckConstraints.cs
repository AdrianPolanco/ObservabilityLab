using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObservabilityLab.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "stock_quantity",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_in_stock",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_StockQuantity",
                table: "products",
                sql: "stock_quantity >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IsInStock_StockQuantity",
                table: "products",
                sql: "(is_in_stock = true AND stock_quantity > 0) OR (is_in_stock = false AND stock_quantity = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_IsInStock_StockQuantity",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_StockQuantity",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_in_stock",
                table: "products");

            migrationBuilder.DropColumn(
                name: "stock_quantity",
                table: "products");
        }
    }
}
