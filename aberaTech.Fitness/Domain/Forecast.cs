using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>A projected fitness with the uncertainty around it.</summary>
/// <param name="Months">How far ahead.</param>
/// <param name="Vdot">The central projection.</param>
/// <param name="StandardDeviation">Spread of that projection.</param>
/// <param name="Low">Low end of the interval.</param>
/// <param name="High">High end of the interval.</param>
/// <param name="Confidence">Coverage of the interval, e.g. 0.80.</param>
public sealed record Band(
    double Months,
    double Vdot,
    double StandardDeviation,
    double Low,
    double High,
    double Confidence);

/// <summary>
/// Turns a fitted model into statements an athlete can act on: a projection
/// with a band around it, the chance of hitting a goal by a date, and the date
/// by which the chance reaches a given level.
/// </summary>
/// <remarks>
/// A single projected number is a prediction pretending to be a fact. The
/// parameters were estimated from a handful of noisy months and carry a
/// covariance (<see cref="ModelFit"/>); the delta method pushes it forward
/// through the model — Var[V(t)] ≈ ∇θV(t)ᵀ·Σ·∇θV(t) — and the residual spread
/// is added on top, because a future month is a new draw of the same noise
/// that made the past ones scatter. The gradient is central differences on the
/// same integration the projection uses, so the band cannot drift away from
/// the curve it belongs to.
///
/// The probability of a goal is then Φ((V(t) − target)/sd): a proper answer to
/// "will I make it", rather than the yes it always is when a model has no
/// error bars. Small numbers here are the point. A target with a 4% chance
/// deserves to be told it has a 4% chance.
///
/// Citations: <see cref="Citations.NonlinearRegression"/>,
/// <see cref="Citations.BanisterModel"/>.
/// </remarks>
public static class Forecast
{
    /// <summary>Longest horizon any question is answered over.</summary>
    public const double MaxMonths = 120;

    /// <summary>The projection at a horizon, with its interval.</summary>
    public static Band At(
        TrajectoryParameters p,
        FitResult fit,
        DoseSchedule schedule,
        double months,
        double confidence = 0.80)
    {
        var vdot = Trajectory.VdotAt(p, schedule, months);
        var sd = StandardDeviation(p, fit, schedule, months);
        var z = Linear.NormalQuantile(0.5 + confidence / 2);
        return new Band(months, vdot, sd, vdot - z * sd, vdot + z * sd, confidence);
    }

    /// <summary>
    /// The spread of the projection: parameter uncertainty pushed through the
    /// model, plus the month-to-month noise the fit measured.
    /// </summary>
    public static double StandardDeviation(
        TrajectoryParameters p, FitResult fit, DoseSchedule schedule, double months)
    {
        if (months <= 0) return 0;

        var gradient = Gradient(p, schedule, months);
        var variance = 0.0;
        for (var a = 0; a < 3; a++)
        {
            for (var b = 0; b < 3; b++)
            {
                variance += gradient[a] * fit.Covariance[a, b] * gradient[b];
            }
        }

        return Math.Sqrt(Math.Max(0, variance) + fit.ResidualSd * fit.ResidualSd);
    }

    /// <summary>∂V(t)/∂(V0, k, r) by central differences.</summary>
    private static double[] Gradient(TrajectoryParameters p, DoseSchedule schedule, double months)
    {
        double Value(TrajectoryParameters q) => Trajectory.VdotAt(q, schedule, months);

        var dStart = Math.Max(1e-5, Math.Abs(p.StartVdot) * 1e-5);
        var dRate = Math.Max(1e-7, Math.Abs(p.RatePerMonth) * 1e-4);
        var dResponse = Math.Max(1e-6, Math.Abs(p.Responsiveness) * 1e-4);

        return
        [
            (Value(p with { StartVdot = p.StartVdot + dStart })
             - Value(p with { StartVdot = p.StartVdot - dStart })) / (2 * dStart),
            (Value(p with { RatePerMonth = p.RatePerMonth + dRate })
             - Value(p with { RatePerMonth = p.RatePerMonth - dRate })) / (2 * dRate),
            (Value(p with { Responsiveness = p.Responsiveness + dResponse })
             - Value(p with { Responsiveness = p.Responsiveness - dResponse })) / (2 * dResponse)
        ];
    }

    /// <summary>The chance of being at or past <paramref name="targetVdot"/> by a date.</summary>
    public static double Probability(
        TrajectoryParameters p, FitResult fit, DoseSchedule schedule, double targetVdot, double months)
    {
        if (months <= 0) return p.StartVdot >= targetVdot ? 1 : 0;

        var vdot = Trajectory.VdotAt(p, schedule, months);
        var sd = StandardDeviation(p, fit, schedule, months);
        if (sd <= 0) return vdot >= targetVdot ? 1 : 0;

        return Linear.NormalCdf((vdot - targetVdot) / sd);
    }

    /// <summary>
    /// The first month by which the chance of holding the target reaches
    /// <paramref name="probability"/>, or null when it never does inside the
    /// horizon.
    /// </summary>
    public static double? MonthsForProbability(
        TrajectoryParameters p,
        FitResult fit,
        DoseSchedule schedule,
        double targetVdot,
        double probability)
    {
        if (probability is <= 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(probability));
        if (Probability(p, fit, schedule, targetVdot, MaxMonths) < probability) return null;
        if (Probability(p, fit, schedule, targetVdot, 0) >= probability) return 0;

        double sooner = 0, later = MaxMonths;
        for (var i = 0; i < 40; i++)
        {
            var mid = (sooner + later) / 2;
            if (Probability(p, fit, schedule, targetVdot, mid) < probability) sooner = mid; else later = mid;
        }

        return (sooner + later) / 2;
    }

    /// <summary>The arithmetic behind one band and one probability.</summary>
    public static IReadOnlyList<CalculationStep> Explain(
        TrajectoryParameters p, FitResult fit, DoseSchedule schedule, double targetVdot, double months)
    {
        var band = At(p, fit, schedule, months);
        var chance = Probability(p, fit, schedule, targetVdot, months);

        return new CalculationTrace()
            .Add(
                "Projection",
                Text($"dV/dt = k·(C − V) integrated {months:0.#} months from VDOT {p.StartVdot:0.0} at k = {p.RatePerMonth:0.0000}"),
                Text($"VDOT {band.Vdot:0.0}"),
                Citations.BanisterModel.Id)
            .Add(
                "Spread of the projection",
                Text($"√(∇V'·Σ·∇V + {fit.ResidualSd:0.00}²) with Σ from the fit of {fit.Observations} months"),
                Text($"± {band.StandardDeviation:0.0} VDOT (1 sd)"),
                Citations.NonlinearRegression.Id)
            .Add(
                Text($"{Format.Percent(band.Confidence)} interval"),
                Text($"{band.Vdot:0.0} ± {Linear.NormalQuantile(0.5 + band.Confidence / 2):0.00} × {band.StandardDeviation:0.0}"),
                Text($"VDOT {band.Low:0.0} to {band.High:0.0}"),
                Citations.NonlinearRegression.Id)
            .Add(
                "Chance of holding the target by then",
                Text($"Φ(({band.Vdot:0.0} − {targetVdot:0.0}) ÷ {band.StandardDeviation:0.0})"),
                Text($"{Format.Percent(chance)}"),
                Citations.NonlinearRegression.Id)
            .Steps;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
