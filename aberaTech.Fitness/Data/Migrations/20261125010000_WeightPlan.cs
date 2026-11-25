using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261125010000_WeightPlan : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "WeightPlanKgPerWeek",
            table: "Settings",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<LocalDate>(
            name: "WeightPlanStartedOn",
            table: "Settings",
            type: "date",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WeightPlanKgPerWeek",
            table: "Settings");

        migrationBuilder.DropColumn(
            name: "WeightPlanStartedOn",
            table: "Settings");
    }
}
