using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261104101459_FbiResults : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FbiResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<LocalDate>(type: "date", nullable: false),
                PullUps = table.Column<int>(type: "integer", nullable: true),
                Sprint300mSeconds = table.Column<double>(type: "double precision", nullable: true),
                PushUps = table.Column<int>(type: "integer", nullable: true),
                Run1_5MileSeconds = table.Column<double>(type: "double precision", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FbiResults", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FbiResults_Date",
            table: "FbiResults",
            column: "Date",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FbiResults");
    }
}
