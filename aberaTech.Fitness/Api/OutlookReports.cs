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
/// <param name="EarliestSelectionDate">The first selection date by which every forecast gate is ready.</param>
/// <param name="BindingGate">The gate that sets that date.</param>
public sealed record OutlookDto(
    string? SelectionDate,
    double WeeklyHours,
    double MeasuredWeeklyHours,
    double Compliance,
    double StartVdot,
    IReadOnlyList<OutlookGateDto> Gates,
    string? EarliestSelectionDate,
    string? BindingGate,
    IReadOnlyList<string> Assumptions);

/// <summary>Builds the gate forecast out of the fitted model and the log's histories.</summary>
internal static class OutlookReports
{
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

        // The week asked about: the one named, else the one the log shows,
        // else the one the profile plans.
        var measured = athlete.MeasuredDose.RunningHours;
        var hours = weeklyHours ?? (measured > 0.25 ? measured : row.PlanMinutesPerWeek / 60.0);
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

        var requirements = SelectionReadiness.Gates
            .SelectMany(g => g.Requirements)
            .GroupBy(r => r.Metric)
            .ToDictionary(g => g.Key, g => g.First());

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
                g.Lines.Select(l =>
                {
                    var requirement = requirements[l.Metric];
                    return new OutlookLineDto(
                        l.Metric, l.Label, l.Probability, l.Method, l.Evidence, l.Projected, l.ReadyInMonths, l.HoursToReach,
                        requirement.Unit, requirement.Comparison.ToString(), requirement.Target);
                }).ToArray())).ToArray(),
            earliest?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            earliest?.GateId,
            assumptions);
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
