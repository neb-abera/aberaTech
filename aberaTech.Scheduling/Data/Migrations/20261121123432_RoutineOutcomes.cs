using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261121123432_RoutineOutcomes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertRoutineOutcomes",
            columns: table => new
            {
                RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                Cycle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Status = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                RoutineLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                CycleAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                At = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertRoutineOutcomes", x => new { x.RoutineId, x.Cycle });
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertRoutineOutcomes_CycleAt",
            table: "AlertRoutineOutcomes",
            column: "CycleAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertRoutineOutcomes");
    }
}
