using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261113004559_AlertEventLeads : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertEventLeads",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                LeadMinutes = table.Column<int>(type: "integer", nullable: false),
                UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                LastSeenAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertEventLeads", x => x.Key);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertEventLeads_LastSeenAt",
            table: "AlertEventLeads",
            column: "LastSeenAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertEventLeads");
    }
}
