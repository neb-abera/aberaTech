using Dynastream.Fit;
using NodaTime;
using Activity = aberaTech.Fitness.Data.Activity;
using Lap = aberaTech.Fitness.Data.Lap;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// A single Garmin .fit activity file, decoded with Garmin's own SDK. This is the
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
        // ran (the zero-length one a stop leaves behind) is not a lap.
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
    /// Stores the average heart rate and speed of the two halves of the
    /// recording's settled time, from the second-by-second records.
    /// </summary>
    /// <remarks>
    /// A heart-rate drift test compares the halves of a steady effort. Evoke
    /// starts its test only once the heart rate has held for 3 minutes, so
    /// the climb to that heart rate is no part of it. The same holds after a
    /// stop: on 2026-11-23 Neb's treadmill ended its program at 60 minutes,
    /// and his heart rate took 86 seconds to come back after the restart.
    /// Read whole, that recording gave 157.3 then 160.1 bpm. Read settled it
    /// gave 158.5 then 160.4.
    ///
    /// A break in the records longer than <see cref="BreakSeconds"/> ends a
    /// stretch. In each stretch the heart rate has settled at the first
    /// moment its one-minute average differs from the minute before by less
    /// than <see cref="SettleRiseBpm"/> bpm. What follows is settled time,
    /// when it lasts <see cref="MinimumSettledSeconds"/> or more. The halves
    /// split the settled time in two. A recording with no settled stretch is
    /// split at its midpoint, whole.
    ///
    /// Heart rate is averaged over time, so a watch that records every few
    /// seconds weighs each sample by how long it stood. Speed is distance
    /// over time, from the distance field the watch accumulates.
    /// </remarks>
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
        if (end - start < 2 * 60) return;

        double? DistanceAt(double at)
        {
            var before = points.LastOrDefault(p => p.Seconds <= at && p.Distance is not null);
            return before.Distance;
        }

        BestEfforts(points.Where(p => p.Distance is not null).Select(p => (p.Seconds, p.Distance!.Value)).ToArray(), activity);

        var heartRates = points.Select(p => (p.Seconds, p.Hr)).ToArray();
        var settled = SettledStretches(heartRates);
        if (settled.Count == 0) settled = [(start, end)];

        // The first half takes settled time until half of it is used.
        var total = settled.Sum(s => s.To - s.From);
        var first = new List<(double From, double To)>();
        var second = new List<(double From, double To)>();
        var left = total / 2;
        foreach (var (from, to) in settled)
        {
            var taken = Math.Clamp(left, 0, to - from);
            if (taken > 0) first.Add((from, from + taken));
            if (to - from - taken > 0) second.Add((from + taken, to));
            left -= to - from;
        }

        double? MeanHr(List<(double From, double To)> pieces)
        {
            double weighted = 0, span = 0;
            foreach (var (from, to) in pieces)
            {
                var (w, s) = WeightedHr(heartRates, 0, heartRates.Length - 1, from, to);
                weighted += w;
                span += s;
            }

            return span > 0 ? weighted / span : null;
        }

        double? Speed(List<(double From, double To)> pieces)
        {
            double meters = 0, seconds = 0;
            foreach (var (from, to) in pieces)
            {
                if (DistanceAt(from) is not { } a || DistanceAt(to) is not { } b) return null;
                meters += b - a;
                seconds += to - from;
            }

            return meters > 0 && seconds > 0 ? meters / seconds : null;
        }

        activity.FirstHalfHr = MeanHr(first);
        activity.SecondHalfHr = MeanHr(second);
        activity.FirstHalfMetersPerSecond = Speed(first);
        activity.SecondHalfMetersPerSecond = Speed(second);
        activity.HalvesSeconds = total;
    }

    /// <summary>A gap between records longer than this is a stop: the watch was paused.</summary>
    public const double BreakSeconds = 20;

    /// <summary>The heart rate is averaged over windows this long to see whether it still climbs.</summary>
    public const double SettleWindowSeconds = 60;

    /// <summary>Settled: one window's average is within this many bpm of the window before.</summary>
    public const double SettleRiseBpm = 2;

    /// <summary>A stretch that has not settled this long after its start is not steady work.</summary>
    public const double SettleSearchSeconds = 15 * 60;

    /// <summary>Settled time shorter than this is left out.</summary>
    public const double MinimumSettledSeconds = 5 * 60;

    /// <summary>
    /// The parts of a recording at a settled heart rate, in order: each
    /// unbroken stretch of records, from where its heart rate stopped
    /// climbing to its end.
    /// </summary>
    internal static List<(double From, double To)> SettledStretches(IReadOnlyList<(double Seconds, double? Hr)> points)
    {
        var stretches = new List<(double From, double To)>();
        var first = 0;
        for (var i = 1; i <= points.Count; i++)
        {
            if (i < points.Count && points[i].Seconds - points[i - 1].Seconds <= BreakSeconds) continue;

            var last = i - 1;
            if (SettledAt(points, first, last) is { } from && points[last].Seconds - from >= MinimumSettledSeconds)
            {
                stretches.Add((from, points[last].Seconds));
            }

            first = i;
        }

        return stretches;
    }

    /// <summary>When the heart rate of one unbroken stretch settled, or null when it did not.</summary>
    private static double? SettledAt(IReadOnlyList<(double Seconds, double? Hr)> points, int first, int last)
    {
        var from = points[first].Seconds;
        var to = points[last].Seconds;
        var earliest = true;
        for (var i = first; i <= last; i++)
        {
            var at = points[i].Seconds;
            if (at < from + SettleWindowSeconds) continue;
            if (at + SettleWindowSeconds > to || at > from + SettleSearchSeconds) return null;

            var before = WeightedHr(points, first, last, at - SettleWindowSeconds, at);
            var after = WeightedHr(points, first, last, at, at + SettleWindowSeconds);
            if (before.Span > 0 && after.Span > 0
                && Math.Abs(after.Weighted / after.Span - before.Weighted / before.Span) < SettleRiseBpm)
            {
                // Settled at the first look: the stretch began settled.
                return earliest ? from : at;
            }

            earliest = false;
        }

        return null;
    }

    /// <summary>Heart rate times seconds, and the seconds that carried a heart rate, between two moments.</summary>
    private static (double Weighted, double Span) WeightedHr(
        IReadOnlyList<(double Seconds, double? Hr)> points, int first, int last, double from, double to)
    {
        double weighted = 0, span = 0;
        for (var i = first; i < last; i++)
        {
            if (points[i + 1].Seconds <= from) continue;
            if (points[i].Seconds >= to) break;

            var a = Math.Max(points[i].Seconds, from);
            var b = Math.Min(points[i + 1].Seconds, to);
            if (b <= a || points[i].Hr is not > 0) continue;
            weighted += points[i].Hr!.Value * (b - a);
            span += b - a;
        }

        return (weighted, span);
    }

    /// <summary>The reading of the records this importer writes: settled halves and best efforts.</summary>
    public const int CurrentRecordsVersion = 3;

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
