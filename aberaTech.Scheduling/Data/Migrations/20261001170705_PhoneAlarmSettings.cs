using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261001170705_PhoneAlarmSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PhoneSnoozeMinutes",
            table: "AlertSettings",
            type: "integer",
            nullable: false,
            defaultValue: 9);

        migrationBuilder.AddColumn<string>(
            name: "PhoneSound",
            table: "AlertSettings",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "default");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PhoneSnoozeMinutes",
            table: "AlertSettings");

        migrationBuilder.DropColumn(
            name: "PhoneSound",
            table: "AlertSettings");
    }
}
