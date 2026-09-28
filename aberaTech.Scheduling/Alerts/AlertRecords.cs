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

/// <summary>
/// The owner's saved settings. One row, id 1, like the mute switch. No row
/// means the configuration's values (<see cref="AlertSettings.Defaults"/>).
/// </summary>
public class AlertSettingsRecord
{
    public const int SingleId = 1;

    public int Id { get; set; } = SingleId;

    public int Priority { get; set; }

    public int RepeatSeconds { get; set; }

    public int StopAfterMinutes { get; set; }

    /// <summary>Empty is the phone's own Pushover default.</summary>
    public string Sound { get; set; } = string.Empty;

    public int DefaultLeadMinutes { get; set; }

    public int PollMinutes { get; set; }

    public int LookaheadHours { get; set; }

    public bool IncludeAllDay { get; set; }

    /// <summary>Empty is UTC.</summary>
    public string TimeZone { get; set; } = string.Empty;

    public string[] OwnerEmails { get; set; } = [];

    /// <summary>0 or 1. A notification sounds once.</summary>
    public int NotificationPriority { get; set; }

    /// <summary>Empty is the phone's own Pushover default.</summary>
    public string NotificationSound { get; set; } = string.Empty;

    /// <summary>What an unmarked event with no choice sends: "none" or "notification".</summary>
    public string DefaultType { get; set; } = AlertTypes.None;

    /// <summary>How long after an alarm's time Pushover follows, so a paired phone rings first. 0 sends at once.</summary>
    public int BackupDelaySeconds { get; set; }

    public Instant UpdatedAt { get; set; }
}

/// <summary>
/// The owner's choice for one event on /alerts: none, notification or
/// alarm. Keyed by the event's UID, so it holds for every occurrence of a
/// repeating event. No row means the event follows its #critical mark and
/// the default.
/// </summary>
public class AlertEventTypeRecord
{
    /// <summary>The event's UID, or its hash when it is longer than a key: see <see cref="AlertPlanner"/>.</summary>
    public string EventId { get; set; } = string.Empty;

    public string Type { get; set; } = AlertTypes.None;

    public Instant UpdatedAt { get; set; }

    /// <summary>The last calendar read that had the event. A row unseen for 60 days is forgotten.</summary>
    public Instant LastSeenAt { get; set; }
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

    /// <summary>
    /// Pushover's receipt for an emergency message, which repeats until
    /// acknowledged. An acknowledgement on /alerts or a paired phone cancels
    /// the repeats with it. Null for any other priority.
    /// </summary>
    public string? Receipt { get; set; }
}

/// <summary>
/// A phone paired on /alerts. Its token is shown once, when it is made.
/// The server keeps the SHA-256 of the token and never the token.
/// </summary>
public class AlertDeviceRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte[] TokenHash { get; set; } = [];

    public Instant CreatedAt { get; set; }

    /// <summary>The last request the token made, written at most once a minute.</summary>
    public Instant? LastSeenAt { get; set; }
}

/// <summary>One occurrence the owner acknowledged, on a paired phone or in a browser. The first acknowledgement stands.</summary>
public class AlertAcknowledgementRecord
{
    public string OccurrenceKey { get; set; } = string.Empty;

    public Instant StartsAt { get; set; }

    public Instant AcknowledgedAt { get; set; }

    /// <summary>"phone" or "browser".</summary>
    public string Via { get; set; } = string.Empty;
}
