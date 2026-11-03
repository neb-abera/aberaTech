using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A countdown: a label, the instant it counts to, and the zone the owner
/// set it in. The page and the paired phone show the time left, or the
/// time since once it has passed. Each has an event at its target on the
/// Google Calendar the alerts read. With Alarm on, that event is an alarm:
/// it rings on the phone and through Pushover until it is stopped.
/// </summary>
/// <param name="TimeZone">An IANA zone id. The target's date and time are written in it.</param>
/// <param name="Alarm">The calendar event rings as an alarm at the target.</param>
/// <param name="CalendarEventId">The event's iCalUID on Google Calendar. Null for a countdown saved before countdowns made events.</param>
public sealed record AlertCountdown(
    Guid Id,
    string Label,
    Instant TargetAt,
    string TimeZone,
    Instant UpdatedAt,
    bool Alarm = false,
    string? CalendarEventId = null);

/// <summary>The bounds of a countdown, and the check a create or an update passes before it is stored.</summary>
public static class AlertCountdowns
{
    /// <summary>At most this many. A create beyond it is refused.</summary>
    public const int MaxCountdowns = 50;

    public const int MaxLabelLength = 60;

    /// <summary>What an empty, blank or missing label is stored as.</summary>
    public const string DefaultLabel = "Countdown";

    /// <summary>The longest zone id the tz database has is 32 characters.</summary>
    public const int MaxTimeZoneLength = 64;

    /// <summary>The earliest target taken.</summary>
    public static readonly Instant Earliest = Instant.FromUtc(1900, 1, 1, 0, 0);

    /// <summary>The latest target taken.</summary>
    public static readonly Instant Latest = Instant.FromUtc(2200, 1, 1, 0, 0);

    /// <summary>How long a countdown's calendar event lasts, the same as a new event's default on the phone.</summary>
    public static readonly Duration EventLength = Duration.FromMinutes(30);

    /// <summary>
    /// The fields that are wrong, by the name the body spells them, or the
    /// countdown to store. A create and an update send the same body.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, AlertCountdown? Valid) Validate(
        Guid id, CountdownRequest request, Instant now)
    {
        var errors = new Dictionary<string, string[]>();

        var label = string.IsNullOrWhiteSpace(request.Label) ? DefaultLabel : request.Label.Trim();
        if (label.Length > MaxLabelLength || label.Any(char.IsControl))
        {
            errors["label"] = [$"At most {MaxLabelLength} characters, no control characters."];
        }

        Instant? target = null;
        if (request.TargetAt is { } text
            && OffsetDateTimePattern.ExtendedIso.Parse(text.Trim()) is { Success: true } parsed
            && parsed.Value.ToInstant() is var instant
            && instant >= Earliest
            && instant <= Latest)
        {
            target = instant;
        }
        else
        {
            errors["targetAt"] = ["A date and time with its offset, between 1900 and 2200."];
        }

        if (request.TimeZone is not { Length: > 0 and <= MaxTimeZoneLength } zone
            || DateTimeZoneProviders.Tzdb.GetZoneOrNull(zone) is null)
        {
            errors["timeZone"] = ["A time zone from the tz database, such as Asia/Amman."];
        }

        if (errors.Count > 0) return (errors, null);

        return (errors, new AlertCountdown(id, label, target!.Value, request.TimeZone!, now, request.Alarm ?? false));
    }

    /// <summary>The order the status lists them in: the target, then the label.</summary>
    public static IReadOnlyList<AlertCountdown> Sorted(IEnumerable<AlertCountdown> countdowns) =>
    [
        .. countdowns
            .OrderBy(countdown => countdown.TargetAt)
            .ThenBy(countdown => countdown.Label, StringComparer.Ordinal)
            .ThenBy(countdown => countdown.Id)
    ];
}

/// <summary>A countdown as the page and the phone send it. Every field is checked by <see cref="AlertCountdowns.Validate"/>.</summary>
/// <param name="TargetAt">ISO 8601 with its offset, such as 2026-11-20T09:00:00+03:00.</param>
/// <param name="Alarm">Ring at the target. Left out, a create is off and an update keeps the saved choice, so an older phone app changes nothing it does not send.</param>
public sealed record CountdownRequest(string? Label, string? TargetAt, string? TimeZone, bool? Alarm = null);
