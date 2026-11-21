using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Product.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    entity_type = table.Column<int>(type: "int", nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    actor_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    correlation_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    changed_fields_json = table.Column<string>(type: "json", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_audit", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_outbox",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    topic = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    payload_json = table.Column<string>(type: "json", nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_outbox", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "text", maxLength: 10000, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.CheckConstraint("ck_products_description_length", "CHAR_LENGTH(description) <= 10000");
                    table.ForeignKey(
                        name: "FK_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_image_uploads",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    staging_key = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    asset_key = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    expected_content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    expected_size = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_image_uploads", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_image_uploads_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    asset_key = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    alt_text = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    sku = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    color_code = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false),
                    color_label = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    size_code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    size_label = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    price_minor = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    weight_g = table.Column<int>(type: "int", nullable: true),
                    length_mm = table.Column<int>(type: "int", nullable: true),
                    width_mm = table.Column<int>(type: "int", nullable: true),
                    height_mm = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.CheckConstraint("ck_product_variants_dimensions", "weight_g > 0 AND length_mm > 0 AND width_mm > 0 AND height_mm > 0");
                    table.CheckConstraint("ck_product_variants_price", "price_minor BETWEEN 1 AND 99999999");
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_audit_entity_type_entity_id_occurred_at_id",
                table: "catalog_audit",
                columns: new[] { "entity_type", "entity_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_outbox_published_at_next_attempt_at_id",
                table: "catalog_outbox",
                columns: new[] { "published_at", "next_attempt_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_id_status_sort_order_id",
                table: "categories",
                columns: new[] { "parent_id", "status", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_image_uploads_product_id",
                table: "product_image_uploads",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_image_uploads_staging_key",
                table: "product_image_uploads",
                column: "staging_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_image_uploads_status_expires_at",
                table: "product_image_uploads",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_product_images_asset_key",
                table: "product_images",
                column: "asset_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_images_product_id_position",
                table: "product_images",
                columns: new[] { "product_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product_id_color_code_size_code",
                table: "product_variants",
                columns: new[] { "product_id", "color_code", "size_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product_id_status_price_minor_id",
                table: "product_variants",
                columns: new[] { "product_id", "status", "price_minor", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_sku",
                table: "product_variants",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id_status_published_at_id",
                table: "products",
                columns: new[] { "category_id", "status", "published_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_slug",
                table: "products",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_status_published_at_id",
                table: "products",
                columns: new[] { "status", "published_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_audit");

            migrationBuilder.DropTable(
                name: "catalog_outbox");

            migrationBuilder.DropTable(
                name: "product_image_uploads");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
