using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261104065448_RecordingHalves : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "FirstHalfHr",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "FirstHalfMetersPerSecond",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "SecondHalfHr",
            table: "Activities",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "SecondHalfMetersPerSecond",
            table: "Activities",
            type: "double precision",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FirstHalfHr",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "FirstHalfMetersPerSecond",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "SecondHalfHr",
            table: "Activities");

        migrationBuilder.DropColumn(
            name: "SecondHalfMetersPerSecond",
            table: "Activities");
    }
}
