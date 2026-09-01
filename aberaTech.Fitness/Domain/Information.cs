using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>What one future measurement would be worth.</summary>
/// <param name="AtMonths">When the measurement would be taken.</param>
/// <param name="Kind">What kind of measurement.</param>
/// <param name="WidthBefore">Width of the 80% prediction interval today, in seconds.</param>
/// <param name="WidthAfter">Expected width once the measurement is in.</param>
/// <param name="Reduction">The share of the interval it removes.</param>
public sealed record MeasurementValue(
    double AtMonths,
    ObservationKind Kind,
    double WidthBefore,
    double WidthAfter,
    double Reduction);

/// <summary>
/// Which measurement to go and take next, decided by how much it would narrow
/// the answer rather than by habit.
/// </summary>
/// <remarks>
/// The model's honest problem is not its mathematics, it is that it is being
/// asked to predict one athlete from a thin, indirect record. That is fixable,
/// and this says what fixing it is worth before the effort is spent: a time
/// trial in two months, or in six, or another month of ordinary logged runs —
/// each has a price in effort and a value in narrowed uncertainty, and only
/// one of those was ever visible.
///
/// The calculation is the standard preposterior one. For a candidate
/// measurement, draw the value it might return from the current posterior
/// predictive, reweight the existing draws by how well each explains that
/// value, and measure the spread of the prediction under those weights. Doing
/// it by reweighting rather than by re-running the sampler is what makes it
/// cheap enough to offer for every candidate at once: the draws are already
/// there, and importance weights are one exponential each.
///
/// Citation: <see cref="Citations.NonlinearRegression"/>.
/// </remarks>
public static class Information
{
    /// <summary>Hypothetical observations averaged over, per candidate.</summary>
    private const int Simulations = 9;

    /// <summary>
    /// What a measurement at <paramref name="atMonths"/> would do to the 80%
    /// interval on the race predicted at <paramref name="horizonMonths"/>.
    /// </summary>
    public static MeasurementValue Value(
        SolverContext context,
        Scenario scenario,
        double atMonths,
        double horizonMonths,
        ObservationKind kind = ObservationKind.TimeTrial)
    {
        if (atMonths < 0) throw new ArgumentOutOfRangeException(nameof(atMonths));
        if (horizonMonths < atMonths) throw new ArgumentOutOfRangeException(nameof(horizonMonths));

        var draws = context.Subsample(SolverContext.SolveDraws);

        // One pass per draw gives both the fitness the measurement would see
        // and the race time the athlete is asking about.
        var atMeasurement = new double[draws.Count];
        var atHorizon = new double[draws.Count];

        for (var i = 0; i < draws.Count; i++)
        {
            atMeasurement[i] = Fitness(context, draws[i], scenario, atMonths);
            atHorizon[i] = Solver.Evaluate(context, draws[i], scenario with { Months = horizonMonths });
        }

        var before = Width(atHorizon, Enumerable.Repeat(1.0, draws.Count).ToArray());

        // The measurement noise is what makes a proxy month worth less than a
        // race: the same fitness, observed through a wider lens.
        var noise = kind == ObservationKind.TimeTrial
            ? Posterior.TimeTrialSd
            : Math.Max(0.6, context.Posterior.Summary(d => d.NoiseSd).Median);

        var sorted = Statistic.Sorted(atMeasurement);
        var after = 0.0;

        for (var s = 0; s < Simulations; s++)
        {
            // Sweep the plausible values the measurement could return rather
            // than assuming it confirms what is already believed.
            var hypothetical = Statistic.Quantile(sorted, (s + 0.5) / Simulations);

            var weights = new double[draws.Count];
            for (var i = 0; i < draws.Count; i++)
            {
                var z = (hypothetical - atMeasurement[i]) / noise;
                weights[i] = Math.Exp(-0.5 * z * z);
            }

            after += Width(atHorizon, weights) / Simulations;
        }

        return new MeasurementValue(
            atMonths, kind, before, after,
            before <= 0 ? 0 : Math.Clamp(1 - after / before, 0, 1));
    }

    /// <summary>The candidates worth comparing, best first.</summary>
    public static IReadOnlyList<MeasurementValue> Options(
        SolverContext context, Scenario scenario, IReadOnlyList<double>? months = null)
    {
        var candidates = months ?? [0, 2, 4, 8];
        return candidates
            .Where(m => m <= scenario.Months)
            .SelectMany(m => new[]
            {
                Value(context, scenario, m, scenario.Months),
                Value(context, scenario, m, scenario.Months, ObservationKind.NormalizedPace)
            })
            .OrderByDescending(v => v.Reduction)
            .ToArray();
    }

    /// <summary>The latent fitness a draw implies at a month, before scoring it as a race.</summary>
    private static double Fitness(
        SolverContext context, ParameterDraw draw, Scenario scenario, double months)
    {
        var effective = DoseResponse
            .Allocate(
                scenario.WeeklyHours * scenario.Compliance,
                context.Limits with { Responsiveness = draw.Responsiveness })
            .Dose with { StrengthHours = scenario.StrengthHours * scenario.Compliance };

        return Trajectory.VdotAt(
            draw.ToParameters(context.ReclaimVdot, context.AnchorVdot),
            new DoseSchedule(effective, context.CurrentDose),
            months);
    }

    /// <summary>The weighted 10th-to-90th spread of a set of draws.</summary>
    private static double Width(IReadOnlyList<double> values, IReadOnlyList<double> weights)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToArray();
        var total = order.Sum(i => weights[i]);
        if (total <= 0) return 0;

        double Quantile(double probability)
        {
            var running = 0.0;
            foreach (var i in order)
            {
                running += weights[i] / total;
                if (running >= probability) return values[i];
            }

            return values[order[^1]];
        }

        return Quantile(0.9) - Quantile(0.1);
    }

    /// <summary>The arithmetic behind a recommendation to go and measure something.</summary>
    public static IReadOnlyList<CalculationStep> Explain(IReadOnlyList<MeasurementValue> options)
    {
        var trace = new CalculationTrace();
        foreach (var option in options.Take(4))
        {
            var what = option.Kind == ObservationKind.TimeTrial
                ? "a time trial"
                : "a month of logged runs";
            trace.Add(
                option.AtMonths <= 0.01 ? $"{what}, now" : $"{what}, in {option.AtMonths:0.#} months",
                Text($"80% interval {option.WidthBefore / 60:0.0} min wide, expected {option.WidthAfter / 60:0.0} min after"),
                Text($"cuts the uncertainty by {Format.Percent(option.Reduction)}"),
                Citations.NonlinearRegression.Id);
        }

        return trace.Steps;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
