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

    /// <summary>Bodyweight or body fat, from the stated weight plan and the weigh-ins since it began.</summary>
    public const string Plan = "plan";

    /// <summary>Today's lift carried forward at the rate lifters of his level gain.</summary>
    public const string Progression = "progression";

    /// <summary>Passing now, with no model to project it.</summary>
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
/// <param name="ReachMonths">When the central projection first reaches the target; null when no method could say or it never does.</param>
/// <param name="Steps">The arithmetic, line by line, for the methods that can show it.</param>
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
    Comparison Comparison,
    double? ReachMonths = null,
    IReadOnlyList<CalculationStep>? Steps = null);

/// <summary>Everything a forecast of a goal is made from.</summary>
/// <param name="P">The athlete's trajectory.</param>
/// <param name="Fit">The fit the trajectory came from, for its covariance.</param>
/// <param name="Schedule">The week being asked about.</param>
/// <param name="Limits">The recovery budget the hours question is bounded by.</param>
/// <param name="HomeAltitudeMeters">Where the standards will be run.</param>
/// <param name="BodyKg">For the load-carriage model.</param>
/// <param name="Current">What the log says today, by metric.</param>
/// <param name="Histories">Dated readings by metric, for the metrics no trajectory covers.</param>
/// <param name="WeightPlan">The weight change the athlete has stated; null when the profile names none.</param>
public sealed record OutlookContext(
    TrajectoryParameters P,
    FitResult Fit,
    DoseSchedule Schedule,
    DoseLimits Limits,
    double HomeAltitudeMeters,
    double? BodyKg,
    IReadOnlyDictionary<string, Measurement> Current,
    IReadOnlyDictionary<string, IReadOnlyList<DatedValue>> Histories,
    LocalDate Today,
    WeightPlan? WeightPlan = null);

/// <summary>
/// Turns the selection gates from a scoreboard into a forecast: for every
/// line, the chance of clearing it by the date it is due, under a named
/// training week.
/// </summary>
/// <remarks>
/// Two engines, chosen by what the line measures. Running lines (the timed
/// runs, the rucks read through the load-carriage model, the pace at the
/// aerobic threshold) are all functions of VDOT, so each is turned into the
/// VDOT that clears it and asked of the Banister trajectory, whose fitted
/// covariance gives the probability (<see cref="Forecast"/>). A barbell
/// lift is carried forward from today's max at the rate lifters of his
/// level gain (<see cref="StrengthProgression"/>). Bodyweight and body fat
/// follow the weight plan he stated and the weigh-ins since it began
/// (<see cref="WeightForecast"/>, <see cref="BodyFatForecast"/>).
///
/// The rest (reps, holds, sprints, carries) have no model here and get no
/// forecast. Until 2026-11-24 they were projected along a straight line
/// through their dated history. That line gave his bodyweight a 14% chance
/// of 165 lb three weeks into a cut, and projected his push-ups at −1 rep.
/// A line through readings knows nothing of a plan, a ceiling, or zero.
///
/// A gate's probability is the product of its lines', which treats them as
/// independent. They are not, because a stronger athlete is stronger everywhere.
/// So the product is a floor, and it is labeled with how many of the lines
/// it covers.
///
/// Citations: <see cref="Citations.NonlinearRegression"/>,
/// <see cref="Citations.BanisterModel"/>, <see cref="Citations.PandolfLoadCarriage"/>.
/// </remarks>
public static class GateOutlook
{
    /// <summary>The chance at which a line counts as ready.</summary>
    public const double ReadyProbability = 0.80;

    /// <summary>When the workbook names no year, every goal is asked about this far out.</summary>
    public const double DefaultHorizonMonths = 12;

    /// <summary>How far ahead a line is searched for its ready date.</summary>
    public const double TrendHorizonMonths = 36;

    private const double DaysPerMonth = 30.4375;
    private static readonly double TwelveMiles = 12 * Vdot.MileMeters;

    public static LineOutlook Line(Requirement requirement, double months, OutlookContext context)
    {
        if (TargetVdot(requirement, context) is { } target)
        {
            return ByTrajectory(requirement, target, months, context);
        }

        if (IsLift(requirement.Metric)
            && context.Current.TryGetValue(requirement.Metric, out var lift) && lift.Value > 0)
        {
            return ByProgression(requirement, lift, months, context);
        }

        if (requirement.Metric is SelectionReadiness.Metrics.BodyweightLb or SelectionReadiness.Metrics.BodyFat)
        {
            return ByPlan(requirement, months, context);
        }

        var passing = context.Current.TryGetValue(requirement.Metric, out var current)
                      && Passes(requirement, current.Value);

        return passing
            ? new LineOutlook(requirement.Metric, requirement.Label, null, OutlookMethod.Held,
                "Clear today, held and not forecast: this event has no model.", current!.Value, null, null,
                requirement.Target, requirement.Unit, requirement.Comparison)
            : new LineOutlook(requirement.Metric, requirement.Label, null, OutlookMethod.None,
                NoModel, null, null, null,
                requirement.Target, requirement.Unit, requirement.Comparison);
    }

    /// <summary>What a line with no model says in place of a forecast.</summary>
    public const string NoModel =
        "No forecast: this event has no model here yet. Readings are not joined by a straight line.";

    /// <summary>What a body line says when the profile names no weight plan.</summary>
    public const string NoPlan =
        "No forecast: the profile names no weight plan. Enter its start date and pounds a week on the Plan tab.";

    private static LineOutlook ByPlan(Requirement requirement, double months, OutlookContext context)
    {
        LineOutlook Blank(string why) => new(requirement.Metric, requirement.Label, null, OutlookMethod.None,
            why, null, null, null, requirement.Target, requirement.Unit, requirement.Comparison);

        if (context.WeightPlan is not { } plan) return Blank(NoPlan);

        var weighIns = context.Histories.GetValueOrDefault(SelectionReadiness.Metrics.BodyweightLb) ?? [];
        if (WeightForecast.Fit(plan, weighIns) is not { } state)
        {
            return Blank(Text($"No forecast: no weigh-in since the plan began on {WeightForecast.Day(plan.StartedOn)}."));
        }

        var due = context.Today.PlusDays((int)Math.Round(months * DaysPerMonth));
        var isWeight = requirement.Metric == SelectionReadiness.Metrics.BodyweightLb;
        var goalWeight = isWeight ? requirement.Target : plan.GoalLb;
        var weight = WeightForecast.At(state, goalWeight, due);

        if (isWeight)
        {
            var chance = WeightForecast.ChanceReached(state, goalWeight, due);
            var reach = WeightForecast.ReachOn(state, goalWeight);
            double? ready = null;
            for (var m = 0.0; m <= TrendHorizonMonths; m += 0.25)
            {
                if (WeightForecast.ChanceReached(state, goalWeight, context.Today.PlusDays((int)Math.Round(m * DaysPerMonth))) >= ReadyProbability)
                {
                    ready = m;
                    break;
                }
            }

            var evidence = Text(
                $"From {state.LevelLb:0.0} lb on {WeightForecast.Day(state.LevelOn)} at {state.RateLbPerWeek:0.00} ± {state.RateSd:0.00} lb a week: ")
                + (reach is { } on
                    ? Text($"{goalWeight:0.#} lb on about {WeightForecast.Day(on)}, then held. ")
                    : "the rate is not a loss, so the goal is not reached. ")
                + "Holding the goal is your plan and is not forecast.";

            return new LineOutlook(requirement.Metric, requirement.Label, chance, OutlookMethod.Plan, evidence, weight.Projected, ready, null,
                requirement.Target, requirement.Unit, requirement.Comparison,
                reach is { } day ? MonthsFrom(context.Today, day) : null,
                WeightForecast.Explain(plan, state, goalWeight, due, weight, chance));
        }

        if (LatestFat(context) is not { } reading)
        {
            return Blank("No forecast: the log holds no body fat reading. Enter one with a weigh-in on the Log tab.");
        }

        var fat = BodyFatForecast.At(reading, weight);
        var fatChance = TrendProbability(requirement, fat);

        // Body fat falls only while the weight does, so it is reached when
        // the weight that gives it is, or not by this plan.
        double? fatReach = null;
        for (var m = 0.0; m <= TrendHorizonMonths; m += 0.25)
        {
            var then = WeightForecast.At(state, goalWeight, context.Today.PlusDays((int)Math.Round(m * DaysPerMonth)));
            if (BodyFatForecast.At(reading, then.Projected) <= requirement.Target)
            {
                fatReach = m;
                break;
            }
        }

        var floor = BodyFatForecast.At(reading, Math.Min(goalWeight, state.LevelLb));
        var fatEvidence = Text(
            $"From {reading.Percent:0.#}% at {reading.WeightLb:0.0} lb on {WeightForecast.Day(reading.Date)}: {BodyFatForecast.At(reading, weight.Projected):0.0}% at {weight.Projected:0.0} lb. ")
            + (floor > requirement.Target
                ? Text($"The plan stops at {goalWeight:0.#} lb, which gives {floor:0.0}%. {requirement.Target:0.#}% there needs {(floor - requirement.Target) / 100 * goalWeight:0.0} lb more fat lost at the same weight. ")
                : "")
            + "The forecast moves point for point with the reading it starts from.";

        return new LineOutlook(requirement.Metric, requirement.Label, fatChance, OutlookMethod.Plan, fatEvidence, fat.Projected, null, null,
            requirement.Target, requirement.Unit, requirement.Comparison,
            fatReach,
            BodyFatForecast.Explain(reading, weight, fat, due, requirement.Target, fatChance));
    }

    /// <summary>The newest body fat reading, with the weight logged the same day.</summary>
    internal static BodyFatReading? LatestFat(OutlookContext context)
    {
        if (!context.Histories.TryGetValue(SelectionReadiness.Metrics.BodyFat, out var fats) || fats.Count == 0) return null;
        var latest = fats.MaxBy(f => f.Date)!;
        var weights = context.Histories.GetValueOrDefault(SelectionReadiness.Metrics.BodyweightLb) ?? [];
        var weight = weights.LastOrDefault(w => w.Date == latest.Date);
        return weight is null ? null : new BodyFatReading(latest.Date, latest.Value, weight.Value);
    }

    private static double MonthsFrom(LocalDate today, LocalDate day) =>
        Math.Max(0, Period.Between(today, day, PeriodUnits.Days).Days) / DaysPerMonth;

    /// <summary>The VDOT that clears a running line, or null when the line is not a running one.</summary>
    internal static double? TargetVdot(Requirement requirement, OutlookContext context)
    {
        switch (requirement.Metric)
        {
            case SelectionReadiness.Metrics.RunTwoMile:
                return RaceVdot(2 * Vdot.MileMeters, requirement.Target, context.HomeAltitudeMeters);
            case SelectionReadiness.Metrics.RunFiveMile:
                return RaceVdot(5 * Vdot.MileMeters, requirement.Target, context.HomeAltitudeMeters);
            case SelectionReadiness.Metrics.RunTenMile:
                return RaceVdot(10 * Vdot.MileMeters, requirement.Target, context.HomeAltitudeMeters);
            case SelectionReadiness.Metrics.RunOneAndHalfMile:
                return RaceVdot(1.5 * Vdot.MileMeters, requirement.Target, context.HomeAltitudeMeters);

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
            SelectionReadiness.Metrics.RunTenMile => Altitude.AtAltitude(Vdot.MinutesFor(10 * Vdot.MileMeters, vdot) * 60, context.HomeAltitudeMeters),
            SelectionReadiness.Metrics.RunOneAndHalfMile => Altitude.AtAltitude(Vdot.MinutesFor(1.5 * Vdot.MileMeters, vdot) * 60, context.HomeAltitudeMeters),
            SelectionReadiness.Metrics.RuckTwelveMileAt45 when context.BodyKg is { } b => LoadCarriage.PredictSeconds(vdot, b, LoadCarriage.ReferenceLoadKg, TwelveMiles),
            SelectionReadiness.Metrics.RuckTwelveMileAt35 when context.BodyKg is { } b => LoadCarriage.PredictSeconds(vdot, b, 35 / BodyMass.PoundsPerKg, TwelveMiles),
            _ => null
        };

    /// <summary>The barbell lines: read forward from today's max, never through older ones.</summary>
    internal static bool IsLift(string metric) => metric is
        SelectionReadiness.Metrics.DeadliftTripleToBodyweight or SelectionReadiness.Metrics.DeadliftTripleLb
        or SelectionReadiness.Metrics.FrontSquatTripleToBodyweight or SelectionReadiness.Metrics.FrontSquatToBodyweight
        or SelectionReadiness.Metrics.FrontSquatLb or SelectionReadiness.Metrics.BenchPressToBodyweight
        or SelectionReadiness.Metrics.BenchPressLb or SelectionReadiness.Metrics.BackSquatFiveLb
        or SelectionReadiness.Metrics.BackSquatLb;

    private static LineOutlook ByProgression(Requirement requirement, Measurement current, double months, OutlookContext context)
    {
        // The best estimate he has held, a ceiling he regains fast. Today's
        // reading is the start whether or not an older one was higher.
        double? best = context.Histories.TryGetValue(requirement.Metric, out var history) && history.Count > 0
            ? history.Max(h => h.Value)
            : null;
        var age = current.On is { } on ? Math.Max(0, Period.Between(on, context.Today, PeriodUnits.Days).Days) / DaysPerMonth : 0;

        var entry = LifterEntry(requirement, context.BodyKg);
        var at = StrengthProgression.At(current.Value, best, months, age, entry);
        var probability = TrendProbability(requirement, at);

        double? ready = null;
        for (var m = 0.0; m <= TrendHorizonMonths; m += 0.25)
        {
            if (TrendProbability(requirement, StrengthProgression.At(current.Value, best, m, age, entry)) >= ReadyProbability)
            {
                ready = m;
                break;
            }
        }

        var evidence = StrengthProgression.Explain(current.Value, best, at, requirement.Unit, SelectionReadiness.Describe, entry);
        return new LineOutlook(requirement.Metric, requirement.Label, probability, OutlookMethod.Progression, evidence, at.Projected, ready, null,
            requirement.Target, requirement.Unit, requirement.Comparison,
            StrengthProgression.MonthsToReach(current.Value, best, requirement.Target, entry),
            StrengthProgression.Steps(current.Value, best, requirement.Target, months, age, at, probability, requirement.Unit, SelectionReadiness.Describe, entry));
    }

    /// <summary>
    /// The level competing lifters start at, in the line's own unit: the
    /// table's one-rep max over bodyweight, turned into pounds where the
    /// line is in pounds and into a rep max by Epley where the line is one.
    /// Null for a lift the table does not cover, or without a bodyweight.
    /// </summary>
    internal static double? LifterEntry(Requirement requirement, double? bodyKg)
    {
        (double Ratio, int Reps, bool Pounds)? line = requirement.Metric switch
        {
            SelectionReadiness.Metrics.BackSquatLb => (StrengthProgression.SquatEntry, 1, true),
            SelectionReadiness.Metrics.BackSquatFiveLb => (StrengthProgression.SquatEntry, 5, true),
            SelectionReadiness.Metrics.BenchPressLb => (StrengthProgression.BenchEntry, 1, true),
            SelectionReadiness.Metrics.BenchPressToBodyweight => (StrengthProgression.BenchEntry, 1, false),
            SelectionReadiness.Metrics.DeadliftTripleLb => (StrengthProgression.DeadliftEntry, 3, true),
            SelectionReadiness.Metrics.DeadliftTripleToBodyweight => (StrengthProgression.DeadliftEntry, 3, false),
            _ => null
        };
        if (line is not { } found) return null;

        // Epley: a set of n reps is a one-rep max over 1 + n ÷ 30.
        var ratio = found.Reps == 1 ? found.Ratio : found.Ratio / (1 + found.Reps / 30.0);
        if (!found.Pounds) return ratio;
        return bodyKg is { } kg ? ratio * kg * BodyMass.PoundsPerKg : null;
    }

    /// <summary>The chance a normal projection clears the line.</summary>
    private static double TrendProbability(Requirement requirement, (double Projected, double Sd) trend)
    {
        var margin = requirement.Comparison == Comparison.AtLeast
            ? trend.Projected - requirement.Target
            : requirement.Target - trend.Projected;
        return trend.Sd <= 0 ? (margin >= 0 ? 1 : 0) : Linear.NormalCdf(margin / trend.Sd);
    }

    private static bool Passes(Requirement requirement, double value) =>
        requirement.Comparison == Comparison.AtLeast ? value >= requirement.Target : value <= requirement.Target;

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
