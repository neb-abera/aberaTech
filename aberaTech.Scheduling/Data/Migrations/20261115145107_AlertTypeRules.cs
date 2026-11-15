using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261115145107_AlertTypeRules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertTypeRules",
            columns: table => new
            {
                Position = table.Column<int>(type: "integer", nullable: false),
                Field = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertTypeRules", x => x.Position);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertTypeRules");
    }
}
