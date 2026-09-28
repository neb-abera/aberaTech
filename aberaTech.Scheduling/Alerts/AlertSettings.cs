using System.Net.Mail;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The settings the alerts run on: the configuration's values, replaced by
/// the owner's saved row once there is one. The worker reads them at the
/// start of every pass and the send path at every send, so a save takes
/// effect without a deploy, on every replica.
/// </summary>
/// <remarks>
/// Priority, RepeatSeconds, StopAfterMinutes and Sound are the alarm's:
/// an event marked #critical, or set to Alarm on the page. The three
/// Notification fields are for an event set to Notification, and
/// DefaultType says what an event with neither sends.
///
/// The three secrets are not here. They stay container secrets: typed into
/// a web form they would pass through the browser and the database.
/// </remarks>
public sealed record AlertSettings(
    int Priority,
    int RepeatSeconds,
    int StopAfterMinutes,
    string Sound,
    int DefaultLeadMinutes,
    int PollMinutes,
    int LookaheadHours,
    bool IncludeAllDay,
    string TimeZone,
    IReadOnlyList<string> OwnerEmails,
    int NotificationPriority = 0,
    string NotificationSound = "",
    string DefaultType = AlertTypes.None,
    int BackupDelaySeconds = 0)
{
    public const int MinPriority = 0;
    public const int MaxPriority = PushoverClient.EmergencyPriority;
    public const int MinRepeatSeconds = PushoverClient.MinRetrySeconds;
    public const int MaxRepeatSeconds = PushoverClient.MaxExpireSeconds;
    public const int MinStopAfterMinutes = 1;
    public const int MaxStopAfterMinutes = PushoverClient.MaxExpireSeconds / 60;
    public const int MinDefaultLeadMinutes = 0;
    public const int MaxDefaultLeadMinutes = 24 * 60;
    public const int MinPollMinutes = 1;
    public const int MaxPollMinutes = 60;
    public const int MinLookaheadHours = 1;
    public const int MaxLookaheadHours = 24 * 14;
    public const int MaxOwnerEmails = 10;
    public const int MaxEmailLength = 254;
    public const int MaxTimeZoneLength = 64;
    public const int MaxNotificationPriority = 1;
    public const int MinBackupDelaySeconds = 0;
    public const int MaxBackupDelaySeconds = 900;

    /// <summary>A backup that would land after this point before the start goes at this point instead.</summary>
    public static readonly Duration LatestBackupBeforeStart = Duration.FromMinutes(1);

    /// <summary>The configuration's values, pulled inside the bounds. What runs until the owner saves.</summary>
    public static AlertSettings Defaults(AlertsOptions options) => new(
        Math.Clamp(options.Priority, MinPriority, MaxPriority),
        Math.Clamp(options.RepeatSeconds, MinRepeatSeconds, MaxRepeatSeconds),
        Math.Clamp(options.StopAfterMinutes, MinStopAfterMinutes, MaxStopAfterMinutes),
        PushoverClient.Sounds.Contains(options.Sound ?? "") ? options.Sound! : "",
        Math.Clamp(options.DefaultLeadMinutes, MinDefaultLeadMinutes, MaxDefaultLeadMinutes),
        Math.Clamp(options.PollMinutes, MinPollMinutes, MaxPollMinutes),
        Math.Clamp(options.LookaheadHours, MinLookaheadHours, MaxLookaheadHours),
        options.IncludeAllDay,
        options.TimeZone?.Trim() ?? "",
        [.. options.OwnerEmails.Select(email => email.Trim()).Where(email => email.Length > 0)],
        Math.Clamp(options.NotificationPriority, MinPriority, MaxNotificationPriority),
        PushoverClient.Sounds.Contains(options.NotificationSound ?? "") ? options.NotificationSound! : "",
        AlertTypes.Defaults.Contains(options.DefaultType ?? "") ? options.DefaultType! : AlertTypes.None,
        Math.Clamp(options.BackupDelaySeconds, MinBackupDelaySeconds, MaxBackupDelaySeconds));

    /// <summary>The saved row, or the configuration's values when nothing is saved.</summary>
    public static async Task<AlertSettings> CurrentAsync(
        IAlertStore store, AlertsOptions options, CancellationToken cancellationToken) =>
        await store.SettingsAsync(cancellationToken) ?? Defaults(options);

    public Duration Poll => Duration.FromMinutes(PollMinutes);

    public Duration DefaultLead => Duration.FromMinutes(DefaultLeadMinutes);

    public Duration Lookahead => Duration.FromHours(LookaheadHours);

    /// <summary>
    /// When an unacknowledged emergency message stops: the owner's limit or
    /// Pushover's 50 sounds, whichever comes first.
    /// </summary>
    public int EffectiveStopSeconds => Math.Min(StopAfterMinutes * 60, PushoverClient.MaxEmergencySounds * RepeatSeconds);

    /// <summary>What an alarm asks Pushover for. Retry and expire go only with priority 2.</summary>
    public PushoverDelivery AlarmDelivery => new(
        Priority,
        Priority == PushoverClient.EmergencyPriority ? RepeatSeconds : null,
        Priority == PushoverClient.EmergencyPriority ? StopAfterMinutes * 60 : null,
        Sound.Length == 0 ? null : Sound);

    /// <summary>What a notification asks Pushover for: one sound, never a retry or an expiry.</summary>
    public PushoverDelivery NotificationDelivery => new(
        NotificationPriority,
        null,
        null,
        NotificationSound.Length == 0 ? null : NotificationSound);

    /// <summary>How an alert of this type is sent. Null for <see cref="AlertTypes.None"/>: nothing goes.</summary>
    public PushoverDelivery? DeliveryFor(string type) => type switch
    {
        AlertTypes.Alarm => AlarmDelivery,
        AlertTypes.Notification => NotificationDelivery,
        _ => null
    };

    /// <summary>
    /// When the Pushover message for an alert goes. An alarm waits
    /// <see cref="BackupDelaySeconds"/> after its time, so a paired phone
    /// rings first and Pushover follows only if nobody acknowledged it. The
    /// wait never runs past one minute before the start: the plan drops an
    /// event once it starts, and a backup after that would never go. A
    /// notification, or a delay of 0, goes at the alert's own time.
    /// </summary>
    public Instant PushoverAt(PlannedAlert alert, string type)
    {
        if (type != AlertTypes.Alarm || BackupDelaySeconds <= 0) return alert.AlertAt;

        var backup = alert.AlertAt + Duration.FromSeconds(BackupDelaySeconds);
        var latest = alert.StartsAt - LatestBackupBeforeStart;
        if (backup <= latest) return backup;
        return latest > alert.AlertAt ? latest : alert.AlertAt;
    }

    /// <summary>The zone when the feed names none. Blank is UTC. A saved name was checked on save.</summary>
    public DateTimeZone FallbackZone() =>
        TimeZone.Length == 0
            ? DateTimeZone.Utc
            : DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone)
              ?? throw new InvalidOperationException($"Alerts:TimeZone '{TimeZone}' is not a time zone database name.");

    /// <summary>What the calendar read depends on. A change means the plan in memory is stale.</summary>
    public string PlanKey =>
        string.Join('|', DefaultLeadMinutes, LookaheadHours, IncludeAllDay, TimeZone, string.Join(',', OwnerEmails));

    /// <summary>
    /// The owner's form, checked field by field. Empty when every field is
    /// inside its bounds. Keys are the JSON field names.
    /// </summary>
    public static Dictionary<string, string[]> Validate(
        int? priority,
        int? repeatSeconds,
        int? stopAfterMinutes,
        string? sound,
        int? defaultLeadMinutes,
        int? pollMinutes,
        int? lookaheadHours,
        bool? includeAllDay,
        string? timeZone,
        IReadOnlyList<string?>? ownerEmails,
        int? notificationPriority,
        string? notificationSound,
        string? defaultType,
        int? backupDelaySeconds)
    {
        var errors = new Dictionary<string, string[]>();

        void Range(string field, int? value, int min, int max, string unit)
        {
            if (value is null) errors[field] = ["Required."];
            else if (value < min || value > max) errors[field] = [$"Between {min} and {max} {unit}."];
        }

        if (priority is null) errors["priority"] = ["Required."];
        else if (priority is < MinPriority or > MaxPriority) errors["priority"] = ["0, 1 or 2."];

        Range("repeatSeconds", repeatSeconds, MinRepeatSeconds, MaxRepeatSeconds, "seconds");
        Range("stopAfterMinutes", stopAfterMinutes, MinStopAfterMinutes, MaxStopAfterMinutes, "minutes");
        Range("defaultLeadMinutes", defaultLeadMinutes, MinDefaultLeadMinutes, MaxDefaultLeadMinutes, "minutes");
        Range("pollMinutes", pollMinutes, MinPollMinutes, MaxPollMinutes, "minutes");
        Range("lookaheadHours", lookaheadHours, MinLookaheadHours, MaxLookaheadHours, "hours");
        Range("backupDelaySeconds", backupDelaySeconds, MinBackupDelaySeconds, MaxBackupDelaySeconds, "seconds");

        if (sound is null) errors["sound"] = ["Required."];
        else if (sound.Length > 0 && !PushoverClient.Sounds.Contains(sound)) errors["sound"] = ["Not a Pushover sound."];

        if (includeAllDay is null) errors["includeAllDay"] = ["Required."];

        if (notificationPriority is null) errors["notificationPriority"] = ["Required."];
        else if (notificationPriority is < MinPriority or > MaxNotificationPriority) errors["notificationPriority"] = ["0 or 1."];

        if (notificationSound is null) errors["notificationSound"] = ["Required."];
        else if (notificationSound.Length > 0 && !PushoverClient.Sounds.Contains(notificationSound))
        {
            errors["notificationSound"] = ["Not a Pushover sound."];
        }

        if (defaultType is null) errors["defaultType"] = ["Required."];
        else if (!AlertTypes.Defaults.Contains(defaultType)) errors["defaultType"] = ["\"none\" or \"notification\"."];

        if (timeZone is null) errors["timeZone"] = ["Required."];
        else if (timeZone.Trim() is { Length: > 0 } zone
                 && (zone.Length > MaxTimeZoneLength || DateTimeZoneProviders.Tzdb.GetZoneOrNull(zone) is null))
        {
            errors["timeZone"] = ["Not a time zone database name, such as America/New_York."];
        }

        if (ownerEmails is null) errors["ownerEmails"] = ["Required."];
        else if (ownerEmails.Count > MaxOwnerEmails) errors["ownerEmails"] = [$"At most {MaxOwnerEmails} addresses."];
        else if (ownerEmails.FirstOrDefault(email => !IsAddress(email)) is { } bad)
        {
            errors["ownerEmails"] = [$"Not an email address: {Shorten(bad ?? "")}"];
        }

        return errors;
    }

    private static bool IsAddress(string? value)
    {
        var text = value?.Trim() ?? "";
        return text.Length is > 0 and <= MaxEmailLength
               && MailAddress.TryCreate(text, out var address)
               && address.Address == text;
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
