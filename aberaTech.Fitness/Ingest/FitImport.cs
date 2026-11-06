using Dynastream.Fit;
using NodaTime;
using Activity = aberaTech.Fitness.Data.Activity;
using Lap = aberaTech.Fitness.Data.Lap;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// A single Garmin .fit activity file, decoded with Garmin's own SDK — the
/// rich-detail path, straight off the watch or a Connect activity export.
/// </summary>
public static class FitImport
{
    public static Activity? Parse(Stream stream)
    {
        var decode = new Decode();
        var listener = new FitListener();
        decode.MesgEvent += listener.OnMesg;

        if (!decode.IsFIT(stream)) return null;
        stream.Position = 0;
        if (!decode.Read(stream)) return null;

        var messages = listener.FitMessages;
        var session = messages.SessionMesgs.FirstOrDefault();
        if (session is null) return null;

        var startTime = session.GetStartTime()?.GetDateTime()
                        ?? messages.FileIdMesgs.FirstOrDefault()?.GetTimeCreated()?.GetDateTime();
        if (startTime is null) return null;

        var started = Instant.FromDateTimeUtc(System.DateTime.SpecifyKind(startTime.Value, DateTimeKind.Utc));

        var timerSeconds = session.GetTotalTimerTime() ?? session.GetTotalElapsedTime() ?? 0;
        if (timerSeconds <= 0) return null;

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Source = "garmin-fit",
            ExternalId = $"fit:{started}",
            StartedAt = started,
            Sport = MapSport(session.GetSport(), session.GetSubSport()),
            Name = "",
            DistanceMeters = session.GetTotalDistance(),
            DurationSeconds = timerSeconds,
            AverageHr = session.GetAvgHeartRate(),
            MaxHr = session.GetMaxHeartRate(),
            Indoor = IsIndoor(session.GetSubSport())
        };

        // The laps as the watch recorded them, in order. A lap that never
        // ran — the zero-length one a stop leaves behind — is not a lap.
        var index = 0;
        foreach (var lap in messages.LapMesgs)
        {
            var seconds = lap.GetTotalTimerTime() ?? lap.GetTotalElapsedTime() ?? 0;
            if (seconds <= 0) continue;

            activity.Laps.Add(new Lap
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                Index = index++,
                DistanceMeters = lap.GetTotalDistance() is { } meters and > 0 ? meters : null,
                Seconds = seconds,
                AverageHr = lap.GetAvgHeartRate()
            });
        }

        Halves(messages.RecordMesgs, activity);

        return activity;
    }

    /// <summary>
    /// Splits the second-by-second records at the midpoint of the recording
    /// and stores each half's average heart rate and speed. Heart rate is
    /// averaged over time, so a watch that records every few seconds weighs
    /// each sample by how long it stood. Speed is distance over time, from the
    /// distance field the watch accumulates.
    /// </summary>
    internal static void Halves(IEnumerable<RecordMesg> records, Activity activity)
    {
        // The file was read, whatever it held, so the sync need not read it again.
        activity.RecordsVersion = CurrentRecordsVersion;

        var points = records
            .Select(r => (Time: r.GetTimestamp()?.GetDateTime(), Hr: (double?)r.GetHeartRate(), Distance: (double?)r.GetDistance()))
            .Where(p => p.Time is not null)
            .Select(p => (Seconds: (p.Time!.Value - System.DateTime.UnixEpoch).TotalSeconds, p.Hr, p.Distance))
            .OrderBy(p => p.Seconds)
            .ToArray();
        if (points.Length < 4) return;

        var start = points[0].Seconds;
        var end = points[^1].Seconds;
        var middle = (start + end) / 2;
        if (end - start < 2 * 60) return;

        double? MeanHr(double from, double to)
        {
            double weighted = 0, span = 0;
            for (var i = 0; i < points.Length - 1; i++)
            {
                var a = Math.Max(points[i].Seconds, from);
                var b = Math.Min(points[i + 1].Seconds, to);
                if (b <= a || points[i].Hr is not > 0) continue;
                weighted += points[i].Hr!.Value * (b - a);
                span += b - a;
            }

            return span > 0 ? weighted / span : null;
        }

        double? DistanceAt(double at)
        {
            var before = points.LastOrDefault(p => p.Seconds <= at && p.Distance is not null);
            return before.Distance;
        }

        double? Speed(double from, double to) =>
            DistanceAt(from) is { } a && DistanceAt(to) is { } b && b > a ? (b - a) / (to - from) : null;

        BestEfforts(points.Where(p => p.Distance is not null).Select(p => (p.Seconds, p.Distance!.Value)).ToArray(), activity);

        activity.FirstHalfHr = MeanHr(start, middle);
        activity.SecondHalfHr = MeanHr(middle, end);
        activity.FirstHalfMetersPerSecond = Speed(start, middle);
        activity.SecondHalfMetersPerSecond = Speed(middle, end);
    }

    /// <summary>The reading of the records this importer writes: halves and best efforts.</summary>
    public const int CurrentRecordsVersion = 2;

    public const double Mile = 1609.344;

    /// <summary>
    /// The fastest continuous stretch of each goal distance, by two pointers
    /// over the cumulative distance. The time across the stretch is taken
    /// between the first record at or past its start and the first record at
    /// or past its end, so a gap in the records can only make it slower.
    /// </summary>
    internal static void BestEfforts(IReadOnlyList<(double Seconds, double Meters)> points, Activity activity)
    {
        double? Fastest(double meters)
        {
            if (points.Count < 2 || points[^1].Meters - points[0].Meters < meters) return null;
            double? best = null;
            var j = 0;
            for (var i = 0; i < points.Count; i++)
            {
                if (j < i) j = i;
                while (j < points.Count && points[j].Meters - points[i].Meters < meters) j++;
                if (j == points.Count) break;
                var time = points[j].Seconds - points[i].Seconds;
                if (time > 0 && (best is null || time < best)) best = time;
            }

            return best;
        }

        activity.Best1_5MileSeconds = Fastest(1.5 * Mile);
        activity.Best2MileSeconds = Fastest(2 * Mile);
        activity.Best5MileSeconds = Fastest(5 * Mile);
        activity.Best10MileSeconds = Fastest(10 * Mile);
    }

    /// <summary>Whether the sub-sport says the session was indoors; null when it says nothing.</summary>
    internal static bool? IsIndoor(SubSport? subSport) => subSport switch
    {
        SubSport.Treadmill or SubSport.IndoorRunning or SubSport.VirtualActivity
            or SubSport.IndoorCycling or SubSport.IndoorRowing => true,
        SubSport.Generic or null => null,
        _ => false
    };

    internal static string MapSport(Sport? sport, SubSport? subSport) => sport switch
    {
        Sport.Running => "run",
        Sport.Hiking => "ruck",
        Sport.Walking when subSport == SubSport.CasualWalking => "other",
        Sport.Walking => "ruck",
        Sport.Training when subSport == SubSport.StrengthTraining => "strength",
        _ => "other"
    };
}
