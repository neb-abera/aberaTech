using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261113174003_RacesAndPlaces : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RaceResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<LocalDate>(type: "date", nullable: false),
                Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                DistanceMeters = table.Column<double>(type: "double precision", nullable: false),
                Seconds = table.Column<double>(type: "double precision", nullable: false),
                AltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                RecordedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RaceResults", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrainingPlaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Since = table.Column<LocalDate>(type: "date", nullable: true),
                Place = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                AltitudeMeters = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrainingPlaces", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RaceResults_Date",
            table: "RaceResults",
            column: "Date");

        // The profile's anchor race becomes the first race entered by hand,
        // and the profile's home altitude the first place, so the log starts
        // from what the projections already read.
        migrationBuilder.Sql(
            """
            INSERT INTO "RaceResults" ("Id", "Date", "Name", "DistanceMeters", "Seconds", "AltitudeMeters", "RecordedAt")
            SELECT gen_random_uuid(), "VdotMeasuredOn", 'Anchor race from the profile', "AnchorDistanceMeters", "AnchorSeconds", "HomeAltitudeMeters", now()
            FROM "Settings"
            WHERE "AnchorDistanceMeters" IS NOT NULL AND "AnchorSeconds" IS NOT NULL AND "VdotMeasuredOn" IS NOT NULL;

            UPDATE "Settings" SET "VdotMeasuredOn" = NULL, "AnchorDistanceMeters" = NULL, "AnchorSeconds" = NULL
            WHERE "AnchorDistanceMeters" IS NOT NULL AND "AnchorSeconds" IS NOT NULL AND "VdotMeasuredOn" IS NOT NULL;

            INSERT INTO "TrainingPlaces" ("Id", "Since", "Place", "AltitudeMeters")
            SELECT gen_random_uuid(), NULL::date, 'Home altitude from the profile', "HomeAltitudeMeters"
            FROM "Settings";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RaceResults");

        migrationBuilder.DropTable(
            name: "TrainingPlaces");
    }
}
