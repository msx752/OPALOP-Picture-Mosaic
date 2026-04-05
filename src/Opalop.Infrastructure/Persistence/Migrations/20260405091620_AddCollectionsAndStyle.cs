using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opalop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionsAndStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "mosaic_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "style",
                table: "mosaic_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "photo_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_collections", x => x.id);
                    table.ForeignKey(
                        name: "fk_photo_collections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photos_collection_id",
                table: "photos",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_collections_user_id",
                table: "photo_collections",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_photos_photo_collections_collection_id",
                table: "photos",
                column: "collection_id",
                principalTable: "photo_collections",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_photos_photo_collections_collection_id",
                table: "photos");

            migrationBuilder.DropTable(
                name: "photo_collections");

            migrationBuilder.DropIndex(
                name: "ix_photos_collection_id",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "mosaic_jobs");

            migrationBuilder.DropColumn(
                name: "style",
                table: "mosaic_jobs");
        }
    }
}
