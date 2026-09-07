using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.Strava;

/// <summary>
/// A Strava activity as this library's activity, laps and all.
/// </summary>
/// <remarks>
/// Strava has no ruck. A watch records a ruck as a hike or a walk, and this
/// athlete names it, so the name decides: a walk or hike called a ruck is a
/// ruck, and one that is not is still a ruck by the convention the Garmin
/// paths already use, because a walk with a heart-rate strap and a pack is
/// what this log means by one. The trainer flag is the treadmill flag the
/// Garmin summaries never carried.
/// </remarks>
public static class StravaMapping
{
    public const string Source = "strava";

    public static Activity? Map(StravaActivity summary, IReadOnlyList<StravaLap> laps)
    {
        if (summary.StartDate is null) return null;

        var started = InstantPattern.ExtendedIso.Parse(summary.StartDate);
        if (!started.Success) return null;

        var seconds = summary.MovingTimeSeconds ?? summary.ElapsedTimeSeconds ?? 0;
        if (seconds <= 0) return null;

        var sport = MapSport(summary.SportType ?? summary.Type ?? "", summary.Name ?? "");
        var name = summary.Name ?? "";

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Source = Source,
            ExternalId = $"strava:{summary.Id}",
            StartedAt = started.Value,
            Sport = sport,
            Name = name,
            DistanceMeters = summary.DistanceMeters is > 0 ? summary.DistanceMeters : null,
            DurationSeconds = seconds,
            AverageHr = Hr(summary.AverageHeartRate),
            MaxHr = Hr(summary.MaxHeartRate),
            Indoor = summary.Trainer,
            LoadKg = sport == "ruck" ? RuckLoad.Parse(name) : null
        };

        var index = 0;
        foreach (var lap in laps.OrderBy(l => l.LapIndex))
        {
            var lapSeconds = lap.MovingTimeSeconds ?? lap.ElapsedTimeSeconds ?? 0;
            if (lapSeconds <= 0) continue;

            activity.Laps.Add(new Lap
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                Index = index++,
                DistanceMeters = lap.DistanceMeters is > 0 ? lap.DistanceMeters : null,
                Seconds = lapSeconds,
                AverageHr = Hr(lap.AverageHeartRate)
            });
        }

        return activity;
    }

    internal static string MapSport(string sportType, string name)
    {
        var t = sportType.Trim().ToLowerInvariant();
        var n = name.ToLowerInvariant();

        if (n.Contains("ruck")) return "ruck";
        if (t is "run" or "trailrun" or "virtualrun") return "run";
        if (t is "walk" or "hike") return "ruck";
        if (t is "weighttraining" or "crossfit" or "workout") return "strength";
        return "other";
    }

    private static int? Hr(double? bpm) => bpm is > 0 ? (int)Math.Round(bpm.Value) : null;
}
