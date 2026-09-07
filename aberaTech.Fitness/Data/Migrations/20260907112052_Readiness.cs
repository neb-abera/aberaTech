using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class Readiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "StrengthSets",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<LocalDate>(
                name: "SelectionDate",
                table: "Settings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LoadKg",
                table: "Activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AftResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<LocalDate>(type: "date", nullable: false),
                    DeadliftKg = table.Column<double>(type: "double precision", nullable: false),
                    HandReleasePushUps = table.Column<int>(type: "integer", nullable: false),
                    SprintDragCarrySeconds = table.Column<double>(type: "double precision", nullable: false),
                    PlankSeconds = table.Column<double>(type: "double precision", nullable: false),
                    TwoMileSeconds = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AftResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AftResults_Date",
                table: "AftResults",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AftResults");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "StrengthSets");

            migrationBuilder.DropColumn(
                name: "SelectionDate",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LoadKg",
                table: "Activities");
        }
    }
}
