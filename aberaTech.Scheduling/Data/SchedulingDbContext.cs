using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using aberaTech.Scheduling.Alerts;
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

    public DbSet<SmsOptOut> SmsOptOuts => Set<SmsOptOut>();

    public DbSet<AdminSessionVersion> AdminSessions => Set<AdminSessionVersion>();

    /// <summary>The calendar alerts' mute switch, skips, send claims and settings. Alerts/AlertRecords.cs.</summary>
    public DbSet<AlertMuteRecord> AlertMutes => Set<AlertMuteRecord>();

    public DbSet<AlertSkipRecord> AlertSkips => Set<AlertSkipRecord>();

    public DbSet<AlertDeliveryRecord> AlertDeliveries => Set<AlertDeliveryRecord>();

    public DbSet<AlertSettingsRecord> AlertSettings => Set<AlertSettingsRecord>();

    public DbSet<AlertEventTypeRecord> AlertEventTypes => Set<AlertEventTypeRecord>();

    /// <summary>The phones paired on /alerts, by the hash of their tokens.</summary>
    public DbSet<AlertDeviceRecord> AlertDevices => Set<AlertDeviceRecord>();

    public DbSet<AlertAcknowledgementRecord> AlertAcknowledgements => Set<AlertAcknowledgementRecord>();

    public DbSet<AlertPushStateRecord> AlertPushStates => Set<AlertPushStateRecord>();

    /// <summary>Events created on /alerts, until Google's iCal feed carries them.</summary>
    public DbSet<AlertCreatedEventRecord> AlertCreatedEvents => Set<AlertCreatedEventRecord>();

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
            entity.Property(appointment => appointment.Email).HasMaxLength(254);
            entity.Property(appointment => appointment.GoogleEventId).HasMaxLength(1024);

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

        builder.Entity<SmsOptOut>(entity =>
        {
            entity.HasKey(optOut => optOut.Id);
            entity.Property(optOut => optOut.PhoneE164).HasMaxLength(16).IsRequired();
            entity.Property(optOut => optOut.Reason).HasMaxLength(200).IsRequired();

            // One row per number. Recording the same opt-out twice would make
            // "is this number suppressed" a question about row counts.
            entity.HasIndex(optOut => optOut.PhoneE164).IsUnique();
        });

        builder.Entity<AdminSessionVersion>(entity =>
        {
            // The key is the lookup: one indexed read per signed-in request.
            entity.HasKey(session => session.Email);
            entity.Property(session => session.Email).HasMaxLength(320);
        });

        builder.Entity<HostCalendarCredential>(entity =>
        {
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.CalendarId).HasMaxLength(320).IsRequired();
            entity.Property(credential => credential.ConnectedEmail).HasMaxLength(320).IsRequired();
            entity.Property(credential => credential.GrantedScopes).HasMaxLength(2048).IsRequired();

            // No length cap on the protected token: the ciphertext is longer
            // than the token and grows if the protection payload format ever
            // changes, and a truncating column would corrupt it silently.
            entity.Property(credential => credential.ProtectedRefreshToken).IsRequired();
        });

        builder.Entity<AlertMuteRecord>(entity =>
        {
            entity.HasKey(mute => mute.Id);
            entity.Property(mute => mute.Id).ValueGeneratedNever();
        });

        builder.Entity<AlertSettingsRecord>(entity =>
        {
            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.Id).ValueGeneratedNever();
            entity.Property(settings => settings.Sound).HasMaxLength(32).IsRequired();
            entity.Property(settings => settings.TimeZone).HasMaxLength(Alerts.AlertSettings.MaxTimeZoneLength).IsRequired();
            entity.Property(settings => settings.OwnerEmails).IsRequired();
            entity.Property(settings => settings.NotificationSound).HasMaxLength(32).IsRequired();
            // A row saved before the column existed sends nothing for an
            // unmarked event: the migration fills it with "none".
            entity.Property(settings => settings.DefaultType).HasMaxLength(AlertTypes.MaxLength).IsRequired()
                .HasDefaultValue(AlertTypes.None);
        });

        builder.Entity<AlertEventTypeRecord>(entity =>
        {
            entity.HasKey(choice => choice.EventId);
            entity.Property(choice => choice.EventId).HasMaxLength(AlertPlanner.MaxKeyLength);
            entity.Property(choice => choice.Type).HasMaxLength(AlertTypes.MaxLength).IsRequired();
            // The prune deletes by it.
            entity.HasIndex(choice => choice.LastSeenAt);
        });

        builder.Entity<AlertDeviceRecord>(entity =>
        {
            entity.HasKey(device => device.Id);
            entity.Property(device => device.Id).ValueGeneratedNever();
            entity.Property(device => device.Name).HasMaxLength(AlertDeviceTokens.MaxNameLength).IsRequired();
            entity.Property(device => device.TokenHash).IsRequired();
            // Every request with a token looks it up by its hash.
            entity.HasIndex(device => device.TokenHash).IsUnique();
            entity.Property(device => device.ApnsToken).HasMaxLength(ApnsPushTokens.MaxLength);
            entity.Property(device => device.ApnsEnvironment).HasMaxLength(ApnsPushTokens.MaxEnvironmentLength);
        });

        builder.Entity<AlertPushStateRecord>(entity =>
        {
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).ValueGeneratedNever();
        });

        builder.Entity<AlertAcknowledgementRecord>(entity =>
        {
            entity.HasKey(acknowledgement => acknowledgement.OccurrenceKey);
            entity.Property(acknowledgement => acknowledgement.OccurrenceKey).HasMaxLength(AlertPlanner.MaxKeyLength);
            entity.Property(acknowledgement => acknowledgement.Via).HasMaxLength(16).IsRequired();
        });

        builder.Entity<AlertCreatedEventRecord>(entity =>
        {
            entity.HasKey(created => created.EventId);
            entity.Property(created => created.EventId).HasMaxLength(AlertPlanner.MaxKeyLength);
            entity.Property(created => created.Title).HasMaxLength(CreatedEvents.MaxTitleLength).IsRequired();
            entity.Property(created => created.Location).HasMaxLength(CreatedEvents.MaxLocationLength);
        });

        builder.Entity<AlertSkipRecord>(entity =>
        {
            entity.HasKey(skip => skip.OccurrenceKey);
            entity.Property(skip => skip.OccurrenceKey).HasMaxLength(AlertPlanner.MaxKeyLength);
        });

        builder.Entity<AlertDeliveryRecord>(entity =>
        {
            // The key is the dedupe: one row per occurrence, ever.
            entity.HasKey(delivery => delivery.OccurrenceKey);
            entity.Property(delivery => delivery.OccurrenceKey).HasMaxLength(AlertPlanner.MaxKeyLength);
            entity.Property(delivery => delivery.Outcome).HasMaxLength(64).IsRequired();
            entity.Property(delivery => delivery.Receipt).HasMaxLength(PushoverClient.MaxReceiptLength);
            entity.HasIndex(delivery => delivery.ClaimedAt);
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
