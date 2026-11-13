using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261113150000_AnchorRace : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "AnchorDistanceMeters",
            table: "Settings",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "AnchorSeconds",
            table: "Settings",
            type: "double precision",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AnchorDistanceMeters",
            table: "Settings");

        migrationBuilder.DropColumn(
            name: "AnchorSeconds",
            table: "Settings");
    }
}
