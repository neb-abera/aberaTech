using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260928001223_AlertSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Priority = table.Column<int>(type: "integer", nullable: false),
                RepeatSeconds = table.Column<int>(type: "integer", nullable: false),
                StopAfterMinutes = table.Column<int>(type: "integer", nullable: false),
                Sound = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DefaultLeadMinutes = table.Column<int>(type: "integer", nullable: false),
                PollMinutes = table.Column<int>(type: "integer", nullable: false),
                LookaheadHours = table.Column<int>(type: "integer", nullable: false),
                IncludeAllDay = table.Column<bool>(type: "boolean", nullable: false),
                TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                OwnerEmails = table.Column<string[]>(type: "text[]", nullable: false),
                UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertSettings", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertSettings");
    }
}
