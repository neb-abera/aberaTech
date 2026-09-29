using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>What the phone shows: the event's title, then when it starts and where.</summary>
public static class AlertText
{
    /// <summary>"9:00 AM EDT, Wed 28 Oct". The abbreviation says which side of a clock change it is.</summary>
    private static readonly ZonedDateTimePattern Start =
        ZonedDateTimePattern.CreateWithInvariantCulture("h:mm tt x, ddd d MMM", null);

    public static string When(Instant instant, DateTimeZone zone) => Start.Format(instant.InZone(zone));

    public static string Title(string title) => Cut(title, PushoverClient.MaxTitle);

    /// <summary>"Test: Standup". The phone shows at a glance that no event is starting.</summary>
    public static string TestTitle(string title) => $"Test: {title}";

    public static string Message(PlannedAlert alert, DateTimeZone zone) =>
        Cut(
            alert.Location is null
                ? $"Starts {When(alert.StartsAt, zone)}"
                : $"Starts {When(alert.StartsAt, zone)}\n{alert.Location}",
            PushoverClient.MaxMessage);

    /// <summary>"Test alert" goes as an alarm, "Test notification" as a notification.</summary>
    public static string TestName(string type) => type == AlertTypes.Alarm ? "Test alert" : "Test notification";

    /// <summary>When it was sent, which kind it is, and what it does on the phone.</summary>
    public static string TestMessage(Instant now, DateTimeZone zone, string type, PushoverDelivery delivery) =>
        $"Sent from abera.tech at {When(now, zone)}, as {(type == AlertTypes.Alarm ? "an alarm" : "a notification")}. "
        + delivery switch
        {
            { Priority: PushoverClient.EmergencyPriority, RetrySeconds: { } retry } =>
                $"It rings every {Every(retry)} until you acknowledge it.",
            { Priority: 1 } => "One sound, even during Pushover's quiet hours.",
            _ => "One sound, follows the phone's settings."
        };

    /// <summary>"minute", "2 minutes", "45 seconds": the gap between two sounds.</summary>
    public static string Every(int seconds) => seconds switch
    {
        60 => "minute",
        _ when seconds % 60 == 0 => $"{seconds / 60} minutes",
        _ => $"{seconds} seconds"
    };

    private static string Cut(string text, int limit) => text.Length <= limit ? text : text[..limit];
}

/// <summary>When the two mute buttons end.</summary>
public static class AlertMute
{
    public static readonly LocalTime Morning = new(6, 0);

    public static Instant ForAnHour(Instant now) => now + Duration.FromHours(1);

    /// <summary>
    /// The next 06:00 in the zone. Pressed in the evening that is tomorrow
    /// morning. Pressed after midnight it is the same date, which is still
    /// the morning the owner means.
    /// </summary>
    public static Instant UntilMorning(Instant now, DateTimeZone zone)
    {
        var local = now.InZone(zone).LocalDateTime;
        var date = local.TimeOfDay < Morning ? local.Date : local.Date.PlusDays(1);
        return zone.AtLeniently(date.At(Morning)).ToInstant();
    }
}
