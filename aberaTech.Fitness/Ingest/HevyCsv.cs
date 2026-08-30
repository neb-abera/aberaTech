using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// Hevy's free "Export data" CSV: one row per set, grouped here into strength
/// activities. Works without a Pro subscription — this is the zero-cost path.
/// </summary>
public static class HevyCsv
{
    private static readonly LocalDateTimePattern StartPattern =
        LocalDateTimePattern.Create("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    public static IReadOnlyList<Activity> Parse(string csv, DateTimeZone zone)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count < 2) return [];

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int Col(string name) => Array.IndexOf(header, name);

        var title = Col("title");
        var startTime = Col("start_time");
        var endTime = Col("end_time");
        var exercise = Col("exercise_title");
        var setIndex = Col("set_index");
        var reps = Col("reps");

        // The weight column is named for the account's display unit.
        var weightKg = Col("weight_kg");
        var weightLbs = Col("weight_lbs");

        if (title < 0 || startTime < 0 || exercise < 0)
        {
            throw new FormatException("Not a Hevy export: expected title, start_time and exercise_title columns.");
        }

        return rows.Skip(1)
            .Where(r => r.Length > Math.Max(exercise, startTime))
            .GroupBy(r => (Title: r[title], Start: r[startTime]))
            .Select(g =>
            {
                var parsed = StartPattern.Parse(g.Key.Start.Trim());
                if (!parsed.Success) return null;

                var started = parsed.Value.InZoneLeniently(zone).ToInstant();
                var duration = 0.0;
                if (endTime >= 0)
                {
                    var end = StartPattern.Parse(g.First()[endTime].Trim());
                    if (end.Success)
                    {
                        duration = Period.Between(parsed.Value, end.Value, PeriodUnits.Seconds).Seconds;
                    }
                }

                var activity = new Activity
                {
                    Id = Guid.NewGuid(),
                    Source = "hevy-csv",
                    // The export carries no workout id; start time identifies it.
                    ExternalId = $"hevy:{started}",
                    StartedAt = started,
                    Sport = "strength",
                    Name = g.Key.Title,
                    DurationSeconds = Math.Max(0, duration)
                };

                var index = 0;
                foreach (var row in g)
                {
                    var kg = 0.0;
                    if (weightKg >= 0 && double.TryParse(row[weightKg], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) kg = v;
                    else if (weightLbs >= 0 && double.TryParse(row[weightLbs], NumberStyles.Float, CultureInfo.InvariantCulture, out var lb)) kg = BodyMass.PoundsToKg(lb);

                    var repCount = 0;
                    if (reps >= 0) _ = int.TryParse(row[reps], out repCount);

                    var order = index++;
                    if (setIndex >= 0 && int.TryParse(row[setIndex], out var declared)) order = declared;

                    activity.Sets.Add(new StrengthSet
                    {
                        Id = Guid.NewGuid(),
                        ActivityId = activity.Id,
                        Exercise = row[exercise],
                        SetIndex = order,
                        WeightKg = kg,
                        Reps = repCount
                    });
                }

                return activity;
            })
            .Where(a => a is not null)
            .Select(a => a!)
            .ToArray();
    }
}
