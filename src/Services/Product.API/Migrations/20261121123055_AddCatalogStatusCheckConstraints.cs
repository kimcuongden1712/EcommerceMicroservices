using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogStatusCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_products_status",
                table: "products",
                sql: "status IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_variants_status",
                table: "product_variants",
                sql: "status IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_image_uploads_status",
                table: "product_image_uploads",
                sql: "status IN (0, 1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_categories_status",
                table: "categories",
                sql: "status IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_products_status",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_variants_status",
                table: "product_variants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_image_uploads_status",
                table: "product_image_uploads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_categories_status",
                table: "categories");
        }
    }
}
