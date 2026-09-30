using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20260930150957_AlertRoutines : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertRoutines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Hour = table.Column<short>(type: "smallint", nullable: false),
                Minute = table.Column<short>(type: "smallint", nullable: false),
                Days = table.Column<short[]>(type: "smallint[]", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                SnoozeMinutes = table.Column<short>(type: "smallint", nullable: false),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertRoutines", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertRoutines");
    }
}
