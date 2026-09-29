using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260929015646_AlertCreatedEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertCreatedEvents",
            columns: table => new
            {
                EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                LeadMinutes = table.Column<int>(type: "integer", nullable: false),
                Critical = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertCreatedEvents", x => x.EventId);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertCreatedEvents");
    }
}
