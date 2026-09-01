using NodaTime;

namespace aberaTech.Fitness.Data;

/// <summary>
/// Whether two records describe one session, when their clocks cannot be
/// compared.
///
/// Garmin's export carries a real UTC clock for every activity. The Connect
/// website's activities CSV carries a wall clock and no zone at all, so the
/// same run read from both is off by whatever the watch's offset was — and
/// this athlete's own export flips from −6 to +3 partway through the year, so
/// there is no single zone that would reconcile them either. Matching on what
/// the session *was* rather than when the file says it started is the only
/// comparison that survives that.
/// </summary>
public static class SameSession
{
    /// <summary>Both files round the same recorded time; they do not disagree by more.</summary>
    public const double DurationToleranceSeconds = 2;

    /// <summary>The summary and the CSV round distance differently — metres, not kilometres, apart.</summary>
    public const double DistanceToleranceMeters = 25;

    /// <summary>
    /// Wider than any real UTC offset (−12 to +14) and narrower than a day, so
    /// a zone shift is forgiven and yesterday's identical run is not.
    /// </summary>
    public static readonly Duration ClockTolerance = Duration.FromHours(15);

    public static bool Matches(Activity one, Activity other)
    {
        if (one.Sport != other.Sport) return false;

        var apart = (one.StartedAt - other.StartedAt).TotalSeconds;
        if (Math.Abs(apart) > ClockTolerance.TotalSeconds) return false;

        if (Math.Abs(one.DurationSeconds - other.DurationSeconds) > DurationToleranceSeconds) return false;

        return (one.DistanceMeters, other.DistanceMeters) switch
        {
            (null, null) => true,
            ({ } a, { } b) => Math.Abs(a - b) <= DistanceToleranceMeters,
            // One of them knows the distance and the other does not: too little
            // in common to call it the same run.
            _ => false
        };
    }

    /// <summary>Sources whose timestamps are real instants.</summary>
    public static readonly string[] TrueClock = ["garmin-export", "garmin-fit"];

    /// <summary>Sources that record a wall clock and leave the zone to be guessed.</summary>
    public static readonly string[] LocalClock = ["garmin-csv"];
}
