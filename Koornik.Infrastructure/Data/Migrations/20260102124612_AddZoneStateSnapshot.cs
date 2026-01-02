using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Koornik.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneStateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoneStateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccupancyCount = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneStateSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneStateSnapshots_ZoneId_CreatedAt_Desc",
                table: "ZoneStateSnapshots",
                columns: new[] { "ZoneId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneStateSnapshots");
        }
    }
}
