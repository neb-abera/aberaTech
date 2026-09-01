using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class PeakAndGoalWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GoalWeightKg",
                table: "Settings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PastPeakWeightKg",
                table: "Settings",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoalWeightKg",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PastPeakWeightKg",
                table: "Settings");
        }
    }
}
