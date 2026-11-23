using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261123121456_RoutineDelays : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertRoutineDelays",
            columns: table => new
            {
                RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                Cycle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Minutes = table.Column<int>(type: "integer", nullable: false),
                CycleAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                At = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertRoutineDelays", x => new { x.RoutineId, x.Cycle });
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertRoutineDelays_CycleAt",
            table: "AlertRoutineDelays",
            column: "CycleAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertRoutineDelays");
    }
}
