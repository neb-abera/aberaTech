using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260928193957_AlertEventTypes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultType",
            table: "AlertSettings",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "none");

        migrationBuilder.AddColumn<int>(
            name: "NotificationPriority",
            table: "AlertSettings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "NotificationSound",
            table: "AlertSettings",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "AlertEventTypes",
            columns: table => new
            {
                EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                LastSeenAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertEventTypes", x => x.EventId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertEventTypes_LastSeenAt",
            table: "AlertEventTypes",
            column: "LastSeenAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertEventTypes");

        migrationBuilder.DropColumn(
            name: "DefaultType",
            table: "AlertSettings");

        migrationBuilder.DropColumn(
            name: "NotificationPriority",
            table: "AlertSettings");

        migrationBuilder.DropColumn(
            name: "NotificationSound",
            table: "AlertSettings");
    }
}
