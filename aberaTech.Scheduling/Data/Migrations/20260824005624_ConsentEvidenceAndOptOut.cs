using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsentEvidenceAndOptOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsentDisclosure",
                table: "QueueEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "ConsentedAt",
                table: "QueueEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentDisclosure",
                table: "Appointments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "ConsentedAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SmsOptOuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneE164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OptedOutAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsOptOuts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsOptOuts_PhoneE164",
                table: "SmsOptOuts",
                column: "PhoneE164",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsOptOuts");

            migrationBuilder.DropColumn(
                name: "ConsentDisclosure",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "ConsentedAt",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "ConsentDisclosure",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ConsentedAt",
                table: "Appointments");
        }
    }
}
