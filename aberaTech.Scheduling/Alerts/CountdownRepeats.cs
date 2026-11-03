using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// How a countdown repeats: never, or every week, month or year at the same
/// wall-clock time in its zone. Once its time comes it moves to the next
/// date. A date the period does not have (the 31st in a short month, 29
/// February in a common year) is skipped, as RFC 5545, and so Google
/// Calendar, skips it.
/// </summary>
public static class CountdownRepeats
{
    public const string None = "none";

    public const string Weekly = "weekly";

    public const string Monthly = "monthly";

    public const string Yearly = "yearly";

    public const int MaxLength = 16;

    public static readonly IReadOnlyList<string> Choices = [None, Weekly, Monthly, Yearly];

    /// <summary>The RRULE Google is sent, or null for one that does not repeat.</summary>
    public static string? Rule(string repeat) => repeat switch
    {
        Weekly => "RRULE:FREQ=WEEKLY",
        Monthly => "RRULE:FREQ=MONTHLY",
        Yearly => "RRULE:FREQ=YEARLY",
        _ => null
    };

    /// <summary>
    /// The first date in the series after <paramref name="now"/>, counted
    /// from <paramref name="anchor"/>, which is itself a date in the series.
    /// The anchor when it is after now or the countdown does not repeat.
    /// </summary>
    public static Instant Next(Instant anchor, string repeat, DateTimeZone zone, Instant now)
    {
        if (repeat == None || anchor > now) return anchor;

        var start = anchor.InZone(zone).LocalDateTime;
        // A first guess near now, so a yearly series from 1900 takes a few steps, not 126.
        var periods = repeat switch
        {
            Weekly => (int)((now - anchor).TotalDays / 7),
            Monthly => (int)((now - anchor).TotalDays / 31),
            _ => (int)((now - anchor).TotalDays / 366)
        };
        for (var n = Math.Max(periods, 1); n < periods + 400; n++)
        {
            var local = repeat switch
            {
                Weekly => start.PlusWeeks(n),
                Monthly => start.PlusMonths(n),
                _ => start.PlusYears(n)
            };
            // A month or year without the day is skipped. A week always has it.
            if (repeat != Weekly && local.Day != start.Day) continue;
            var at = local.InZoneLeniently(zone).ToInstant();
            if (at > now) return at;
        }

        return anchor;
    }
}
