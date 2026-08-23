using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using aberaTech.Scheduling.Outbox;

namespace aberaTech.Scheduling.Data;

public class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>
    /// Data protection keys, kept in the database rather than on the container's
    /// filesystem.
    /// </summary>
    /// <remarks>
    /// A container app revision has no durable disk, so the default key ring is
    /// regenerated on every restart — and anything encrypted with the old keys,
    /// which here means the host's Google refresh token, becomes permanently
    /// unreadable. Persisting the keys alongside the data they protect is what
    /// makes "connect your calendar once" true rather than "reconnect after
    /// every deploy".
    /// </remarks>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<AvailabilityRuleRecord> AvailabilityRules => Set<AvailabilityRuleRecord>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<QueueSession> QueueSessions => Set<QueueSession>();

    public DbSet<QueueEntryRecord> QueueEntries => Set<QueueEntryRecord>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<HostCalendarCredential> HostCalendarCredentials => Set<HostCalendarCredential>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("btree_gist");

        builder.Entity<AvailabilityRuleRecord>(entity =>
        {
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.ZoneId).HasMaxLength(64).IsRequired();
        });

        builder.Entity<Appointment>(entity =>
        {
            entity.HasKey(appointment => appointment.Id);
            entity.Property(appointment => appointment.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(appointment => appointment.PhoneE164).HasMaxLength(16).IsRequired();
            entity.Property(appointment => appointment.BookedZoneId).HasMaxLength(64).IsRequired();

            // Reading the agenda is always "what is on between these two
            // instants", so the index matches the query rather than the key.
            entity.HasIndex(appointment => new { appointment.StartsAt, appointment.EndsAt });
        });

        builder.Entity<QueueSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Name).HasMaxLength(120).IsRequired();
            entity
                .HasMany(session => session.Entries)
                .WithOne(record => record.Session!)
                .HasForeignKey(record => record.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QueueEntryRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(record => record.PhoneE164).HasMaxLength(16).IsRequired();

            // Two people cannot hold the same place in the same queue. Enforced
            // here rather than by reading the current maximum and adding one,
            // which races the moment two people press join together.
            entity
                .HasIndex(record => new { record.SessionId, record.Position })
                .IsUnique();

            // The dispatcher and the queue view both filter on state within a
            // session, and a busy afternoon reads this far more than it writes.
            entity.HasIndex(record => new { record.SessionId, record.State });
        });

        builder.Entity<HostCalendarCredential>(entity =>
        {
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.CalendarId).HasMaxLength(320).IsRequired();
            entity.Property(credential => credential.ConnectedEmail).HasMaxLength(320).IsRequired();

            // No length cap on the protected token: the ciphertext is longer
            // than the token and grows if the protection payload format ever
            // changes, and a truncating column would corrupt it silently.
            entity.Property(credential => credential.ProtectedRefreshToken).IsRequired();
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.ToPhoneE164).HasMaxLength(16).IsRequired();
            entity.Property(message => message.Body).HasMaxLength(1600).IsRequired();
            entity.Property(message => message.IdempotencyKey).HasMaxLength(64).IsRequired();

            // A delivery receipt arrives knowing only the provider's id, so the
            // lookup that matches it back to a row has to be indexed. Unique
            // because two of our messages sharing a provider id would mean we
            // had attributed a receipt to the wrong person.
            entity
                .HasIndex(message => message.ProviderMessageId)
                .IsUnique()
                .HasFilter("\"ProviderMessageId\" IS NOT NULL");

            // The claim query the dispatcher runs on every tick: due messages in
            // a non-terminal state, oldest first. Partial, because delivered and
            // dead-lettered rows accumulate forever and are never claimed.
            entity
                .HasIndex(message => message.NextAttemptAt)
                .HasFilter("\"NextAttemptAt\" IS NOT NULL");

            // Nothing may be sent twice, whatever the retry loop decides.
            entity
                .HasIndex(message => message.IdempotencyKey)
                .IsUnique();
        });
    }
}
