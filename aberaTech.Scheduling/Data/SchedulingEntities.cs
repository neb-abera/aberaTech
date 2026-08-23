using aberaTech.Scheduling.Domain;
using NodaTime;

namespace aberaTech.Scheduling.Data;

/// <summary>A stored availability rule. See <see cref="AvailabilityRule"/> for why it holds civil time.</summary>
public class AvailabilityRuleRecord
{
    public Guid Id { get; set; }

    public IsoDayOfWeek Day { get; set; }

    public LocalTime StartsAt { get; set; }

    public LocalTime EndsAt { get; set; }

    public string ZoneId { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public AvailabilityRule ToDomain() => new(Day, StartsAt, EndsAt, ZoneId);
}

/// <summary>A booked appointment.</summary>
public class Appointment
{
    public Guid Id { get; set; }

    /// <summary>Stored as timestamptz. The zone below is for display only.</summary>
    public Instant StartsAt { get; set; }

    public Instant EndsAt { get; set; }

    /// <summary>
    /// The zone the visitor booked in, so a confirmation can be phrased in the
    /// time they actually saw rather than the host's.
    /// </summary>
    public string BookedZoneId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PhoneE164 { get; set; } = string.Empty;

    public Instant CreatedAt { get; set; }

    public bool Cancelled { get; set; }
}

public class QueueSession
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Instant OpensAt { get; set; }

    public Instant ClosesAt { get; set; }

    /// <summary>How long the host expects each conversation to take by default.</summary>
    public Duration DefaultDuration { get; set; } = Duration.FromMinutes(15);

    public bool Open { get; set; }

    public List<QueueEntryRecord> Entries { get; set; } = [];
}

public class QueueEntryRecord
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public QueueSession? Session { get; set; }

    public int Position { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string PhoneE164 { get; set; } = string.Empty;

    /// <summary>
    /// The IANA zone the visitor's browser reported when they joined. Every
    /// message they receive is rendered in it, so they are never shown a time in
    /// the host's zone or, worse, the server's UTC.
    /// </summary>
    public string ZoneId { get; set; } = string.Empty;

    public Duration Expected { get; set; }

    public QueueEntryState State { get; set; } = QueueEntryState.Waiting;

    public Instant JoinedAt { get; set; }

    public Instant? StartedAt { get; set; }

    /// <summary>
    /// The projected start most recently put in front of this person, and the
    /// two milestone flags. Persisted rather than recomputed because the whole
    /// point is to compare against what they were actually told, which is a
    /// fact about the past that no amount of recalculation can recover.
    /// </summary>
    public Instant? LastAnnouncedStart { get; set; }

    public bool ImminentSent { get; set; }

    public bool TurnSent { get; set; }

    public QueueEntry ToDomain() => new(Id, Position, Expected, State, StartedAt);

    public NotificationState ToNotificationState() => new(LastAnnouncedStart, ImminentSent, TurnSent);
}
