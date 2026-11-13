using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261113132754_TreadmillGrade : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "TreadmillGradePercent",
            table: "Settings",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<LocalDate>(
            name: "TreadmillGradeSince",
            table: "Settings",
            type: "date",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TreadmillGradePercent",
            table: "Settings");

        migrationBuilder.DropColumn(
            name: "TreadmillGradeSince",
            table: "Settings");
    }
}
