using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations;

/// <inheritdoc />
public partial class _20261121163528_AppAccess : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                Person = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Device = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                LastSeenAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AppSignInCodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CodeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                Challenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Person = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ExpiresAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppSignInCodes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppSessions_TokenHash",
            table: "AppSessions",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppSignInCodes_CodeHash",
            table: "AppSignInCodes",
            column: "CodeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppSignInCodes_ExpiresAt",
            table: "AppSignInCodes",
            column: "ExpiresAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppSessions");

        migrationBuilder.DropTable(
            name: "AppSignInCodes");
    }
}
