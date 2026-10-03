using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261003065623_RoutineRingsAndPushoverAck : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TimeZone",
            table: "AlertDevices",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AlertHeldRings",
            columns: table => new
            {
                OccurrenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                AlertAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertHeldRings", x => x.OccurrenceKey);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertDeliveries_Receipt",
            table: "AlertDeliveries",
            column: "Receipt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertHeldRings");

        migrationBuilder.DropIndex(
            name: "IX_AlertDeliveries_Receipt",
            table: "AlertDeliveries");

        migrationBuilder.DropColumn(
            name: "TimeZone",
            table: "AlertDevices");
    }
}
