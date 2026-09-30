using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260930181237_AlertEventChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertEventChanges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Series = table.Column<bool>(type: "boolean", nullable: false),
                OriginalStartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                LocationSet = table.Column<bool>(type: "boolean", nullable: false),
                Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                EndsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                LeadMinutes = table.Column<int>(type: "integer", nullable: true),
                TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                AlertAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                Reminder = table.Column<bool>(type: "boolean", nullable: false),
                Critical = table.Column<bool>(type: "boolean", nullable: false),
                Recurring = table.Column<bool>(type: "boolean", nullable: false),
                PickedLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertEventChanges", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertEventChanges");
    }
}
