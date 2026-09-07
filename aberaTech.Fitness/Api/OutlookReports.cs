using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>One gate line, looked at by its due date.</summary>
public sealed record OutlookLineDto(
    string Metric,
    string Label,
    double? Probability,
    string Method,
    string Evidence,
    double? Projected,
    double? ReadyInMonths,
    double? HoursToReach,
    string Unit,
    string Comparison,
    double Target);

/// <summary>One gate, looked at by its due date.</summary>
public sealed record OutlookGateDto(
    string Id,
    string Name,
    int WeeksBeforeSelection,
    string? DueOn,
    double MonthsAway,
    double? Probability,
    int Forecast,
    int Total,
    double? ReadyInMonths,
    IReadOnlyList<OutlookLineDto> Lines);

/// <summary>The gates as a forecast, under one training week.</summary>
/// <param name="WeeklyHours">The running hours the forecast was made at.</param>
/// <param name="MeasuredWeeklyHours">The running hours the log shows, for the slider's starting point.</param>
/// <param name="PlannedWeeklyHours">The running hours the profile says the athlete can train, the slider's starting point.</param>
/// <param name="HoursBasis">Where the week being asked about came from, in words.</param>
/// <param name="EarliestSelectionDate">The first selection date by which every forecast gate is ready.</param>
/// <param name="BindingGate">The gate that sets that date.</param>
/// <param name="Inputs">Every number the forecast is computed from, with where each came from.</param>
public sealed record OutlookDto(
    string? SelectionDate,
    double WeeklyHours,
    double MeasuredWeeklyHours,
    double PlannedWeeklyHours,
    string HoursBasis,
    double Compliance,
    double StartVdot,
    IReadOnlyList<OutlookGateDto> Gates,
    string? EarliestSelectionDate,
    string? BindingGate,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Assumptions);

/// <summary>Builds the gate forecast out of the fitted model and the log's histories.</summary>
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

    public static async Task<OutlookDto> BuildAsync(
        FitnessDbContext database,
        double? weeklyHours,
        double compliance,
        LocalDate today,
        CancellationToken cancellationToken)
    {
        var athlete = await FitnessReports.SnapshotAsync(database, today.Year, cancellationToken);
        var row = athlete.Row;
        var weight = await database.BodyMetrics.OrderByDescending(m => m.Date).FirstOrDefaultAsync(cancellationToken);

        var p = new TrajectoryParameters(
            athlete.AnchorVdot, athlete.ReclaimVdot, athlete.Fit.RatePerMonth.Value, athlete.Fit.Responsiveness.Value);
        var limits = FitnessReports.LimitsFor(athlete, athlete.Fit.Responsiveness.Value);

        var measured = athlete.MeasuredDose.RunningHours;
        var (hours, basis) = DefaultWeek(weeklyHours, measured, row.AvailableHoursPerWeek, row.PlanMinutesPerWeek);
        var plan = DoseResponse.Allocate(hours, limits).Dose;
        var schedule = DoseSchedule.Constant(plan.Scale(compliance));

        var readings = await ReadinessReports.GatherAsync(database, row, weight, athlete.Trend, today, cancellationToken);

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

        var gates = GateOutlook.Evaluate(context, row.SelectionDate);
        var earliest = GateOutlook.EarliestSelection(gates, today);

        var inputs = new List<string>
        {
            row.VdotMeasuredOn is { } measuredOn
                ? Text($"Anchor: VDOT {p.StartVdot:0.0}, from the {measuredOn:yyyy-MM-dd} time trial in the profile.")
                : Text($"Anchor: VDOT {p.StartVdot:0.0}, from the profile; no time trial date is set."),
            athlete.ReclaimVdot is { } reclaim
                ? Text($"Reclaim ceiling: VDOT {reclaim:0.0}, from the lifetime best in the profile; fitness held before comes back faster.")
                : "Reclaim ceiling: none; no lifetime best is set in the profile.",
            Text($"Trajectory: rate {athlete.Fit.RatePerMonth.Value:0.000} per month, responsiveness {athlete.Fit.Responsiveness.Value:0.00}, fitted to the log (the Banister model)."),
            Text($"Running hours: {hours:0.0} h a week, {basis}. The log's last eight weeks average {measured:0.0} h."),
            Text($"Weeks kept: {Format.Percent(compliance)}; the planned week is multiplied by it before the forecast, so 75% of 7 h is asked as 5.25 h."),
            weight is { } w
                ? Text($"Bodyweight: {w.WeightKg:0.0} kg, weighed {w.Date:yyyy-MM-dd}; the ruck lines read through it.")
                : "Bodyweight: none logged; the ruck lines cannot be forecast.",
            Text($"Home altitude: {row.HomeAltitudeMeters:0} m; run times are corrected for it."),
            row.SelectionDate is { } selection
                ? Text($"Selection date: {selection:yyyy-MM-dd}; each gate is due the number of weeks before it that its standard is meant to be in hand.")
                : Text($"Selection date: none set; every gate is asked about {GateOutlook.DefaultHorizonMonths:0} months out."),
            Text($"Today: {today:yyyy-MM-dd}.")
        };

        var assumptions = new List<string>
        {
            Text($"Running lines are asked of the Banister trajectory from VDOT {p.StartVdot:0.0} at {schedule.Target.RunningHours:0.0} h/week ({Format.Percent(compliance)} of {hours:0.0} h kept), split the way the model would advise; the spread is the fit's covariance plus its residual."),
            "Rucks are read through the load-carriage model at the current bodyweight; a timed twelve-mile in the log would outrank it.",
            Text($"Every other line is a straight line through its own dated readings ({GateOutlook.MinimumTrendPoints}+ over {GateOutlook.MinimumTrendSpanDays}+ days), with the regression's prediction interval as the spread."),
            "A gate's chance is the product of its lines', as if independent. They are not, so it is a floor, and it covers only the lines that could be forecast.",
            row.SelectionDate is null
                ? Text($"No selection date is set, so every gate is asked about {GateOutlook.DefaultHorizonMonths:0} months out.")
                : "Each gate's date counts back from the selection date by the weeks its standard is meant to be in hand."
        };

        return new OutlookDto(
            row.SelectionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            hours,
            measured,
            row.AvailableHoursPerWeek,
            basis,
            compliance,
            p.StartVdot,
            gates.Select(g => new OutlookGateDto(
                g.GateId,
                g.Name,
                g.WeeksBeforeSelection,
                g.DueOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.MonthsAway,
                g.Probability,
                g.Forecast,
                g.Total,
                g.ReadyInMonths,
                // The line's own standard. This used to be looked up by
                // metric across all gates, first match wins, so the
                // competitive row printed the day-one two-mile and the prep
                // five-mile while its probabilities were computed against
                // its own, harder, numbers.
                g.Lines.Select(l =>
                {
                    return new OutlookLineDto(
                        l.Metric, l.Label, l.Probability, l.Method, l.Evidence, l.Projected, l.ReadyInMonths, l.HoursToReach,
                        l.Unit, l.Comparison.ToString(), l.Target);
                }).ToArray())).ToArray(),
            earliest?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            earliest?.GateId,
            inputs,
            assumptions);
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
