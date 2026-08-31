using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class GoalDistancesAndGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AvailableHoursPerWeek",
                table: "Settings",
                type: "double precision",
                nullable: false,
                // The entity's own default, so a row written before this
                // column existed does not read back as "no time to train".
                defaultValue: 7.0);

            migrationBuilder.AddColumn<bool>(
                name: "Female",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DistanceMeters",
                table: "Goals",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Goals",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableHoursPerWeek",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Female",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DistanceMeters",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "Goals");
        }
    }
}
