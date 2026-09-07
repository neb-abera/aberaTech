using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class SustainedWeeklyHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SustainedWeeklyHours",
                table: "Settings",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SustainedWeeklyHours",
                table: "Settings");
        }
    }
}
