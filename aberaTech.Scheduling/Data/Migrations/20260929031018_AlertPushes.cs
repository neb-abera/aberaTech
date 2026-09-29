using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260929031018_AlertPushes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApnsEnvironment",
            table: "AlertDevices",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ApnsToken",
            table: "AlertDevices",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Instant>(
            name: "PushedAt",
            table: "AlertDevices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "PushedVersion",
            table: "AlertDevices",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "AlertPushStates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                AlarmsFingerprint = table.Column<byte[]>(type: "bytea", nullable: true),
                UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertPushStates", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertPushStates");

        migrationBuilder.DropColumn(
            name: "ApnsEnvironment",
            table: "AlertDevices");

        migrationBuilder.DropColumn(
            name: "ApnsToken",
            table: "AlertDevices");

        migrationBuilder.DropColumn(
            name: "PushedAt",
            table: "AlertDevices");

        migrationBuilder.DropColumn(
            name: "PushedVersion",
            table: "AlertDevices");
    }
}
