using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class LockedPredictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MadeOn = table.Column<LocalDate>(type: "date", nullable: false),
                    TargetDate = table.Column<LocalDate>(type: "date", nullable: false),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: false),
                    PredictedSeconds = table.Column<double>(type: "double precision", nullable: false),
                    PredictedFastSeconds = table.Column<double>(type: "double precision", nullable: false),
                    PredictedSlowSeconds = table.Column<double>(type: "double precision", nullable: false),
                    WeeklyHours = table.Column<double>(type: "double precision", nullable: false),
                    Compliance = table.Column<double>(type: "double precision", nullable: false),
                    RaceMassKg = table.Column<double>(type: "double precision", nullable: true),
                    ActualSeconds = table.Column<double>(type: "double precision", nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_TargetDate",
                table: "Predictions",
                column: "TargetDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Predictions");
        }
    }
}
