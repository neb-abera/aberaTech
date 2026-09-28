using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260928220345_AlertDevices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BackupDelaySeconds",
            table: "AlertSettings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Receipt",
            table: "AlertDeliveries",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AlertAcknowledgements",
            columns: table => new
            {
                OccurrenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                AcknowledgedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                Via = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertAcknowledgements", x => x.OccurrenceKey);
            });

        migrationBuilder.CreateTable(
            name: "AlertDevices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                LastSeenAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertDevices", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertDevices_TokenHash",
            table: "AlertDevices",
            column: "TokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertAcknowledgements");

        migrationBuilder.DropTable(
            name: "AlertDevices");

        migrationBuilder.DropColumn(
            name: "BackupDelaySeconds",
            table: "AlertSettings");

        migrationBuilder.DropColumn(
            name: "Receipt",
            table: "AlertDeliveries");
    }
}
