using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace aberaTech.Scheduling.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    BookedZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PhoneE164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvailabilityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<LocalTime>(type: "time", nullable: false),
                    EndsAt = table.Column<LocalTime>(type: "time", nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QueueEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ToPhoneE164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Body = table.Column<string>(type: "character varying(1600)", maxLength: 1600, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OpensAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    ClosesAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    DefaultDuration = table.Column<Duration>(type: "interval", nullable: false),
                    Open = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PhoneE164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ZoneId = table.Column<string>(type: "text", nullable: false),
                    Expected = table.Column<Duration>(type: "interval", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    LastAnnouncedStart = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    ImminentSent = table.Column<bool>(type: "boolean", nullable: false),
                    TurnSent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueEntries_QueueSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "QueueSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StartsAt_EndsAt",
                table: "Appointments",
                columns: new[] { "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_IdempotencyKey",
                table: "Outbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_NextAttemptAt",
                table: "Outbox",
                column: "NextAttemptAt",
                filter: "\"NextAttemptAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ProviderMessageId",
                table: "Outbox",
                column: "ProviderMessageId",
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_SessionId_Position",
                table: "QueueEntries",
                columns: new[] { "SessionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_SessionId_State",
                table: "QueueEntries",
                columns: new[] { "SessionId", "State" });

            // Two appointments may not overlap. Ever.
            //
            // Enforced by the database rather than by application code, because
            // the application cannot win this race: "read the calendar, see the
            // slot is free, insert" is correct only until two people press book
            // at the same moment, and no amount of checking beforehand closes
            // that window. An exclusion constraint closes it, because Postgres
            // evaluates it while holding the row.
            //
            // The predicate excludes cancelled rows, so cancelling frees the
            // time immediately rather than leaving a tombstone blocking it.
            migrationBuilder.Sql("""
                ALTER TABLE "Appointments"
                ADD CONSTRAINT "appointments_do_not_overlap"
                EXCLUDE USING gist (tstzrange("StartsAt", "EndsAt") WITH &&)
                WHERE (NOT "Cancelled");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "Appointments" DROP CONSTRAINT IF EXISTS "appointments_do_not_overlap";""");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "AvailabilityRules");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "QueueEntries");

            migrationBuilder.DropTable(
                name: "QueueSessions");
        }
    }
}
