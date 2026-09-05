namespace aberaTech.Fitness.Domain;

/// <summary>One logged session, reduced to what classifying it needs.</summary>
/// <param name="Sport">run, ruck, strength or other.</param>
/// <param name="DistanceMeters">Distance, when the session had one.</param>
/// <param name="Seconds">Duration.</param>
public sealed record LoggedSession(string Sport, double? DistanceMeters, double Seconds);

/// <summary>
/// Reads a training week out of what was actually logged, instead of asking
/// the athlete to describe it.
/// </summary>
/// <remarks>
/// Each run is placed in a zone by its own average pace against the athlete's
/// current Daniels bands (<see cref="TrainingPaces"/>) — the same bands the app
/// prescribes with, so the plan and the log are scored on one ruler. Rucking
/// is aerobic volume; lifting is its own zone.
///
/// One honest limitation: a session's average pace includes its warm-up and
/// recovery jogs, so an interval workout mostly lands in the easy band. The
/// classification therefore <i>under</i>-counts hard time, which makes the
/// measured mix a floor rather than a reading. Splitting laps out of the FIT
/// files would fix it; until then the number is presented as what it is.
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

    public static TrainingZone ZoneOf(LoggedSession session, double vdot)
    {
        if (session.Sport == "strength") return TrainingZone.Strength;
        if (session.Sport != "run") return TrainingZone.Easy;
        if (session.DistanceMeters is not { } meters || meters <= 0 || session.Seconds <= 0)
        {
            return TrainingZone.Easy;
        }

        var secPerKm = session.Seconds / (meters / 1000.0);
        var bands = TrainingPaces.For(vdot);
        var threshold = bands.Single(b => b.Zone == "T");
        var interval = bands.Single(b => b.Zone == "I");

        if (secPerKm <= interval.SlowSecPerKm) return TrainingZone.Interval;
        if (secPerKm <= threshold.SlowSecPerKm) return TrainingZone.Threshold;
        return TrainingZone.Easy;
    }

    /// <summary>The average training week these sessions add up to.</summary>
    public static TrainingDose WeeklyDose(IEnumerable<LoggedSession> sessions, double weeks, double vdot)
    {
        if (weeks <= 0) throw new ArgumentOutOfRangeException(nameof(weeks));

        var dose = new TrainingDose();
        foreach (var session in sessions)
        {
            var zone = ZoneOf(session, vdot);
            var hours = session.Seconds / 3600.0 / weeks;

            // Rucking counts towards the running ceiling at a discount, because
            // it is the same engine through a different movement.
            if (session.Sport == "ruck") hours *= RuckTransfer;

            dose = dose.With(zone, dose[zone] + hours);
        }

        return dose;
    }

    /// <summary>The arithmetic behind a measured week.</summary>
    public static IReadOnlyList<CalculationStep> Explain(TrainingDose dose, double weeks, int sessions) =>
        new CalculationTrace()
            .Add(
                "Your current week, from the log",
                $"{sessions} sessions over {weeks:0.#} weeks, each placed by its average pace against your own bands",
                $"{dose.EasyHours:0.0} h easy, {dose.ThresholdHours:0.0} h threshold, "
                + $"{dose.IntervalHours:0.0} h interval, {dose.StrengthHours:0.0} h strength",
                Citations.DanielsVdot.Id)
            .Steps;
}
