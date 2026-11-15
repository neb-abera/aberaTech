using System.Net;
using System.Text.Json;
using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>What Google says about a booking's event.</summary>
public enum BookingEventState
{
    /// <summary>Google gave no usable answer. Nothing is decided from this.</summary>
    Unknown = 0,

    /// <summary>The event exists, at the times and with the reply carried here.</summary>
    Scheduled = 1,

    /// <summary>The host deleted the event.</summary>
    Deleted = 2
}

/// <summary>
/// A booking's event as Google holds it now: whether it exists, when it is,
/// and whether the visitor declined.
/// </summary>
/// <param name="Declined">The booking's own attendee answered no.</param>
/// <param name="Reason">Why the state is Unknown, for the log.</param>
public readonly record struct BookingEvent(
    BookingEventState State,
    Instant StartsAt,
    Instant EndsAt,
    bool Declined,
    string? Reason)
{
    public static BookingEvent Unknown(string reason) =>
        new(BookingEventState.Unknown, default, default, false, reason);

    public static BookingEvent Deleted { get; } =
        new(BookingEventState.Deleted, default, default, false, null);

    public static BookingEvent Scheduled(Instant startsAt, Instant endsAt, bool declined) =>
        new(BookingEventState.Scheduled, startsAt, endsAt, declined, null);

    /// <summary>
    /// Reads Google's answer to events.get.
    /// </summary>
    /// <remarks>
    /// Pure, so the reading is asserted without a network. The body is
    /// untrusted: anything this does not recognize is Unknown, and Unknown
    /// changes nothing.
    ///
    /// Only a 200 whose status is "cancelled" counts as deleted. Google's
    /// reference says get always returns a deleted event that way. A 404 is
    /// Unknown, because it is also what a reconnected, different calendar
    /// answers for every booking, and canceling them all would be wrong.
    /// </remarks>
    public static BookingEvent FromGoogle(HttpStatusCode status, string? body, string? attendeeEmail)
    {
        if ((int)status is < 200 or >= 300)
        {
            return Unknown($"Google answered {(int)status}.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Unknown("Google sent an empty event.");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Unknown("Google's event was not an object.");
            }

            if (Text(root, "status") == "cancelled")
            {
                return Deleted;
            }

            // An all-day event carries a date and no dateTime. A booking is
            // never all day, so the host has turned it into something else.
            if (!TryInstant(root, "start", out var startsAt) || !TryInstant(root, "end", out var endsAt))
            {
                return Unknown("Google's event has no start or end time.");
            }

            if (endsAt <= startsAt)
            {
                return Unknown("Google's event ends before it starts.");
            }

            return Scheduled(startsAt, endsAt, HasDeclined(root, attendeeEmail));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // ArgumentException is text that is not valid UTF-16, such as half
            // of a surrogate pair. The parser refuses to encode it.
            return Unknown("Google's event was not JSON.");
        }
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryInstant(JsonElement root, string name, out Instant instant)
    {
        instant = default;

        if (!root.TryGetProperty(name, out var when) || when.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // RFC 3339 with an offset, which is what Google sends for a timed
        // event. Without an offset the instant is not known, so it is refused.
        var text = Text(when, "dateTime");
        if (text is null
            || !DateTimeOffset.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed)
            || !(text.EndsWith('Z') || text.EndsWith('z') || HasOffset(text)))
        {
            return false;
        }

        instant = Instant.FromDateTimeOffset(parsed);
        return true;
    }

    /// <summary>Whether the text ends in a numeric offset such as -05:00.</summary>
    private static bool HasOffset(string text) =>
        text.Length >= 6
        && text[^6] is '+' or '-'
        && text[^3] == ':';

    /// <summary>
    /// Whether the booking's own attendee answered no.
    /// </summary>
    /// <remarks>
    /// Matched on the address the invite went to. Somebody else the host
    /// added to the event declining does not cancel the booking.
    /// </remarks>
    private static bool HasDeclined(JsonElement root, string? attendeeEmail)
    {
        if (string.IsNullOrWhiteSpace(attendeeEmail)
            || !root.TryGetProperty("attendees", out var attendees)
            || attendees.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var attendee in attendees.EnumerateArray())
        {
            if (attendee.ValueKind == JsonValueKind.Object
                && string.Equals(Text(attendee, "email"), attendeeEmail.Trim(), StringComparison.OrdinalIgnoreCase)
                && Text(attendee, "responseStatus") == "declined")
            {
                return true;
            }
        }

        return false;
    }
}
