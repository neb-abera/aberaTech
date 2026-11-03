using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// How a countdown repeats: never, or every N hours, days, weeks, months or
/// years. Once its time comes it moves to the next date. Days, weeks,
/// months and years keep the time on its zone's clock. Hours are elapsed
/// time. A date a month or year does not have (the 31st in a short month,
/// 29 February in a common year) is skipped, as RFC 5545, and so Google
/// Calendar, skips it.
/// </summary>
public static class CountdownRepeats
{
    public const string None = "none";

    public const string Hourly = "hourly";

    public const string Daily = "daily";

    public const string Weekly = "weekly";

    public const string Monthly = "monthly";

    public const string Yearly = "yearly";

    public const int MaxLength = 16;

    /// <summary>The most units between two dates.</summary>
    public const int MaxEvery = 999;

    public static readonly IReadOnlyList<string> Choices = [None, Hourly, Daily, Weekly, Monthly, Yearly];

    /// <summary>The RRULE Google is sent, or null for one that does not repeat.</summary>
    public static string? Rule(string repeat, int every)
    {
        var frequency = repeat switch
        {
            Hourly => "HOURLY",
            Daily => "DAILY",
            Weekly => "WEEKLY",
            Monthly => "MONTHLY",
            Yearly => "YEARLY",
            _ => null
        };
        if (frequency is null) return null;
        return every == 1 ? $"RRULE:FREQ={frequency}" : $"RRULE:FREQ={frequency};INTERVAL={every}";
    }

    /// <summary>
    /// The first date in the series after <paramref name="now"/>, counted
    /// from <paramref name="anchor"/>, which is itself a date in the series.
    /// The anchor when it is after now or the countdown does not repeat.
    /// </summary>
    public static Instant Next(Instant anchor, string repeat, int every, DateTimeZone zone, Instant now)
    {
        if (repeat == None || anchor > now) return anchor;
        every = Math.Clamp(every, 1, MaxEvery);

        if (repeat == Hourly)
        {
            var step = Duration.FromHours(every);
            var steps = (long)Math.Floor((now - anchor) / step) + 1;
            return anchor + step * steps;
        }

        var start = anchor.InZone(zone).LocalDateTime;
        var days = repeat switch
        {
            Daily => 1.0,
            Weekly => 7.0,
            Monthly => 31.0,
            _ => 366.0
        } * every;
        // A first guess near now, so a series from 1900 takes a few steps, not thousands.
        var guess = (long)((now - anchor).TotalDays / days);
        for (var n = Math.Max(guess, 1); n < guess + 400; n++)
        {
            var periods = (int)(n * every);
            var local = repeat switch
            {
                Daily => start.PlusDays(periods),
                Weekly => start.PlusWeeks(periods),
                Monthly => start.PlusMonths(periods),
                _ => start.PlusYears(periods)
            };
            // A month or year without the day is skipped. A day and a week always have it.
            if (repeat is Monthly or Yearly && local.Day != start.Day) continue;
            var at = local.InZoneLeniently(zone).ToInstant();
            if (at > now) return at;
        }

        return anchor;
    }

    /// <summary>"Every 2 weeks", "Every year": what the page shows. Null for one that does not repeat.</summary>
    public static string? Describe(string repeat, int every)
    {
        var unit = repeat switch
        {
            Hourly => "hour",
            Daily => "day",
            Weekly => "week",
            Monthly => "month",
            Yearly => "year",
            _ => null
        };
        return unit is null ? null : every == 1 ? $"Every {unit}" : $"Every {every} {unit}s";
    }
}
