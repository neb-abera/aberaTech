namespace aberaTech.Fitness.Domain;

/// <summary>One lap of a logged session, reduced to what classifying it needs.</summary>
public sealed record LoggedLap(double? DistanceMeters, double Seconds, int? AverageHr);

/// <summary>One logged session, reduced to what classifying it needs.</summary>
/// <param name="Sport">run, ruck, strength or other.</param>
/// <param name="DistanceMeters">Distance, when the session had one.</param>
/// <param name="Seconds">Duration.</param>
/// <param name="Laps">The watch's laps, when the source carried them.</param>
/// <param name="Indoor">Treadmill or trainer, when the source said.</param>
/// <param name="AverageHr">Average heart rate, when recorded.</param>
public sealed record LoggedSession(
    string Sport,
    double? DistanceMeters,
    double Seconds,
    IReadOnlyList<LoggedLap>? Laps = null,
    bool? Indoor = null,
    int? AverageHr = null);

/// <summary>
/// The athlete's heart-rate landmarks, for placing effort where pace cannot be
/// trusted: the aerobic threshold and the lactate threshold.
/// </summary>
/// <remarks>
/// The pace bands and the heart-rate bands have to agree on what "easy" is.
/// Daniels' E and M bands (which this engine folds into Easy) run up to about
/// 84% of VO2max; the aerobic threshold sits near the bottom of M and the
/// lactate threshold near the top of T. So Easy reaches to the midpoint
/// between the two thresholds, Threshold reaches to the lactate threshold,
/// and anything above is Interval.
///
/// When no lactate-threshold heart rate is known, it is taken to be 10% above
/// the aerobic threshold — the spread at which Johnston's rule says the
/// aerobic system is no longer the limiter (<see cref="Citations.UphillAthleteAet"/>).
/// </remarks>
public sealed record HeartRateBands(int AetHr, int? LtHr)
{
    /// <summary>The assumed AeT→LT spread when the lactate threshold has not been measured.</summary>
    public const double DefaultLtRatio = 1.10;

    /// <summary>The lactate-threshold heart rate, measured or assumed.</summary>
    public int ThresholdCeiling => LtHr ?? (int)Math.Round(AetHr * DefaultLtRatio);

    /// <summary>The heart rate above which a session is no longer easy.</summary>
    public int EasyCeiling => (AetHr + ThresholdCeiling) / 2;

    public TrainingZone ZoneOf(int hr)
    {
        if (hr <= EasyCeiling) return TrainingZone.Easy;
        return hr <= ThresholdCeiling ? TrainingZone.Threshold : TrainingZone.Interval;
    }
}

/// <summary>
/// Reads a training week out of what was actually logged, instead of asking
/// the athlete to describe it.
/// </summary>
/// <remarks>
/// Each run is placed in a zone against the athlete's current Daniels bands
/// (<see cref="TrainingPaces"/>) — the same bands the app prescribes with, so
/// the plan and the log are scored on one ruler. Rucking is aerobic volume;
/// lifting is its own zone.
///
/// A session's average pace hides its structure: an interval workout, averaged
/// with its recoveries, reads as an easy run. When the watch recorded laps,
/// each lap is placed on its own and the session's time is split across the
/// zones its laps landed in. Without laps the average stands, and the hard
/// time is under-counted — which the explanation says.
///
/// A treadmill's distance is the belt's word, or the wrist's guess at it, so
/// an indoor lap is placed by heart rate against
/// <see cref="HeartRateBands"/> instead, whenever a heart rate was recorded.
///
/// Citation: <see cref="Citations.DanielsVdot"/>.
/// </remarks>
public static class SessionMix
{
    /// <summary>
    /// What an hour of rucking is worth as an hour of easy running, towards a
    /// running ceiling.
    /// </summary>
    /// <remarks>
    /// Rucking is aerobic work and builds the same engine, but it is not the
    /// same movement: slower, heavier, and with a stride and economy that do
    /// not fully transfer to running a race. Counting it hour-for-hour
    /// flattered a rucking week's effect on a running time. Counting it as
    /// nothing would be worse — it is a large part of this athlete's training
    /// and a large part of the aerobic base it builds.
    ///
    /// The weight is a judgement, not a measurement, and it is here as one
    /// number so it can be argued with rather than buried in a classifier.
    ///
    /// Citation: <see cref="Citations.UphillAthleteAet"/>.
    /// </remarks>
    public const double RuckTransfer = 0.75;

    /// <summary>Laps have to account for this much of a session before they are trusted to describe it.</summary>
    public const double LapCoverage = 0.8;

    /// <summary>A lap shorter than this is a button press, not a segment.</summary>
    public const double MinimumLapSeconds = 60;

    /// <summary>
    /// The spread of lap paces (coefficient of variation) above which a run
    /// was a workout, not a steady effort.
    /// </summary>
    public const double SteadyPaceSpread = 0.15;

    /// <summary>The zone a whole session lands in by its average pace — the no-laps reading.</summary>
    public static TrainingZone ZoneOf(LoggedSession session, double vdot) => ZoneOf(session, vdot, null);

    /// <summary>
    /// The zone a whole session lands in: by heart rate when it was indoors
    /// and a heart rate is known, by average pace otherwise.
    /// </summary>
    public static TrainingZone ZoneOf(LoggedSession session, double vdot, HeartRateBands? bands)
    {
        if (session.Sport == "strength") return TrainingZone.Strength;
        if (session.Sport != "run") return TrainingZone.Easy;

        if (session.Indoor == true && bands is not null && session.AverageHr is { } hr && hr > 0)
        {
            return bands.ZoneOf(hr);
        }

        return ByPace(session.DistanceMeters, session.Seconds, vdot);
    }

    /// <summary>
    /// One session's hours split across the zones, lap by lap when the laps
    /// are there to split by.
    /// </summary>
    public static TrainingDose Split(LoggedSession session, double vdot, HeartRateBands? bands)
    {
        var dose = new TrainingDose();
        var hours = session.Seconds / 3600.0;

        if (session.Sport != "run" || !LapsDescribe(session))
        {
            var zone = ZoneOf(session, vdot, bands);
            return dose.With(zone, hours);
        }

        var laps = session.Laps!;
        var covered = 0.0;
        foreach (var lap in laps)
        {
            if (lap.Seconds <= 0) continue;
            var zone = LapZone(lap, session.Indoor, vdot, bands);
            dose = dose.With(zone, dose[zone] + lap.Seconds / 3600.0);
            covered += lap.Seconds;
        }

        // Whatever the laps did not cover was the watch running between
        // presses — easy, as far as anyone can tell.
        var remainder = Math.Max(0, session.Seconds - covered) / 3600.0;
        return dose.With(TrainingZone.Easy, dose.EasyHours + remainder);
    }

    /// <summary>The average training week these sessions add up to.</summary>
    public static TrainingDose WeeklyDose(
        IEnumerable<LoggedSession> sessions, double weeks, double vdot, HeartRateBands? bands = null)
    {
        if (weeks <= 0) throw new ArgumentOutOfRangeException(nameof(weeks));

        var dose = new TrainingDose();
        foreach (var session in sessions)
        {
            var split = Split(session, vdot, bands);

            // Rucking counts towards the running ceiling at a discount, because
            // it is the same engine through a different movement.
            var scale = (session.Sport == "ruck" ? RuckTransfer : 1.0) / weeks;

            foreach (var zone in TrainingDose.Zones)
            {
                dose = dose.With(zone, dose[zone] + split[zone] * scale);
            }
        }

        return dose;
    }

    /// <summary>Whether a session's laps are complete enough to describe it.</summary>
    public static bool LapsDescribe(LoggedSession session)
    {
        if (session.Laps is not { Count: >= 2 } laps || session.Seconds <= 0) return false;
        var covered = laps.Where(l => l.Seconds > 0).Sum(l => l.Seconds);
        return covered >= LapCoverage * session.Seconds;
    }

    /// <summary>
    /// Whether a run was one steady effort — the kind the aerobic trend can
    /// read pace-at-heart-rate from — rather than a workout or a race.
    /// </summary>
    /// <remarks>
    /// Two tests. The average heart rate has to sit below the lactate
    /// threshold, because the pace–heart-rate relationship the normalization
    /// leans on is linear only through the aerobic range. And when laps are
    /// there, their paces have to agree with each other: repeats with
    /// recoveries spread far wider than any steady run drifts.
    /// </remarks>
    public static bool IsSteady(LoggedSession session, HeartRateBands? bands)
    {
        if (session.Sport != "run") return false;
        if (session.AverageHr is not { } hr || hr <= 0) return false;
        if (bands is not null && hr > bands.ThresholdCeiling) return false;

        var paces = (session.Laps ?? [])
            .Where(l => l.Seconds >= MinimumLapSeconds && l.DistanceMeters is > 0)
            .Select(l => l.Seconds / (l.DistanceMeters!.Value / 1000.0))
            .ToArray();
        if (paces.Length < 3) return true;

        var mean = paces.Average();
        var spread = Math.Sqrt(paces.Sum(p => (p - mean) * (p - mean)) / paces.Length) / mean;
        return spread <= SteadyPaceSpread;
    }

    /// <summary>The arithmetic behind a measured week.</summary>
    public static IReadOnlyList<CalculationStep> Explain(TrainingDose dose, double weeks, int sessions, int lapSplit = 0)
    {
        var placed = lapSplit > 0
            ? $"{lapSplit} of them split lap by lap, the rest placed by average pace against your own bands"
            : "each placed by its average pace against your own bands";

        return new CalculationTrace()
            .Add(
                "Your current week, from the log",
                $"{sessions} sessions over {weeks:0.#} weeks, {placed}",
                $"{dose.EasyHours:0.0} h easy, {dose.ThresholdHours:0.0} h threshold, "
                + $"{dose.IntervalHours:0.0} h interval, {dose.StrengthHours:0.0} h strength",
                Citations.DanielsVdot.Id)
            .Steps;
    }

    private static TrainingZone LapZone(LoggedLap lap, bool? indoor, double vdot, HeartRateBands? bands)
    {
        var byHeart = bands is not null && lap.AverageHr is { } hr && hr > 0;

        // Indoors the heart rate is the better witness; outdoors the pace is,
        // and the heart rate stands in only when the lap has no distance.
        if (indoor == true && byHeart) return bands!.ZoneOf(lap.AverageHr!.Value);
        if (lap.DistanceMeters is > 0) return ByPace(lap.DistanceMeters, lap.Seconds, vdot);
        if (byHeart) return bands!.ZoneOf(lap.AverageHr!.Value);
        return TrainingZone.Easy;
    }

    private static TrainingZone ByPace(double? distanceMeters, double seconds, double vdot)
    {
        if (distanceMeters is not { } meters || meters <= 0 || seconds <= 0)
        {
            return TrainingZone.Easy;
        }

        var secPerKm = seconds / (meters / 1000.0);
        var bands = TrainingPaces.For(vdot);
        var threshold = bands.Single(b => b.Zone == "T");
        var interval = bands.Single(b => b.Zone == "I");

        if (secPerKm <= interval.SlowSecPerKm) return TrainingZone.Interval;
        if (secPerKm <= threshold.SlowSecPerKm) return TrainingZone.Threshold;
        return TrainingZone.Easy;
    }
}
