using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminSessionVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSessions",
                columns: table => new
                {
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessions", x => x.Email);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSessions");
        }
    }
}
