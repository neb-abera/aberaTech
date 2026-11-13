using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261113152407_ThresholdLog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Thresholds",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Value = table.Column<double>(type: "double precision", nullable: false),
                MeasuredOn = table.Column<LocalDate>(type: "date", nullable: true),
                Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                RecordedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Thresholds", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Thresholds_Kind_MeasuredOn",
            table: "Thresholds",
            columns: new[] { "Kind", "MeasuredOn" });

        // The values in use carry over as the first entries, undated, so the
        // log starts from what the zones already read and nothing is lost.
        migrationBuilder.Sql(
            """
            INSERT INTO "Thresholds" ("Id", "Kind", "Value", "MeasuredOn", "Source", "ActivityId", "RecordedAt")
            SELECT gen_random_uuid(), 'aet-hr', "ReferenceHr", NULL::date, 'in use before the log was kept', NULL::uuid, now()
            FROM "Settings"
            UNION ALL
            SELECT gen_random_uuid(), 'lt-hr', "LtHr", NULL::date, 'in use before the log was kept', NULL::uuid, now()
            FROM "Settings" WHERE "LtHr" IS NOT NULL
            UNION ALL
            SELECT gen_random_uuid(), 'lt-pace', "LtSecondsPerKm", NULL::date, 'in use before the log was kept', NULL::uuid, now()
            FROM "Settings" WHERE "LtSecondsPerKm" IS NOT NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Thresholds");
    }
}
