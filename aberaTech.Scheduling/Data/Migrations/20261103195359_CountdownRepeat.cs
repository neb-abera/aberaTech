using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261103195359_CountdownRepeat : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Repeat",
            table: "AlertCountdowns",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "none");

        migrationBuilder.AddColumn<int>(
            name: "RepeatEvery",
            table: "AlertCountdowns",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Repeat",
            table: "AlertCountdowns");

        migrationBuilder.DropColumn(
            name: "RepeatEvery",
            table: "AlertCountdowns");
    }
}
