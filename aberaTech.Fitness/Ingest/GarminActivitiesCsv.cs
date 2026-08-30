using System.Globalization;
using aberaTech.Fitness.Data;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// Garmin Connect's Activities page CSV export — the bulk-history path. One
/// row per activity, summary only, which is exactly what the aerobic trend
/// needs (distance, time, average HR).
/// </summary>
public static class GarminActivitiesCsv
{
    private static readonly LocalDateTimePattern DatePattern =
        LocalDateTimePattern.Create("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static IReadOnlyList<Activity> Parse(string csv, DateTimeZone zone)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count < 2) return [];

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int Col(string name) => Array.IndexOf(header, name);

        var type = Col("activity type");
        var date = Col("date");
        var title = Col("title");
        var distance = Col("distance");
        var time = Col("time");
        var avgHr = Col("avg hr");
        var maxHr = Col("max hr");

        if (type < 0 || date < 0 || time < 0)
        {
            throw new FormatException("Not a Garmin activities export: expected Activity Type, Date and Time columns.");
        }

        var activities = new List<Activity>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Length <= Math.Max(time, date)) continue;

            var parsedDate = DatePattern.Parse(row[date].Trim());
            if (!parsedDate.Success) continue;

            var seconds = ParseDuration(row[time]);
            if (seconds <= 0) continue;

            var started = parsedDate.Value.InZoneLeniently(zone).ToInstant();

            activities.Add(new Activity
            {
                Id = Guid.NewGuid(),
                Source = "garmin-csv",
                ExternalId = $"garmin:{parsedDate.Value:yyyyMMdd'T'HHmmss}",
                StartedAt = started,
                Sport = MapSport(row[type]),
                Name = title >= 0 && row.Length > title ? row[title] : "",
                DistanceMeters = distance >= 0 ? ParseKilometers(row[distance]) : null,
                DurationSeconds = seconds,
                AverageHr = ParseHr(avgHr, row),
                MaxHr = ParseHr(maxHr, row)
            });
        }

        return activities;
    }

    internal static string MapSport(string activityType)
    {
        var t = activityType.Trim().ToLowerInvariant();
        if (t.Contains("ruck")) return "ruck";
        if (t.Contains("run")) return "run";
        if (t.Contains("hik")) return "ruck";
        if (t.Contains("strength")) return "strength";
        return "other";
    }

    /// <summary>Garmin writes durations as H:mm:ss, mm:ss, or with fractions.</summary>
    internal static double ParseDuration(string value)
    {
        var parts = value.Trim().Split(':');
        if (parts.Length is < 2 or > 3) return 0;

        double total = 0;
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return 0;
            total = total * 60 + n;
        }

        return total;
    }

    private static double? ParseKilometers(string value)
    {
        // Garmin can write "3.02" or "3,02" depending on account locale, and
        // thousands separators on long rides. Normalise the comma-decimal case.
        var cleaned = value.Trim().Replace("\"", "");
        if (cleaned.Length == 0 || cleaned == "--") return null;
        if (cleaned.Contains(',') && !cleaned.Contains('.')) cleaned = cleaned.Replace(',', '.');
        else cleaned = cleaned.Replace(",", "");

        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var km)
            ? km * 1000.0
            : null;
    }

    private static int? ParseHr(int column, string[] row)
    {
        if (column < 0 || row.Length <= column) return null;
        return int.TryParse(row[column].Trim(), out var hr) && hr > 0 ? hr : null;
    }
}
