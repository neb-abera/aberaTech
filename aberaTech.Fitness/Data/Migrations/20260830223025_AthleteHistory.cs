using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class AthleteHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                table: "Settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeAltitudeMeters",
                table: "Settings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PastPeakDistanceMeters",
                table: "Settings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PastPeakSeconds",
                table: "Settings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PastPeakYear",
                table: "Settings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthYear",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "HomeAltitudeMeters",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PastPeakDistanceMeters",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PastPeakSeconds",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PastPeakYear",
                table: "Settings");
        }
    }
}
