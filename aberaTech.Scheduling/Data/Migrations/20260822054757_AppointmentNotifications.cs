using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "Outbox",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Outbox");
        }
    }
}
