using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_product_image_uploads_status",
                table: "product_image_uploads");

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "product_image_uploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cleanup_attempts",
                table: "product_image_uploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "product_image_uploads",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "product_image_uploads",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_started_at",
                table: "product_image_uploads",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "staging_deleted_at",
                table: "product_image_uploads",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_image_uploads_status_next_attempt_at_expires_at",
                table: "product_image_uploads",
                columns: new[] { "status", "next_attempt_at", "expires_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_image_uploads_status",
                table: "product_image_uploads",
                sql: "status IN (0, 1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE product_image_uploads SET status = 3 WHERE status = 4");

            migrationBuilder.DropIndex(
                name: "IX_product_image_uploads_status_next_attempt_at_expires_at",
                table: "product_image_uploads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_image_uploads_status",
                table: "product_image_uploads");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "product_image_uploads");

            migrationBuilder.DropColumn(
                name: "cleanup_attempts",
                table: "product_image_uploads");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "product_image_uploads");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "product_image_uploads");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "product_image_uploads");

            migrationBuilder.DropColumn(
                name: "staging_deleted_at",
                table: "product_image_uploads");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_image_uploads_status",
                table: "product_image_uploads",
                sql: "status IN (0, 1, 2, 3)");
        }
    }
}
