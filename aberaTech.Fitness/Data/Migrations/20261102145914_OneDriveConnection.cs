using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Fitness.Data.Migrations;

/// <inheritdoc />
public partial class _20261102145914_OneDriveConnection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OneDriveConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                ProtectedRefreshToken = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                ConnectedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OneDriveConnections", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OneDriveConnections");
    }
}
