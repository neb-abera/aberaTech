using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations
{
    /// <inheritdoc />
    public partial class CalendarAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertDeliveries",
                columns: table => new
                {
                    OccurrenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompletedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertDeliveries", x => x.OccurrenceKey);
                });

            migrationBuilder.CreateTable(
                name: "AlertMutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MutedUntil = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertMutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertSkips",
                columns: table => new
                {
                    OccurrenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertSkips", x => x.OccurrenceKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_ClaimedAt",
                table: "AlertDeliveries",
                column: "ClaimedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertDeliveries");

            migrationBuilder.DropTable(
                name: "AlertMutes");

            migrationBuilder.DropTable(
                name: "AlertSkips");
        }
    }
}
