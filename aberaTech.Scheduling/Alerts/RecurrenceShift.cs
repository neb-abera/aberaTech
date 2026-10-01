using System.Globalization;
using System.Text.RegularExpressions;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A series' recurrence lines, as Google's <c>recurrence</c> array holds
/// them, rewritten for a move of the series' first start, so the rule goes
/// on generating the days the start moved to. Pure: lines and two starts
/// in, lines out.
/// </summary>
/// <remarks>
/// RFC 5545 section 3.3.10: BYDAY, BYMONTHDAY and BYMONTH fix the days a
/// rule generates, whatever DTSTART says. A weekly rule with BYDAY=TU keeps
/// making Tuesdays after its start moves to a Wednesday. A rule without
/// them takes its days from DTSTART and follows the start unchanged.
///
/// The shift D is counted in whole days between the old and new start
/// dates on the series' own clock (its start.timeZone), so a move across a
/// clock change or across midnight in UTC counts the days the owner sees.
///
/// <list type="bullet">
/// <item>BYDAY: every weekday moves by D, modulo 7. A rule with one ordinal
/// weekday (2TU) in a MONTHLY rule, or a YEARLY rule with BYMONTH, whose
/// ordinal named the old start takes the new start's ordinal, so the start
/// stays an occurrence: the second Tuesday the 13th moved to Wednesday the
/// 14th is the second Wednesday. Any other ordinal is kept and its day
/// moves.</item>
/// <item>BYMONTHDAY, in a MONTHLY or YEARLY rule only: one value that named
/// the old start takes the new start's day (31 moved a day later is 1).
/// A negative one (-1, the last day) stays negative while the start stays
/// in its month. Any other value moves by D and is clamped to 1..31, or
/// -31..-1.</item>
/// <item>BYMONTH, in a YEARLY rule: one value that named the old start's
/// month takes the new start's month.</item>
/// <item>UNTIL, COUNT, INTERVAL, WKST and every other part stay as they
/// are. So do BYSETPOS, BYYEARDAY and BYWEEKNO, which Google's editor does
/// not write.</item>
/// <item>EXDATE and RDATE dates move by the same change in date and
/// wall-clock time as the start, in their own TZID or as UTC, so a
/// cancelled occurrence stays cancelled and an added one stays added. RFC
/// 5545 section 3.8.5.1 matches an EXDATE to an occurrence by its exact
/// start, so a date left behind would no longer match anything. A PERIOD
/// value is kept.</item>
/// </list>
/// </remarks>
public static class RecurrenceShift
{
    private static readonly string[] Weekdays = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"];

    private static readonly Regex DayEntry = new(
        "^(?<ordinal>[+-]?[0-9]{1,2})?(?<day>MO|TU|WE|TH|FR|SA|SU)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly LocalDateTimePattern Local =
        LocalDateTimePattern.Create("uuuuMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static readonly LocalDatePattern Date = LocalDatePattern.Create("uuuuMMdd", CultureInfo.InvariantCulture);

    /// <summary>
    /// The lines with every part a move from <paramref name="from"/> to
    /// <paramref name="to"/> changes. Null when nothing changes: a move
    /// within the same day of a series with no EXDATE or RDATE, or a rule
    /// that takes its days from DTSTART.
    /// </summary>
    /// <param name="zone">The series' clock, the zone Google names on its start.</param>
    public static IReadOnlyList<string>? Rewrite(IReadOnlyList<string> recurrence, Instant from, Instant to, DateTimeZone zone)
    {
        var oldStart = from.InZone(zone).LocalDateTime;
        var newStart = to.InZone(zone).LocalDateTime;
        var change = Period.Between(oldStart, newStart, PeriodUnits.Days | PeriodUnits.AllTimeUnits);
        var days = Period.Between(oldStart.Date, newStart.Date, PeriodUnits.Days).Days;

        var changed = false;
        var lines = new List<string>(recurrence.Count);
        foreach (var line in recurrence)
        {
            var next = Name(line) switch
            {
                "RRULE" or "EXRULE" when days != 0 => Rule(line, days, oldStart.Date, newStart.Date),
                "EXDATE" or "RDATE" when change != Period.Zero => Dates(line, change, days, zone),
                _ => line
            };
            changed |= !string.Equals(next, line, StringComparison.Ordinal);
            lines.Add(next);
        }

        return changed ? lines : null;
    }

    /// <summary>The property name, upper case: what comes before the first ';' or ':'.</summary>
    private static string Name(string line)
    {
        var end = line.IndexOfAny([';', ':']);
        return (end < 0 ? line : line[..end]).Trim().ToUpperInvariant();
    }

    /// <summary>An RRULE or EXRULE line with its day parts moved by <paramref name="days"/>.</summary>
    private static string Rule(string line, int days, LocalDate from, LocalDate to)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0) return line;

        var parts = line[(colon + 1)..]
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var equals = part.IndexOf('=', StringComparison.Ordinal);
                return equals < 0 ? (Key: part, Value: (string?)null) : (Key: part[..equals], Value: part[(equals + 1)..]);
            })
            .ToList();
        string? Get(string key) =>
            parts.FirstOrDefault(part => string.Equals(part.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

        var freq = Get("FREQ")?.ToUpperInvariant();
        var monthly = freq == "MONTHLY";
        var yearly = freq == "YEARLY";
        var byMonth = Get("BYMONTH");

        for (var i = 0; i < parts.Count; i++)
        {
            var (key, value) = parts[i];
            if (value is null) continue;
            var next = key.ToUpperInvariant() switch
            {
                "BYDAY" => ByDay(value, days, from, to, monthly || (yearly && byMonth is not null)),
                "BYMONTHDAY" when monthly || yearly => ByMonthDay(value, days, from, to),
                "BYMONTH" when yearly => ByMonth(value, from, to),
                _ => value
            };
            parts[i] = (key, next);
        }

        return line[..(colon + 1)] + string.Join(';', parts.Select(part => part.Value is null ? part.Key : $"{part.Key}={part.Value}"));
    }

    private static string ByDay(string value, int days, LocalDate from, LocalDate to, bool ordinalInMonth)
    {
        var entries = value.Split(',');
        var shift = ((days % 7) + 7) % 7;
        var moved = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            var match = DayEntry.Match(entry.Trim());
            if (!match.Success) return value;

            var day = Array.IndexOf(Weekdays, match.Groups["day"].Value.ToUpperInvariant());
            var newDay = Weekdays[(day + shift) % 7];
            var ordinal = match.Groups["ordinal"].Success ? int.Parse(match.Groups["ordinal"].Value, CultureInfo.InvariantCulture) : 0;
            if (ordinal != 0
                && entries.Length == 1
                && ordinalInMonth
                && day == (int)from.DayOfWeek - 1
                && ordinal == OrdinalInMonth(from, ordinal < 0))
            {
                ordinal = OrdinalInMonth(to, ordinal < 0);
            }

            moved.Add(ordinal == 0 ? newDay : $"{ordinal.ToString(CultureInfo.InvariantCulture)}{newDay}");
        }

        return string.Join(',', moved);
    }

    /// <summary>Which of its weekday in its month the date is: 2 for the second, -1 for the last.</summary>
    private static int OrdinalInMonth(LocalDate date, bool fromEnd) =>
        fromEnd
            ? -((DaysIn(date) - date.Day) / 7 + 1)
            : (date.Day - 1) / 7 + 1;

    private static int DaysIn(LocalDate date) => date.Calendar.GetDaysInMonth(date.Year, date.Month);

    private static string ByMonthDay(string value, int days, LocalDate from, LocalDate to)
    {
        var entries = value.Split(',');
        var numbers = new List<int>(entries.Length);
        foreach (var entry in entries)
        {
            if (!int.TryParse(entry.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                || number == 0 || number is > 31 or < -31)
            {
                return value;
            }

            numbers.Add(number);
        }

        if (numbers is [var only] && (only == from.Day || only == from.Day - DaysIn(from) - 1))
        {
            // One value that named the old start: the new start's day. A
            // count from the month's end stays one while the start stays in
            // its month.
            var sameMonth = from.Year == to.Year && from.Month == to.Month;
            return (only < 0 && sameMonth ? to.Day - DaysIn(to) - 1 : to.Day).ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(',', numbers
            .Select(number => number > 0 ? Math.Clamp(number + days, 1, 31) : Math.Clamp(number + days, -31, -1))
            .Distinct()
            .Select(number => number.ToString(CultureInfo.InvariantCulture)));
    }

    private static string ByMonth(string value, LocalDate from, LocalDate to) =>
        int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var month) && month == from.Month
            ? to.Month.ToString(CultureInfo.InvariantCulture)
            : value;

    /// <summary>An EXDATE or RDATE line with each date moved by the change on the wall clock.</summary>
    private static string Dates(string line, Period change, int days, DateTimeZone zone)
    {
        var colon = ValueStart(line);
        if (colon < 0) return line;

        var parameters = line[..colon].Split(';').Skip(1).ToList();
        string? Parameter(string name) => parameters
            .Select(parameter => parameter.Split('=', 2))
            .Where(pair => pair.Length == 2 && string.Equals(pair[0].Trim(), name, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair[1].Trim().Trim('"'))
            .FirstOrDefault();

        var kind = Parameter("VALUE")?.ToUpperInvariant();
        if (kind == "PERIOD") return line;
        var own = Parameter("TZID") is { } tzid ? DateTimeZoneProviders.Tzdb.GetZoneOrNull(tzid) : null;

        var values = line[(colon + 1)..].Split(',');
        var moved = new List<string>(values.Length);
        foreach (var raw in values)
        {
            var text = raw.Trim();
            string? next;
            if (kind == "DATE")
            {
                var parsed = Date.Parse(text);
                next = parsed.Success ? Date.Format(parsed.Value.PlusDays(days)) : null;
            }
            else if (text.EndsWith('Z'))
            {
                var parsed = Local.Parse(text[..^1]);
                next = parsed.Success
                    ? Local.Format(Moved(parsed.Value.InUtc().ToInstant(), change, zone).InUtc().LocalDateTime) + "Z"
                    : null;
            }
            else
            {
                var parsed = Local.Parse(text);
                // A TZID the database knows: its clock. A floating time, or
                // a TZID it does not know: the series' clock.
                var clock = own ?? zone;
                next = parsed.Success
                    ? Local.Format(Moved(clock.AtLeniently(parsed.Value).ToInstant(), change, zone).InZone(clock).LocalDateTime)
                    : null;
            }

            if (next is null) return line;
            moved.Add(next);
        }

        return line[..(colon + 1)] + string.Join(',', moved);
    }

    /// <summary>An instant moved by a change in date and time on the series' wall clock.</summary>
    private static Instant Moved(Instant at, Period change, DateTimeZone zone) =>
        zone.AtLeniently(at.InZone(zone).LocalDateTime + change).ToInstant();

    /// <summary>The colon that starts a content line's value: the first one outside a quoted parameter.</summary>
    private static int ValueStart(string line)
    {
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ':' && !quoted) return i;
        }

        return -1;
    }
}
