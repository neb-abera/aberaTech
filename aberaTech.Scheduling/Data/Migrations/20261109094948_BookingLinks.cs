using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261109094948_BookingLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "BookingLinkId",
            table: "Appointments",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "BookingLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                EarliestDate = table.Column<LocalDate>(type: "date", nullable: true),
                HorizonDays = table.Column<int>(type: "integer", nullable: false),
                LeadMinutes = table.Column<int>(type: "integer", nullable: false),
                AppointmentMinutes = table.Column<int>(type: "integer", nullable: false),
                ShowsQueue = table.Column<bool>(type: "boolean", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BookingLinks", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BookingLinks_IsDefault",
            table: "BookingLinks",
            column: "IsDefault",
            unique: true,
            filter: "\"IsDefault\"");

        migrationBuilder.CreateIndex(
            name: "IX_BookingLinks_KeyHash",
            table: "BookingLinks",
            column: "KeyHash",
            unique: true,
            filter: "\"KeyHash\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BookingLinks");

        migrationBuilder.DropColumn(
            name: "BookingLinkId",
            table: "Appointments");
    }
}
