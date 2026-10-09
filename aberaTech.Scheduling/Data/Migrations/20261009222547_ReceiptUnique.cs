using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261009222547_ReceiptUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A receipt held by more than one delivery stays with the newest.
        // Development databases hold such pairs from the fake Pushover that
        // numbered receipts from 1 at each start (aberaTech #307).
        migrationBuilder.Sql(
            """
            UPDATE "AlertDeliveries" AS older SET "Receipt" = NULL
            WHERE older."Receipt" IS NOT NULL
              AND EXISTS (
                SELECT 1 FROM "AlertDeliveries" AS newer
                WHERE newer."Receipt" = older."Receipt"
                  AND (newer."ClaimedAt", newer."OccurrenceKey") > (older."ClaimedAt", older."OccurrenceKey"));
            """);

        migrationBuilder.DropIndex(
            name: "IX_AlertDeliveries_Receipt",
            table: "AlertDeliveries");

        migrationBuilder.CreateIndex(
            name: "IX_AlertDeliveries_Receipt",
            table: "AlertDeliveries",
            column: "Receipt",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AlertDeliveries_Receipt",
            table: "AlertDeliveries");

        migrationBuilder.CreateIndex(
            name: "IX_AlertDeliveries_Receipt",
            table: "AlertDeliveries",
            column: "Receipt");
    }
}
