using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The mute switch. One row, id 1: a second row would make "are alerts
/// muted" a question about which row to believe.
/// </summary>
public class AlertMuteRecord
{
    public const int SingleId = 1;

    public int Id { get; set; } = SingleId;

    /// <summary>Null, or a time already past, is not muted.</summary>
    public Instant? MutedUntil { get; set; }

    public Instant UpdatedAt { get; set; }
}

/// <summary>One occurrence the owner pressed Skip on.</summary>
public class AlertSkipRecord
{
    /// <summary>The event's UID and the occurrence's start: see <see cref="AlertPlanner"/>.</summary>
    public string OccurrenceKey { get; set; } = string.Empty;

    public Instant StartsAt { get; set; }

    public Instant CreatedAt { get; set; }
}

/// <summary>
/// The claim on one occurrence's alert. The key is the primary key, so a
/// second claim, from a restart or from another replica, is refused by the
/// database rather than by a read that races.
/// </summary>
public class AlertDeliveryRecord
{
    public string OccurrenceKey { get; set; } = string.Empty;

    public Instant StartsAt { get; set; }

    public Instant ClaimedAt { get; set; }

    /// <summary>"claimed" until Pushover answers, then "sent" or "failed: …".</summary>
    public string Outcome { get; set; } = "claimed";

    public Instant? CompletedAt { get; set; }
}
