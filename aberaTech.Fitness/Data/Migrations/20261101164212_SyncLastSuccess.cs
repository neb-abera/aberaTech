using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261101164212_SyncLastSuccess : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Instant>(
            name: "LastSuccessAt",
            table: "SyncStates",
            type: "timestamp with time zone",
            nullable: true);

        // Every run recorded so far that did not end in a failure message.
        migrationBuilder.Sql(
            """
            UPDATE "SyncStates" SET "LastSuccessAt" = "LastRunAt"
            WHERE "LastOutcome" IS NOT NULL AND "LastOutcome" NOT LIKE '%did not answer%';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastSuccessAt",
            table: "SyncStates");
    }
}
