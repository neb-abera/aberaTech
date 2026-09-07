using System.Globalization;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>One dated reading of a metric.</summary>
public sealed record DatedValue(LocalDate Date, double Value);

/// <summary>How one gate line was forecast.</summary>
public static class OutlookMethod
{
    /// <summary>The Banister trajectory with its fitted covariance, through VDOT.</summary>
    public const string Trajectory = "trajectory";

    /// <summary>A straight line through the metric's own dated history.</summary>
    public const string Trend = "trend";

    /// <summary>Passing now, with too little history to project.</summary>
    public const string Held = "held";

    /// <summary>Nothing to forecast from.</summary>
    public const string None = "none";
}

/// <summary>One gate line, looked at by its due date.</summary>
/// <param name="Probability">The chance of clearing it by the date, when a method could say.</param>
/// <param name="Projected">The central projection at the date, in the line's unit.</param>
/// <param name="ReadyInMonths">When the chance first reaches <see cref="GateOutlook.ReadyProbability"/>; null when it never does within the horizon.</param>
/// <param name="HoursToReach">Weekly running hours whose central trajectory clears the line by the date; trajectory lines only.</param>
/// <param name="Target">The standard this line was asked against: its own gate's, not the first gate that shares the metric.</param>
public sealed record LineOutlook(
    string Metric,
    string Label,
    double? Probability,
    string Method,
    string Evidence,
    double? Projected,
    double? ReadyInMonths,
    double? HoursToReach,
    double Target,
    string Unit,
    Comparison Comparison);

/// <summary>One gate, looked at by its due date.</summary>
/// <param name="Probability">The product of the forecast lines' probabilities, treating them as independent.</param>
/// <param name="Forecast">How many lines carry a probability.</param>
/// <param name="ReadyInMonths">When the last forecast line reaches the ready probability; null when one never does.</param>
public sealed record GateOutlookResult(
    string GateId,
    string Name,
    int WeeksBeforeSelection,
    LocalDate? DueOn,
    double MonthsAway,
    double? Probability,
    int Forecast,
    int Total,
    double? ReadyInMonths,
    IReadOnlyList<LineOutlook> Lines);

/// <summary>Everything a forecast of the gates is made from.</summary>
/// <param name="P">The athlete's trajectory.</param>
/// <param name="Fit">The fit the trajectory came from, for its covariance.</param>
/// <param name="Schedule">The week being asked about.</param>
/// <param name="Limits">The recovery budget the hours question is bounded by.</param>
/// <param name="HomeAltitudeMeters">Where the standards will be run.</param>
/// <param name="BodyKg">For the load-carriage model.</param>
/// <param name="Current">What the log says today, by metric.</param>
/// <param name="Histories">Dated readings by metric, for the metrics no trajectory covers.</param>
public sealed record OutlookContext(
    TrajectoryParameters P,
    FitResult Fit,
    DoseSchedule Schedule,
    DoseLimits Limits,
    double HomeAltitudeMeters,
    double? BodyKg,
    IReadOnlyDictionary<string, Measurement> Current,
    IReadOnlyDictionary<string, IReadOnlyList<DatedValue>> Histories,
    LocalDate Today);

/// <summary>
/// Turns the selection gates from a scoreboard into a forecast: for every
/// line, the chance of clearing it by the date it is due, under a named
/// training week.
/// </summary>
/// <remarks>
/// Two engines, chosen by what the line measures. Running lines — the timed
/// runs, the rucks read through the load-carriage model, the pace at the
/// aerobic threshold — are all functions of VDOT, so each is turned into the
/// VDOT that clears it and asked of the Banister trajectory, whose fitted
/// covariance gives the probability (<see cref="Forecast"/>). The rest —
/// reps, lifts, body fat, the fitness test — have no dose-response model
/// here, so each is projected along a straight line through its own dated
/// history, with the prediction interval of that regression as the spread.
/// A straight line is a modest model of a lift; it is the honest one when
/// the alternative is a number with no interval at all.
///
/// A gate's probability is the product of its lines', which treats them as
/// independent. They are not — a stronger athlete is stronger everywhere —
/// so the product is a floor, and it is labelled with how many of the lines
/// it covers.
///
/// Citations: <see cref="Citations.NonlinearRegression"/>,
/// <see cref="Citations.BanisterModel"/>, <see cref="Citations.PandolfLoadCarriage"/>.
/// </remarks>
public static class GateOutlook
{
    /// <summary>The chance at which a line counts as ready.</summary>
    public const double ReadyProbability = 0.80;

    /// <summary>A trend needs this many dated readings…</summary>
    public const int MinimumTrendPoints = 3;

    /// <summary>…spanning at least this long, or it is a couple of days, not a trend.</summary>
    public const int MinimumTrendSpanDays = 42;

    /// <summary>When no selection date is named, every gate is asked about this far out.</summary>
    public const double DefaultHorizonMonths = 12;

    /// <summary>How far ahead a trend is searched for its ready date.</summary>
    public const double TrendHorizonMonths = 36;

    private const double DaysPerMonth = 30.4375;
    private static readonly double TwelveMiles = 12 * Vdot.MileMeters;

    public static IReadOnlyList<GateOutlookResult> Evaluate(OutlookContext context, LocalDate? selectionDate)
    {
        return SelectionReadiness.Gates
            .Select(gate =>
            {
                var dueOn = selectionDate?.PlusWeeks(-gate.WeeksBeforeSelection);
                var months = dueOn is { } due
                    ? Math.Max(0, Period.Between(context.Today, due, PeriodUnits.Days).Days / DaysPerMonth)
                    : DefaultHorizonMonths;

                var lines = gate.Requirements.Select(r => Line(r, months, context)).ToArray();
                var forecast = lines.Where(l => l.Probability is not null).ToArray();

                double? probability = forecast.Length == 0
                    ? null
                    : forecast.Aggregate(1.0, (acc, l) => acc * l.Probability!.Value);

                double? ready = forecast.Length == 0 ? null
                    : forecast.Any(l => l.ReadyInMonths is null) ? null
                    : forecast.Max(l => l.ReadyInMonths!.Value);

                return new GateOutlookResult(
                    gate.Id, gate.Name, gate.WeeksBeforeSelection, dueOn, months,
                    probability, forecast.Length, lines.Length, ready, lines);
            })
            .ToArray();
    }

    /// <summary>
    /// The earliest selection date by which every gate that can be forecast
    /// is ready, and the gate that sets it. Null when some forecast gate
    /// never gets there.
    /// </summary>
    public static (LocalDate Date, string GateId)? EarliestSelection(
        IReadOnlyList<GateOutlookResult> gates, LocalDate today)
    {
        (LocalDate Date, string GateId)? latest = null;
        foreach (var gate in gates.Where(g => g.Forecast > 0))
        {
            if (gate.ReadyInMonths is not { } ready) return null;
            var date = today.PlusDays((int)Math.Ceiling(ready * DaysPerMonth)).PlusWeeks(gate.WeeksBeforeSelection);
            if (latest is null || date > latest.Value.Date) latest = (date, gate.GateId);
        }

        return latest;
    }

    public static LineOutlook Line(Requirement requirement, double months, OutlookContext context)
    {
        if (TargetVdot(requirement, context) is { } target)
        {
            return ByTrajectory(requirement, target, months, context);
        }

        if (context.Histories.TryGetValue(requirement.Metric, out var history)
            && TrendAt(history, context.Today.PlusDays((int)Math.Round(months * DaysPerMonth))) is { } trend)
        {
            return ByTrend(requirement, history, trend, months, context);
        }

        var passing = context.Current.TryGetValue(requirement.Metric, out var current)
                      && Passes(requirement, current.Value);
        var count = context.Histories.TryGetValue(requirement.Metric, out var some) ? some.Count : 0;
        var need = $"{MinimumTrendPoints} dated readings over {MinimumTrendSpanDays} days make a trend; the log has {count}";

        return passing
            ? new LineOutlook(requirement.Metric, requirement.Label, null, OutlookMethod.Held,
                $"Clear today, held rather than forecast: {need}.", current!.Value, null, null,
                requirement.Target, requirement.Unit, requirement.Comparison)
            : new LineOutlook(requirement.Metric, requirement.Label, null, OutlookMethod.None,
                $"Nothing to project from: {need}.", null, null, null,
                requirement.Target, requirement.Unit, requirement.Comparison);
    }

    /// <summary>The VDOT that clears a running line, or null when the line is not a running one.</summary>
    internal static double? TargetVdot(Requirement requirement, OutlookContext context)
    {
        switch (requirement.Metric)
        {
            case SelectionReadiness.Metrics.RunTwoMile:
                return RaceVdot(2 * Vdot.MileMeters, requirement.Target, context.HomeAltitudeMeters);
            case SelectionReadiness.Metrics.RunFiveMile:
                return RaceVdot(5 * Vdot.MileMeters, requirement.Target, context.HomeAltitudeMeters);

            case SelectionReadiness.Metrics.RuckTwelveMileAt45:
                return context.BodyKg is { } b45 ? RuckVdot(b45, LoadCarriage.ReferenceLoadKg, requirement.Target) : null;
            case SelectionReadiness.Metrics.RuckTwelveMileAt35:
                return context.BodyKg is { } b35 ? RuckVdot(b35, 35 / BodyMass.PoundsPerKg, requirement.Target) : null;

            case SelectionReadiness.Metrics.AerobicThresholdPace:
                // The pace held at the aerobic threshold moves with the engine:
                // today's ratio of AeT pace to VDOT, carried forward through
                // the elasticity of VDOT in speed.
                if (!context.Current.TryGetValue(requirement.Metric, out var aet) || aet.Value <= 0) return null;
                var minutesFor5k = aet.Value / Vdot.MileMeters * 5000 / 60;
                var elasticity = Vdot.SpeedElasticity(5000, minutesFor5k);
                return context.P.StartVdot * Math.Pow(aet.Value / requirement.Target, elasticity);

            default:
                return null;
        }
    }

    private static double RaceVdot(double distanceMeters, double seconds, double altitudeMeters) =>
        Vdot.FromRace(distanceMeters, Altitude.ToSeaLevel(seconds, altitudeMeters) / 60.0);

    /// <summary>The VDOT whose predicted twelve-mile at the load is the target: the load-carriage model inverted.</summary>
    internal static double RuckVdot(double bodyKg, double loadKg, double targetSeconds)
    {
        double slower = 15, faster = 100;
        for (var i = 0; i < 50; i++)
        {
            var mid = (slower + faster) / 2;
            if (LoadCarriage.PredictSeconds(mid, bodyKg, loadKg, TwelveMiles) > targetSeconds) slower = mid; else faster = mid;
        }

        return (slower + faster) / 2;
    }

    private static LineOutlook ByTrajectory(Requirement requirement, double targetVdot, double months, OutlookContext context)
    {
        var probability = Forecast.Probability(context.P, context.Fit, context.Schedule, targetVdot, months);
        var ready = Forecast.MonthsForProbability(context.P, context.Fit, context.Schedule, targetVdot, ReadyProbability);
        var hours = months > 0
            ? Trajectory.HoursToReach(context.P, targetVdot, months, null, context.Limits)
            : null;

        var projectedVdot = Trajectory.VdotAt(context.P, context.Schedule, months);
        var projected = ProjectedValue(requirement, projectedVdot, context);

        var evidence = Text(
            $"Needs VDOT {targetVdot:0.0}; the trajectory from {context.P.StartVdot:0.0} at {context.Schedule.Target.RunningHours:0.0} h/week reaches {projectedVdot:0.0} in {months:0.0} months")
            + (hours is { } h ? Text($"; {h:0.0} h/week would put the central projection there on the date") : "; no sustainable week gets the central projection there in time")
            + ".";

        return new LineOutlook(requirement.Metric, requirement.Label, probability, OutlookMethod.Trajectory, evidence, projected, ready, hours,
            requirement.Target, requirement.Unit, requirement.Comparison);
    }

    /// <summary>The line's own unit at a projected VDOT, for the reader who does not think in VDOT.</summary>
    private static double? ProjectedValue(Requirement requirement, double vdot, OutlookContext context) =>
        requirement.Metric switch
        {
            SelectionReadiness.Metrics.RunTwoMile => Altitude.AtAltitude(Vdot.MinutesFor(2 * Vdot.MileMeters, vdot) * 60, context.HomeAltitudeMeters),
            SelectionReadiness.Metrics.RunFiveMile => Altitude.AtAltitude(Vdot.MinutesFor(5 * Vdot.MileMeters, vdot) * 60, context.HomeAltitudeMeters),
            SelectionReadiness.Metrics.RuckTwelveMileAt45 when context.BodyKg is { } b => LoadCarriage.PredictSeconds(vdot, b, LoadCarriage.ReferenceLoadKg, TwelveMiles),
            SelectionReadiness.Metrics.RuckTwelveMileAt35 when context.BodyKg is { } b => LoadCarriage.PredictSeconds(vdot, b, 35 / BodyMass.PoundsPerKg, TwelveMiles),
            _ => null
        };

    private static LineOutlook ByTrend(
        Requirement requirement, IReadOnlyList<DatedValue> history, (double Projected, double Sd) trend, double months, OutlookContext context)
    {
        var probability = TrendProbability(requirement, trend);

        double? ready = null;
        for (var m = 0.0; m <= TrendHorizonMonths; m += 0.25)
        {
            var at = TrendAt(history, context.Today.PlusDays((int)Math.Round(m * DaysPerMonth)));
            if (at is { } point && TrendProbability(requirement, point) >= ReadyProbability)
            {
                ready = m;
                break;
            }
        }

        var first = history.Min(h => h.Date);
        var last = history.Max(h => h.Date);
        var evidence = Text(
            $"Straight line through {history.Count} readings from {first:yyyy-MM-dd} to {last:yyyy-MM-dd} projects {SelectionReadiness.Describe(requirement.Unit, trend.Projected)} ± {SelectionReadiness.Describe(requirement.Unit, trend.Sd)} at the date.");

        return new LineOutlook(requirement.Metric, requirement.Label, probability, OutlookMethod.Trend, evidence, trend.Projected, ready, null,
            requirement.Target, requirement.Unit, requirement.Comparison);
    }

    private static double TrendProbability(Requirement requirement, (double Projected, double Sd) trend)
    {
        var margin = requirement.Comparison == Comparison.AtLeast
            ? trend.Projected - requirement.Target
            : requirement.Target - trend.Projected;
        return trend.Sd <= 0 ? (margin >= 0 ? 1 : 0) : Linear.NormalCdf(margin / trend.Sd);
    }

    /// <summary>
    /// Ordinary least squares through the readings, evaluated at a date, with
    /// the prediction-interval spread: residual scatter, widened by how far
    /// the date sits from the readings' centre. Null when there is no trend
    /// to speak of.
    /// </summary>
    internal static (double Projected, double Sd)? TrendAt(IReadOnlyList<DatedValue> history, LocalDate at)
    {
        var points = history
            .GroupBy(h => h.Date)
            .Select(g => g.OrderByDescending(h => h.Value).First())
            .OrderBy(h => h.Date)
            .ToArray();
        if (points.Length < MinimumTrendPoints) return null;

        var origin = points[0].Date;
        var span = Period.Between(origin, points[^1].Date, PeriodUnits.Days).Days;
        if (span < MinimumTrendSpanDays) return null;

        var xs = points.Select(p => (double)Period.Between(origin, p.Date, PeriodUnits.Days).Days).ToArray();
        var ys = points.Select(p => p.Value).ToArray();
        var n = xs.Length;
        var xBar = xs.Average();
        var yBar = ys.Average();
        var sxx = xs.Sum(x => (x - xBar) * (x - xBar));
        var slope = xs.Zip(ys, (x, y) => (x - xBar) * (y - yBar)).Sum() / sxx;
        var intercept = yBar - slope * xBar;

        var residual = xs.Zip(ys, (x, y) => y - (intercept + slope * x)).Sum(e => e * e);
        var sd = Math.Sqrt(residual / Math.Max(1, n - 2));

        // A perfectly straight history has no measured scatter; a floor keeps
        // the interval from collapsing to a point nobody believes.
        var floor = 0.02 * Math.Max(Math.Abs(yBar), 1e-9);
        sd = Math.Max(sd, floor);

        var x = (double)Period.Between(origin, at, PeriodUnits.Days).Days;
        var widen = Math.Sqrt(1 + 1.0 / n + (x - xBar) * (x - xBar) / sxx);
        return (intercept + slope * x, sd * widen);
    }

    private static bool Passes(Requirement requirement, double value) =>
        requirement.Comparison == Comparison.AtLeast ? value >= requirement.Target : value <= requirement.Target;

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
