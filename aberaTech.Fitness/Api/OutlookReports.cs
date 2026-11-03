using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>The inputs a goal forecast is made from: the fitted model, the week, and the log's histories.</summary>
internal static class OutlookReports
{
    /// <summary>
    /// The week the forecast is asked about, and where it came from.
    /// </summary>
    /// <remarks>
    /// Named on the slider, else the hours the profile says the athlete can
    /// train, else the profile's planned minutes, else what the log shows.
    /// The log used to come first, which seeded the slider with a thin
    /// month's 0.9 h and made every gate look hopeless before anyone touched
    /// it. The log's average is still reported beside the slider; it is a
    /// fact about the past, not the week to plan on.
    /// </remarks>
    internal static (double Hours, string Basis) DefaultWeek(
        double? named, double measured, double availableHoursPerWeek, double planMinutesPerWeek)
    {
        if (named is { } hours) return (hours, "the week set on the slider");
        if (availableHoursPerWeek > 0)
            return (availableHoursPerWeek, $"the hours a week the profile says you can train ({availableHoursPerWeek:0.0} h)");
        if (planMinutesPerWeek > 0)
            return (planMinutesPerWeek / 60.0, $"the profile's planned {planMinutesPerWeek:0} minutes a week");
        return (measured, "what the log's last eight weeks show, because the profile names no week");
    }

    /// <summary>Everything one forecast is asked against: the athlete, the week, the readings.</summary>
    internal sealed record Frame(
        AthleteSnapshot Athlete,
        TrajectoryParameters P,
        double Measured,
        double Hours,
        string Basis,
        DoseSchedule Schedule,
        Readings Readings,
        OutlookContext Context);

    /// <summary>The forecast's inputs, from a log that has already been read. One engine for the outlook and the goals.</summary>
    internal static Frame FrameFor(
        TrainingHistory history,
        ReadinessEvidence evidence,
        double? weeklyHours,
        double compliance,
        LocalDate today,
        Instant now)
    {
        var athlete = FitnessReports.Snapshot(history, now, today.Year);
        var row = athlete.Row;
        var weight = evidence.LatestWeight;

        var p = new TrajectoryParameters(
            athlete.AnchorVdot, athlete.ReclaimVdot, athlete.Fit.RatePerMonth.Value, athlete.Fit.Responsiveness.Value);
        var limits = FitnessReports.LimitsFor(athlete, athlete.Fit.Responsiveness.Value);

        var measured = athlete.MeasuredDose.RunningHours;
        var (hours, basis) = DefaultWeek(weeklyHours, measured, row.AvailableHoursPerWeek, row.PlanMinutesPerWeek);
        var plan = DoseResponse.Allocate(hours, limits).Dose;
        var schedule = DoseSchedule.Constant(plan.Scale(compliance));

        var readings = ReadinessReports.Gather(history, evidence, athlete.Trend, today);

        var context = new OutlookContext(
            p,
            athlete.Fit,
            schedule,
            limits,
            row.HomeAltitudeMeters,
            weight?.WeightKg,
            readings.Measurements,
            readings.Histories.ToDictionary(h => h.Key, h => (IReadOnlyList<DatedValue>)h.Value),
            today);

        return new Frame(athlete, p, measured, hours, basis, schedule, readings, context);
    }

}
