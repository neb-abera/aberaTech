using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261119181241_RoutineSteps : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertRoutineCompletions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                StepId = table.Column<Guid>(type: "uuid", nullable: false),
                Cycle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                RoutineLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                StepLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                CycleAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                StepAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Photo = table.Column<byte[]>(type: "bytea", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertRoutineCompletions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AlertRoutineSteps",
            columns: table => new
            {
                RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<short>(type: "smallint", nullable: false),
                Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                OffsetMinutes = table.Column<short>(type: "smallint", nullable: false),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertRoutineSteps", x => new { x.RoutineId, x.Id });
                table.ForeignKey(
                    name: "FK_AlertRoutineSteps_AlertRoutines_RoutineId",
                    column: x => x.RoutineId,
                    principalTable: "AlertRoutines",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertRoutineCompletions_CycleAt",
            table: "AlertRoutineCompletions",
            column: "CycleAt");

        migrationBuilder.CreateIndex(
            name: "IX_AlertRoutineCompletions_RoutineId_StepId_Cycle",
            table: "AlertRoutineCompletions",
            columns: new[] { "RoutineId", "StepId", "Cycle" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertRoutineCompletions");

        migrationBuilder.DropTable(
            name: "AlertRoutineSteps");
    }
}
