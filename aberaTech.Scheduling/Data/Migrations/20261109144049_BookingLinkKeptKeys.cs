using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261109144049_BookingLinkKeptKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Instant>(
            name: "KeyIssuedAt",
            table: "BookingLinks",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProtectedKey",
            table: "BookingLinks",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Instant>(
            name: "RevokedAt",
            table: "BookingLinks",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "KeyIssuedAt",
            table: "BookingLinks");

        migrationBuilder.DropColumn(
            name: "ProtectedKey",
            table: "BookingLinks");

        migrationBuilder.DropColumn(
            name: "RevokedAt",
            table: "BookingLinks");
    }
}
