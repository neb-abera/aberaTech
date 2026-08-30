using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFitness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Sport = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    AverageHr = table.Column<int>(type: "integer", nullable: true),
                    MaxHr = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BodyMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<LocalDate>(type: "date", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    BodyFatPercent = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetValue = table.Column<double>(type: "double precision", nullable: false),
                    TargetDate = table.Column<LocalDate>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceHr = table.Column<int>(type: "integer", nullable: false),
                    LtSecondsPerKm = table.Column<double>(type: "double precision", nullable: true),
                    PlanMinutesPerWeek = table.Column<double>(type: "double precision", nullable: false),
                    StartVdot = table.Column<double>(type: "double precision", nullable: false),
                    VdotMeasuredOn = table.Column<LocalDate>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrengthSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Exercise = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SetIndex = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    Reps = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrengthSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrengthSets_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Source_ExternalId",
                table: "Activities",
                columns: new[] { "Source", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_StartedAt",
                table: "Activities",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BodyMetrics_Date",
                table: "BodyMetrics",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_Metric",
                table: "Goals",
                column: "Metric",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrengthSets_ActivityId",
                table: "StrengthSets",
                column: "ActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BodyMetrics");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "StrengthSets");

            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
