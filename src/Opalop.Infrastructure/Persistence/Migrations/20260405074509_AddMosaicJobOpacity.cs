using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opalop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMosaicJobOpacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "opacity",
                table: "mosaic_jobs",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opacity",
                table: "mosaic_jobs");
        }
    }
}
