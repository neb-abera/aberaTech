using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261126220325_RoutineStepEveryDays : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<short>(
            name: "EveryDays",
            table: "AlertRoutineSteps",
            type: "smallint",
            nullable: false,
            defaultValue: (short)1);

        migrationBuilder.AddColumn<LocalDate>(
            name: "StartsOn",
            table: "AlertRoutineSteps",
            type: "date",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EveryDays",
            table: "AlertRoutineSteps");

        migrationBuilder.DropColumn(
            name: "StartsOn",
            table: "AlertRoutineSteps");
    }
}
