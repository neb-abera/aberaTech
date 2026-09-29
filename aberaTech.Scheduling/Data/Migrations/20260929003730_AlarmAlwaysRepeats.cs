using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260929003730_AlarmAlwaysRepeats : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Priority",
            table: "AlertSettings");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "AlertSettings",
            type: "integer",
            nullable: false,
            // A rollback runs the release before this one, which reads the
            // alarm priority. 2 keeps its alarms repeating.
            defaultValue: 2);
    }
}
