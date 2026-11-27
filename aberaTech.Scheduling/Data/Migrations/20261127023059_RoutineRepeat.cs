using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261127023059_RoutineRepeat : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Every",
            table: "AlertRoutines",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "Repeat",
            table: "AlertRoutines",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "weekdays");

        migrationBuilder.AddColumn<LocalDate>(
            name: "StartsOn",
            table: "AlertRoutines",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Unit",
            table: "AlertRoutines",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "weeks");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Every",
            table: "AlertRoutines");

        migrationBuilder.DropColumn(
            name: "Repeat",
            table: "AlertRoutines");

        migrationBuilder.DropColumn(
            name: "StartsOn",
            table: "AlertRoutines");

        migrationBuilder.DropColumn(
            name: "Unit",
            table: "AlertRoutines");
    }
}
