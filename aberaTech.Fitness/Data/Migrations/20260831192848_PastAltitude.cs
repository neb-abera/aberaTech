using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations
{
    /// <inheritdoc />
    public partial class PastAltitude : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PastAltitudeMeters",
                table: "Settings",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PastAltitudeMeters",
                table: "Settings");
        }
    }
}
