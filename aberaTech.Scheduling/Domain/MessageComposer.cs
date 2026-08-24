using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Domain;

/// <summary>
/// Writes the text of each notification, in the recipient's own time zone.
/// </summary>
/// <remarks>
/// Every time in every message is rendered from an instant into the zone the
/// visitor booked in. That is the whole of the timezone fix as far as a recipient
/// can see: they are never shown a time in somebody else's zone, and never shown
/// a bare number that could be either.
///
/// The zone abbreviation is included deliberately. "3:40 PM" from a scheduler is
/// ambiguous to anybody who has ever been burned by one; "3:40 PM CDT" is not.
/// </remarks>
public static class MessageComposer
{
    private static readonly ZonedDateTimePattern TimePattern =
        ZonedDateTimePattern.CreateWithInvariantCulture("h:mm tt x", DateTimeZoneProviders.Tzdb);

    /// <summary>
    /// The longest body this composer will produce. One GSM-7 segment is 160
    /// characters, and a message that spills into a second segment costs twice
    /// as much and counts twice against the daily carrier cap.
    /// </summary>
    public const int SingleSegment = 160;

    /// <param name="brandName">
    /// Prefixed to messages sent to a visitor. Carriers require the registered
    /// brand to appear in the message, and a text from an unknown number is
    /// indistinguishable from spam without it.
    /// </param>
    public static string Compose(
        NotificationKind kind,
        string hostName,
        Instant projectedStart,
        DateTimeZone zone,
        string? brandName = null)
    {
        var at = TimePattern.Format(projectedStart.InZone(zone));

        var body = kind switch
        {
            NotificationKind.Joined =>
                $"You're in the queue for {hostName}, around {at}. Reply STOP to opt out.",
            NotificationKind.TimeChanged =>
                $"Your estimated start with {hostName} moved to {at}.",
            NotificationKind.Imminent =>
                $"You're up soon with {hostName}, around {at}. Please make your way over.",
            NotificationKind.YourTurn =>
                $"You're up now with {hostName}.",
            // The first message a visitor receives carries the opt-out, which is
            // both the convention and what keeps a sending number in good
            // standing. Later messages in the same thread do not repeat it.
            NotificationKind.Booked =>
                $"Booked with {hostName} for {at}. Reply STOP to opt out.",
            NotificationKind.ReminderDayBefore =>
                $"Tomorrow: you're with {hostName} at {at}.",
            NotificationKind.Reminder =>
                $"Reminder: you're with {hostName} at {at}.",
            NotificationKind.Cancelled =>
                $"Your {at} with {hostName} has been cancelled.",
            NotificationKind.HostBooked =>
                $"New booking: {at}.",
            NotificationKind.HostCancelled =>
                $"Cancelled: {at}.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown notification kind.")
        };

        // The host does not need telling which brand is texting him about his own
        // calendar, and the characters are better spent elsewhere.
        var toHost = kind is NotificationKind.HostBooked or NotificationKind.HostCancelled;

        return string.IsNullOrWhiteSpace(brandName) || toHost ? body : $"{brandName}: {body}";
    }
}
