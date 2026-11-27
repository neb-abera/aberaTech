using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261127031858_CloudRoutineFires : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CloudRoutineFires",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                Zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ClaimedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CompletedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CloudRoutineFires", x => x.Key);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CloudRoutineFires_ClaimedAt",
            table: "CloudRoutineFires",
            column: "ClaimedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CloudRoutineFires");
    }
}
