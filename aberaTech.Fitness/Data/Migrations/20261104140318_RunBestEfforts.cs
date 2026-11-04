using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261104140318_RunBestEfforts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "Best10MileSeconds",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Best1_5MileSeconds",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Best2MileSeconds",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Best5MileSeconds",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RecordsVersion",
            table: "Activities",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Best10MileSeconds",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "Best1_5MileSeconds",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "Best2MileSeconds",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "Best5MileSeconds",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "RecordsVersion",
            table: "Activities");
    }
}
