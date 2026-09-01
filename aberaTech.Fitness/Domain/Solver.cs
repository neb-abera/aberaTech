using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>A lever the athlete can actually pull, or the calendar.</summary>
public enum Factor
{
    /// <summary>Weekly running hours planned.</summary>
    WeeklyHours,

    /// <summary>The share of the plan that actually happens.</summary>
    Compliance,

    /// <summary>Race weight, in kilograms.</summary>
    RaceMassKg,

    /// <summary>Weekly hours of heavy resistance work.</summary>
    StrengthHours,

    /// <summary>How far ahead the question is asked.</summary>
    Months
}

/// <summary>One complete what-if: every factor set, nothing implied.</summary>
public sealed record Scenario(
    double DistanceMeters,
    double Months,
    double WeeklyHours,
    double Compliance,
    double? RaceMassKg = null,
    double StrengthHours = 0)
{
    public double this[Factor factor] => factor switch
    {
        Factor.WeeklyHours => WeeklyHours,
        Factor.Compliance => Compliance,
        Factor.RaceMassKg => RaceMassKg ?? 0,
        Factor.StrengthHours => StrengthHours,
        Factor.Months => Months,
        _ => throw new ArgumentOutOfRangeException(nameof(factor))
    };

    public Scenario With(Factor factor, double value) => factor switch
    {
        Factor.WeeklyHours => this with { WeeklyHours = Math.Max(0, value) },
        Factor.Compliance => this with { Compliance = Math.Clamp(value, 0.01, 1) },
        Factor.RaceMassKg => this with { RaceMassKg = value },
        Factor.StrengthHours => this with { StrengthHours = Math.Max(0, value) },
        Factor.Months => this with { Months = Math.Max(0, value) },
        _ => throw new ArgumentOutOfRangeException(nameof(factor))
    };
}

/// <summary>Everything about the athlete the solver holds fixed.</summary>
/// <param name="PeakWeightKg">
/// What the athlete weighed when the lifetime best was set. Without it the
/// reclaimable peak cannot follow the race-weight factor, and moving the anchor
/// alone would shrink the reclaim runway every time the athlete planned to race
/// lighter.
/// </param>
public sealed record SolverContext(
    PosteriorSamples Posterior,
    double AnchorVdot,
    double? ReclaimVdot,
    TrainingDose CurrentDose,
    double? CurrentMassKg,
    double AltitudeMeters,
    DoseLimits Limits,
    double? PeakWeightKg = null)
{
    /// <summary>
    /// Draws to solve over. Solving is a bisection per draw, so the full cloud
    /// is wasteful: a few hundred draws pin an 80% interval to a hair.
    /// </summary>
    public const int SolveDraws = 240;

    public IReadOnlyList<ParameterDraw> Subsample(int count)
    {
        if (Posterior.Draws.Count <= count) return Posterior.Draws;

        var stride = Posterior.Draws.Count / (double)count;
        return Enumerable.Range(0, count)
            .Select(i => Posterior.Draws[(int)(i * stride)])
            .ToArray();
    }
}

/// <summary>A distribution, summarised the way the page shows it.</summary>
/// <param name="Median">The middle of the draws.</param>
/// <param name="Low">10th percentile.</param>
/// <param name="High">90th percentile.</param>
/// <param name="Impossible">Share of draws for which no answer exists at all.</param>
/// <param name="AlreadyMet">
/// Share of draws in which the factor needs no change — the target is met
/// where it already sits. Reported separately because folding "no change
/// needed" into a median makes the median mean nothing.
/// </param>
public sealed record Spread(
    double Median, double Low, double High, double Impossible = 0, double AlreadyMet = 0)
{
    public static Spread Of(IEnumerable<double?> values, double alreadyMet = 0)
    {
        var all = values.ToArray();
        var found = Statistic.Sorted(all.Where(v => v.HasValue).Select(v => v!.Value));
        var impossible = all.Length == 0 ? 0 : 1 - found.Length / (double)all.Length;

        return found.Length == 0
            ? new Spread(double.NaN, double.NaN, double.NaN, 1, alreadyMet)
            : new Spread(
                Statistic.Quantile(found, 0.5),
                Statistic.Quantile(found, 0.10),
                Statistic.Quantile(found, 0.90),
                impossible,
                alreadyMet);
    }
}

/// <summary>What one factor is worth, at the point the scenario sits on.</summary>
/// <param name="Factor">The lever.</param>
/// <param name="Value">Where it is set now.</param>
/// <param name="Elasticity">
/// The percentage change in the predicted time from a one percent change in
/// this factor — the derivative, made comparable across units.
/// </param>
/// <param name="PerUnitSeconds">Seconds of race time per unit of the factor.</param>
/// <param name="LowValue">The bottom of the range the swing was taken over.</param>
/// <param name="HighValue">The top of it.</param>
/// <param name="LowSeconds">Predicted time at the bottom.</param>
/// <param name="HighSeconds">Predicted time at the top.</param>
public sealed record FactorSensitivity(
    Factor Factor,
    double Value,
    double Elasticity,
    double PerUnitSeconds,
    double LowValue,
    double HighValue,
    double LowSeconds,
    double HighSeconds)
{
    /// <summary>How much race time this factor moves across its range.</summary>
    public double Swing => Math.Abs(HighSeconds - LowSeconds);
}

/// <summary>
/// One engine for every question the console asks: fix all the factors but
/// one, and either evaluate or solve.
/// </summary>
/// <remarks>
/// The console used to have two hard-coded directions — a plan forward to a
/// time, and a goal backward to a plan — which is two implementations of one
/// idea and no help at all when the question is "what compliance would I need"
/// or "how light would I have to be". Every relationship here is monotone in
/// its factor, so a single bisection answers all of them, and the answer is a
/// distribution rather than a number because it is solved once per posterior
/// draw.
///
/// The derivatives fall out of the same evaluation. Elasticity — the percentage
/// change in the answer per percentage change in a factor — is what makes a
/// weekly hour, a compliance point and a kilogram comparable, and comparing
/// them is the whole question of what to change next.
/// </remarks>
public static class Solver
{
    /// <summary>The predicted race time for one parameter draw, in seconds.</summary>
    public static double Evaluate(SolverContext context, ParameterDraw draw, Scenario scenario)
    {
        var anchor = context.AnchorVdot;
        if (scenario.RaceMassKg is { } target && context.CurrentMassKg is { } current
                                              && Math.Abs(target - current) > 0.01)
        {
            anchor = BodyMass.AdjustVdot(anchor, current, target);
        }

        // The peak moves with the anchor. Scaling one and not the other made
        // the race-weight factor eat its own benefit: a lighter plan raised the
        // starting fitness toward a fixed ceiling, shortening the stretch that
        // is re-earned at the retraining rate.
        var reclaim = BodyMass.AtRaceWeight(
            context.ReclaimVdot, context.PeakWeightKg, scenario.RaceMassKg ?? context.CurrentMassKg);

        var p = draw.ToParameters(reclaim, anchor);
        var effective = DoseResponse
            .Allocate(scenario.WeeklyHours * scenario.Compliance, context.Limits with { Responsiveness = draw.Responsiveness })
            .Dose with { StrengthHours = scenario.StrengthHours * scenario.Compliance };

        var vdot = Trajectory.VdotAt(p, new DoseSchedule(effective, context.CurrentDose), scenario.Months);
        return Altitude.AtAltitude(Vdot.MinutesFor(scenario.DistanceMeters, vdot) * 60, context.AltitudeMeters);
    }

    /// <summary>The predicted race time as a distribution over the posterior.</summary>
    public static Spread Predict(SolverContext context, Scenario scenario, int draws = SolverContext.SolveDraws) =>
        Spread.Of(context.Subsample(draws).Select(draw => (double?)Evaluate(context, draw, scenario)));

    /// <summary>Whether a bigger value of a factor makes the predicted time faster.</summary>
    private static bool FasterWhenLarger(Factor factor) => factor switch
    {
        Factor.RaceMassKg => false,
        _ => true
    };

    /// <summary>The range a factor is searched over when it is the unknown.</summary>
    public static (double Low, double High) Bracket(SolverContext context, Factor factor) => factor switch
    {
        Factor.WeeklyHours => (0, context.Limits.MaxStrain / TrainingDose.StrainWeight(TrainingZone.Easy)),
        Factor.Compliance => (0.01, 1.0),
        Factor.RaceMassKg => (
            Math.Max(40, (context.CurrentMassKg ?? 75) * 0.80),
            (context.CurrentMassKg ?? 75) * 1.20),
        Factor.StrengthHours => (0, 8),
        Factor.Months => (0, Forecast.MaxMonths),
        _ => throw new ArgumentOutOfRangeException(nameof(factor))
    };

    /// <summary>
    /// The value of <paramref name="unknown"/> at which the scenario hits
    /// <paramref name="targetSeconds"/>, as a distribution. Draws for which no
    /// value in the bracket gets there are reported as impossible rather than
    /// clamped to the edge, because an edge value reads as an answer.
    /// </summary>
    public static Spread Solve(
        SolverContext context,
        Scenario scenario,
        Factor unknown,
        double targetSeconds,
        int draws = SolverContext.SolveDraws)
    {
        var (low, high) = Bracket(context, unknown);
        var better = FasterWhenLarger(unknown);
        var current = scenario[unknown];
        var unchanged = 0;

        var answers = new List<double?>();
        foreach (var draw in context.Subsample(draws))
        {
            double Achieved(double value) => Evaluate(context, draw, scenario.With(unknown, value));

            var best = better ? Achieved(high) : Achieved(low);
            var worst = better ? Achieved(low) : Achieved(high);

            // A target outside what the bracket can reach has no answer, and
            // saying so beats returning the edge, which reads as one.
            if (best > targetSeconds)
            {
                answers.Add(null);
                continue;
            }

            if (worst <= targetSeconds)
            {
                // Any setting reaches it, so nothing has to change.
                unchanged++;
                answers.Add(current);
                continue;
            }

            answers.Add(FindRoot(Achieved, low, high, targetSeconds, better));
        }

        return Spread.Of(answers, answers.Count == 0 ? 0 : unchanged / (double)answers.Count);
    }

    /// <summary>
    /// The factor value at which the predicted time equals the target, by the
    /// Illinois variant of false position.
    /// </summary>
    /// <remarks>
    /// Bisection halves the bracket every step regardless of what it learns;
    /// on a smooth monotone curve like this one, interpolating through the two
    /// bracketing points converges in roughly a third as many evaluations. The
    /// Illinois modification halves the retained endpoint's value when it
    /// sticks, which is what stops the plain secant method from crawling in
    /// from one side. Every evaluation here is an ODE solve run once per
    /// posterior draw, so the factor of three is the difference between a page
    /// that responds and one that waits.
    /// </remarks>
    private static double FindRoot(
        Func<double, double> achieved, double low, double high, double target, bool fasterWhenLarger)
    {
        // Work with a function that is positive at `low` and negative at `high`
        // whichever way the factor runs, so one implementation covers both.
        double Gap(double x) => fasterWhenLarger ? achieved(x) - target : target - achieved(x);

        double a = low, b = high;
        var fa = Gap(a);
        var fb = Gap(b);
        var span = Math.Abs(high - low);

        for (var i = 0; i < 24; i++)
        {
            if (Math.Abs(fb - fa) < 1e-12) break;

            var c = b - fb * (b - a) / (fb - fa);
            if (!(c > Math.Min(a, b)) || !(c < Math.Max(a, b))) c = (a + b) / 2;

            var fc = Gap(c);
            if (Math.Abs(fc) < 1e-4 || Math.Abs(b - a) < span * 1e-9) return c;

            if (fa * fc < 0)
            {
                b = c;
                fb = fc;
                fa /= 2;
            }
            else
            {
                a = c;
                fa = fc;
                fb /= 2;
            }
        }

        return (a + b) / 2;
    }

    /// <summary>The chance of being at or under the target time on the date.</summary>
    public static double Probability(
        SolverContext context, Scenario scenario, double targetSeconds,
        int draws = SolverContext.SolveDraws) =>
        Statistic.Share(
            context.Subsample(draws).Select(draw => Evaluate(context, draw, scenario)).ToArray(),
            seconds => seconds <= targetSeconds);

    /// <summary>
    /// Every factor's local derivative and its swing across a plausible range,
    /// sorted by how much race time it moves — a tornado, in data form.
    /// </summary>
    public static IReadOnlyList<FactorSensitivity> Sensitivities(
        SolverContext context, Scenario scenario, IReadOnlyList<Factor>? factors = null)
    {
        // One representative draw: the tornado compares factors against each
        // other, and doing that inside one draw keeps the comparison clean.
        var draw = Median(context);
        var baseline = Evaluate(context, draw, scenario);

        var results = new List<FactorSensitivity>();
        foreach (var factor in factors ?? DefaultFactors(context))
        {
            var value = scenario[factor];
            if (factor == Factor.RaceMassKg && context.CurrentMassKg is null) continue;

            var nudge = Math.Max(Math.Abs(value) * 0.01, Smallest(factor));
            var up = Evaluate(context, draw, scenario.With(factor, value + nudge));
            var down = Evaluate(context, draw, scenario.With(factor, value - nudge));
            var slope = (up - down) / (2 * nudge);

            var (low, high) = Range(context, scenario, factor);
            results.Add(new FactorSensitivity(
                factor,
                value,
                Elasticity: value == 0 || baseline == 0 ? 0 : slope * value / baseline,
                PerUnitSeconds: slope,
                low,
                high,
                Evaluate(context, draw, scenario.With(factor, low)),
                Evaluate(context, draw, scenario.With(factor, high))));
        }

        return results.OrderByDescending(r => r.Swing).ToArray();
    }

    /// <summary>
    /// The predicted time over a grid of two factors, for a contour plot —
    /// which is how "what if I change both of these" gets answered without
    /// guessing at one of them.
    /// </summary>
    public static double[,] Surface(
        SolverContext context,
        Scenario scenario,
        Factor across,
        Factor down,
        (double Low, double High) acrossRange,
        (double Low, double High) downRange,
        int resolution = 32)
    {
        var draw = Median(context);
        var grid = new double[resolution, resolution];

        for (var row = 0; row < resolution; row++)
        {
            var y = downRange.Low + (downRange.High - downRange.Low) * row / (resolution - 1.0);
            for (var column = 0; column < resolution; column++)
            {
                var x = acrossRange.Low + (acrossRange.High - acrossRange.Low) * column / (resolution - 1.0);
                grid[row, column] = Evaluate(context, draw, scenario.With(across, x).With(down, y));
            }
        }

        return grid;
    }

    /// <summary>The draw closest to the posterior's centre, by its parameters.</summary>
    public static ParameterDraw Median(SolverContext context)
    {
        var samples = context.Posterior;
        return new ParameterDraw(
            samples.Summary(d => d.StartVdot).Median,
            samples.Summary(d => d.RatePerMonth).Median,
            samples.Summary(d => d.Responsiveness).Median,
            samples.Summary(d => d.PaceScale).Median,
            samples.Summary(d => d.NoiseSd).Median);
    }

    public static IReadOnlyList<Factor> DefaultFactors(SolverContext context) =>
        context.CurrentMassKg is null
            ? [Factor.WeeklyHours, Factor.Compliance, Factor.StrengthHours, Factor.Months]
            : [Factor.WeeklyHours, Factor.Compliance, Factor.RaceMassKg, Factor.StrengthHours, Factor.Months];

    /// <summary>The plausible range a tornado swings a factor over.</summary>
    public static (double Low, double High) Range(SolverContext context, Scenario scenario, Factor factor) =>
        factor switch
        {
            Factor.WeeklyHours => (
                Math.Max(0.5, scenario.WeeklyHours * 0.5),
                Math.Min(context.Limits.MaxStrain, scenario.WeeklyHours * 1.5 + 1)),
            Factor.Compliance => (Math.Max(0.2, scenario.Compliance - 0.25), 1.0),
            Factor.RaceMassKg => (
                (context.CurrentMassKg ?? 75) - 7,
                (context.CurrentMassKg ?? 75) + 4),
            Factor.StrengthHours => (0, Math.Max(2, scenario.StrengthHours * 2)),
            Factor.Months => (Math.Max(1, scenario.Months * 0.5), scenario.Months * 1.5),
            _ => throw new ArgumentOutOfRangeException(nameof(factor))
        };

    private static double Smallest(Factor factor) => factor switch
    {
        Factor.Compliance => 0.005,
        Factor.Months => 0.05,
        _ => 0.02
    };

    /// <summary>The arithmetic behind one solved answer.</summary>
    public static IReadOnlyList<CalculationStep> Explain(
        SolverContext context, Scenario scenario, Factor unknown, double targetSeconds, Spread answer)
    {
        var trace = new CalculationTrace()
            .Add(
                "Question",
                Text($"{Format.Distance(scenario.DistanceMeters)} in {Format.Clock(targetSeconds)} at month {scenario.Months:0.#}, solving for {Name(unknown)}"),
                double.IsNaN(answer.Median)
                    ? "no value reaches it"
                    : Text($"{answer.Median:0.00} ({answer.Low:0.00} to {answer.High:0.00})"),
                Citations.BanisterModel.Id)
            .Add(
                "How",
                Text($"bisection on {Name(unknown)}, run once for each of {SolverContext.SolveDraws} posterior draws"),
                Text($"{Format.Percent(1 - answer.Impossible)} of draws have an answer"),
                Citations.NonlinearRegression.Id);

        foreach (var sensitivity in Sensitivities(context, scenario).Take(3))
        {
            trace.Add(
                $"Sensitivity to {Name(sensitivity.Factor)}",
                Text($"∂(time)/∂({Name(sensitivity.Factor)}) at {sensitivity.Value:0.00}"),
                // An elasticity is already a ratio of fractional changes, so
                // it is a percentage per percentage — not a percentage of one.
                Text($"{sensitivity.PerUnitSeconds:0.0} s per unit — {Math.Abs(sensitivity.Elasticity):0.000}% of race time per 1% change"),
                Citations.BanisterModel.Id);
        }

        return trace.Steps;
    }

    public static string Name(Factor factor) => factor switch
    {
        Factor.WeeklyHours => "weekly hours",
        Factor.Compliance => "compliance",
        Factor.RaceMassKg => "race weight",
        Factor.StrengthHours => "strength hours",
        Factor.Months => "months",
        _ => factor.ToString()
    };

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
