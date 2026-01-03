using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Koornik.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneCurrentOccupancyAndGeoJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentOccupancy",
                table: "Zones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GeoJson",
                table: "Zones",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentOccupancy",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "GeoJson",
                table: "Zones");
        }
    }
}
