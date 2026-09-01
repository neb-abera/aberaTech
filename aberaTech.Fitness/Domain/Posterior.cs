using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>How a fitness observation was obtained, which decides how much to trust it.</summary>
public enum ObservationKind
{
    /// <summary>A race or time trial: a direct measure of what the athlete can do.</summary>
    TimeTrial,

    /// <summary>A month of steady runs, scored by pace at a reference heart rate.</summary>
    NormalizedPace
}

/// <summary>One posterior draw: a complete set of model parameters.</summary>
/// <param name="StartVdot">Fitness at the first observation.</param>
/// <param name="RatePerMonth">How fast fitness closes on its ceiling.</param>
/// <param name="Responsiveness">How much ceiling the athlete's training buys.</param>
/// <param name="PaceScale">
/// The factor turning pace-at-heart-rate into VDOT. It absorbs a miscalibrated
/// treadmill, a heart-rate strap reading high, and the imperfection of the
/// normalisation itself — the things that bias a proxy but not a race.
/// </param>
/// <param name="NoiseSd">Month-to-month scatter of the pace-derived series.</param>
public sealed record ParameterDraw(
    double StartVdot,
    double RatePerMonth,
    double Responsiveness,
    double PaceScale,
    double NoiseSd)
{
    public double[] ToArray() => [StartVdot, RatePerMonth, Responsiveness, PaceScale, NoiseSd];

    public static ParameterDraw FromArray(double[] values) =>
        new(values[0], values[1], values[2], values[3], values[4]);

    public TrajectoryParameters ToParameters(double? reclaimVdot, double? startVdot = null) =>
        new(startVdot ?? StartVdot, reclaimVdot, RatePerMonth, Responsiveness);
}

/// <summary>How well the sampler explored, so a bad fit can be spotted rather than believed.</summary>
/// <param name="AcceptanceRate">Share of proposals accepted; healthy is 0.15–0.4.</param>
/// <param name="RHat">Worst between-chain agreement across parameters; healthy is under 1.01.</param>
/// <param name="EffectiveSampleSize">Worst effective sample size across parameters.</param>
/// <param name="Converged">Whether both diagnostics passed.</param>
public sealed record SamplerDiagnostics(
    double AcceptanceRate,
    double RHat,
    double EffectiveSampleSize,
    bool Converged);

/// <summary>The fitted model as a cloud of parameter sets rather than one.</summary>
public sealed record PosteriorSamples(
    IReadOnlyList<ParameterDraw> Draws,
    SamplerDiagnostics Diagnostics,
    int Observations,
    int TimeTrials,
    IReadOnlyList<CalculationStep> Steps)
{
    /// <summary>Marginal summary of one parameter across the draws.</summary>
    public (double Median, double Low, double High) Summary(
        Func<ParameterDraw, double> parameter, double confidence = 0.80)
    {
        var sorted = Statistic.Sorted(Draws.Select(parameter));
        var tail = (1 - confidence) / 2;
        return (
            Statistic.Quantile(sorted, 0.5),
            Statistic.Quantile(sorted, tail),
            Statistic.Quantile(sorted, 1 - tail));
    }
}

/// <summary>
/// The model as a posterior distribution over its parameters, sampled by
/// Markov chain Monte Carlo.
/// </summary>
/// <remarks>
/// <b>Why not the point estimate.</b> <see cref="ModelFit"/> returns one set of
/// parameters and a covariance, and <see cref="Forecast"/> pushes that forward
/// as a normal. That is the right tool when the data are plentiful and the
/// likelihood is close to quadratic. Neither is true here: one athlete, a
/// handful of noisy months, parameters that trade off against each other
/// (a high ceiling reached slowly looks much like a low one reached fast), and
/// hard bounds — responsiveness cannot be negative. Under those conditions a
/// normal approximation quietly reports symmetric intervals for a skewed
/// answer. The posterior is the honest object, and for a single athlete it is
/// affordable: a few seconds of sampling, cached until the data change.
///
/// <b>The model.</b> Latent fitness follows the trajectory ODE through the
/// training actually done. A time trial observes it directly with small noise.
/// A month of steady runs observes it through pace at a reference heart rate,
/// which is a <i>proxy</i>: it carries a scale error, from a treadmill belt
/// that lies, a strap that reads high, or the normalisation itself. That scale
/// is a parameter, so the model can discover that the proxy series runs a few
/// percent off and stop treating its level as gospel — a race pins the level,
/// the proxy pins the shape.
///
/// <b>The sampler.</b> Adaptive random-walk Metropolis: four chains from
/// dispersed starts, proposal covariance adapted to the chain's own during
/// burn-in (Haario, Saksman &amp; Tamminen) and frozen afterwards so the chain
/// is a proper Markov chain. Convergence is reported, not assumed —
/// split-R̂ and effective sample size ship with every fit, and the page says so
/// when they fail.
///
/// Citations: <see cref="Citations.NonlinearRegression"/>,
/// <see cref="Citations.BanisterModel"/>.
/// </remarks>
public static class Posterior
{
    /// <summary>Noise on a time trial, in VDOT: pacing, wind and the day.</summary>
    public const double TimeTrialSd = 0.45;

    private const int Chains = 4;
    private const int BurnIn = 1200;
    private const int PerChain = 1200;
    private const int Thin = 4;

    /// <summary>The acceptance rate a random-walk chain is happiest at.</summary>
    private const double TargetAcceptance = 0.25;

    /// <summary>Prior means and spreads, all of them beliefs held before the data.</summary>
    public sealed record Priors(
        double StartVdot,
        double StartVdotSd = 2.0,
        double RatePerMonth = 0.0676,
        double RatePerMonthSd = 0.030,
        double Responsiveness = 1.0,
        double ResponsivenessSd = 0.20,
        double PaceScale = 1.0,
        double PaceScaleSd = 0.06,
        double NoiseSd = 1.2,
        double NoiseSdSd = 0.8);

    public static PosteriorSamples Sample(
        IReadOnlyList<FitObservation> observations,
        Priors priors,
        double? reclaimVdot = null)
    {
        var data = observations.OrderBy(o => o.Months).ToArray();
        var history = ModelFit.History(data);
        var trials = data.Count(o => o.Kind == ObservationKind.TimeTrial);
        var horizons = data.Select(o => o.Months).ToArray();

        double LogPosterior(double[] theta)
        {
            var prior = LogPrior(theta, priors);
            if (double.IsNegativeInfinity(prior)) return prior;

            var draw = ParameterDraw.FromArray(theta);
            var predicted = Trajectory.VdotSeries(draw.ToParameters(reclaimVdot), history, horizons);
            var total = prior;

            for (var i = 0; i < data.Length; i++)
            {
                var trial = data[i].Kind == ObservationKind.TimeTrial;
                var observed = trial ? data[i].ObservedVdot : draw.PaceScale * data[i].ObservedVdot;
                var sd = trial ? TimeTrialSd : draw.NoiseSd;

                var z = (observed - predicted[i]) / sd;
                total += -0.5 * z * z - Math.Log(sd);
            }

            return total;
        }

        double[] start =
        [
            priors.StartVdot, priors.RatePerMonth, priors.Responsiveness,
            priors.PaceScale, priors.NoiseSd
        ];

        // A point fit is a cheap, much better starting position than the prior
        // mean, so the chains spend their burn-in exploring rather than walking.
        if (data.Length >= ModelFit.MinimumObservations)
        {
            var point = ModelFit.Fit(data, new ModelFit.Priors(
                priors.StartVdot, priors.StartVdotSd,
                priors.RatePerMonth, priors.RatePerMonthSd,
                priors.Responsiveness, priors.ResponsivenessSd));
            start = [point.StartVdot.Value, point.RatePerMonth.Value, point.Responsiveness.Value,
                priors.PaceScale, Math.Max(0.2, point.ResidualSd)];
        }

        double[] scale =
        [
            priors.StartVdotSd, priors.RatePerMonthSd, priors.ResponsivenessSd,
            priors.PaceScaleSd, priors.NoiseSdSd
        ];

        var chains = new List<double[]>[Chains];
        var accepted = 0;
        var proposed = 0;

        for (var chain = 0; chain < Chains; chain++)
        {
            var rng = new Rng((ulong)(0x5EED_0000 + chain));
            var position = (double[])start.Clone();

            // Disperse the starts, so agreement between chains means something.
            for (var i = 0; i < position.Length; i++)
            {
                position[i] += (chain == 0 ? 0 : rng.Normal(0, scale[i] * 0.5));
            }

            var (draws, chainAccepted, chainProposed) = Run(position, scale, LogPosterior, rng);
            chains[chain] = draws;
            accepted += chainAccepted;
            proposed += chainProposed;
        }

        var samples = chains
            .SelectMany(chain => chain)
            .Select(ParameterDraw.FromArray)
            .ToArray();

        var diagnostics = Diagnose(chains, accepted / (double)proposed);
        var steps = Explain(samples, diagnostics, data.Length, trials, priors);

        return new PosteriorSamples(samples, diagnostics, data.Length, trials, steps);
    }

    /// <summary>One chain: adaptive during burn-in, fixed proposal afterwards.</summary>
    /// <remarks>
    /// Haario, Saksman and Tamminen's adaptive Metropolis. The parameters of
    /// this model trade off hard against each other — a fast approach to a low
    /// ceiling looks much like a slow approach to a high one — so a proposal
    /// that moves each parameter independently walks across the ridge instead
    /// of along it and is rejected almost every time. The proposal is therefore
    /// shaped by the Cholesky factor of the chain's own covariance, with a
    /// global size adapted towards a healthy acceptance rate. Both stop at the
    /// end of burn-in: a chain that never stops adapting is not a Markov chain,
    /// and its draws are not from the posterior.
    /// </remarks>
    private static (List<double[]> Draws, int Accepted, int Proposed) Run(
        double[] start, double[] scale, Func<double[], double> logPosterior, Rng rng)
    {
        var dimensions = start.Length;
        var position = (double[])start.Clone();
        var current = logPosterior(position);

        if (double.IsNegativeInfinity(current))
        {
            // A dispersed start can land outside the prior's support; fall back
            // to the undispersed one rather than sampling from nowhere.
            position = (double[])start.Clone();
            position[1] = Math.Clamp(position[1], 0.006, 0.49);
            position[2] = Math.Clamp(position[2], 0.31, 2.99);
            position[3] = Math.Clamp(position[3], 0.76, 1.24);
            position[4] = Math.Clamp(position[4], 0.11, 5.9);
            current = logPosterior(position);
        }

        // Rule-of-thumb scaling for random-walk Metropolis in d dimensions.
        var stride = 2.38 / Math.Sqrt(dimensions);
        var factor = Diagonal(scale.Select(s => s * 0.5).ToArray());
        var globalScale = 1.0;

        var kept = new List<double[]>();
        var seen = new List<double[]>();
        var accepted = 0;
        var proposed = 0;
        var acceptedSinceAdapting = 0;
        var proposedSinceAdapting = 0;

        for (var iteration = 0; iteration < BurnIn + PerChain * Thin; iteration++)
        {
            var noise = new double[dimensions];
            for (var i = 0; i < dimensions; i++) noise[i] = rng.Normal();

            var candidate = new double[dimensions];
            for (var i = 0; i < dimensions; i++)
            {
                var move = 0.0;
                for (var j = 0; j <= i; j++) move += factor[i, j] * noise[j];
                candidate[i] = position[i] + globalScale * stride * move;
            }

            var proposal = logPosterior(candidate);
            var sampling = iteration >= BurnIn;
            if (sampling) proposed++;
            proposedSinceAdapting++;

            if (proposal - current > Math.Log(Math.Max(rng.Uniform(), 1e-300)))
            {
                position = candidate;
                current = proposal;
                if (sampling) accepted++;
                acceptedSinceAdapting++;
            }

            if (iteration < BurnIn)
            {
                seen.Add((double[])position.Clone());

                if (iteration > 250 && iteration % 100 == 0)
                {
                    // Shape from the second half of what has been seen, so the
                    // walk in from the starting point does not inflate it.
                    var recent = seen.Skip(seen.Count / 2).ToArray();
                    var shaped = Linear.Cholesky(Covariance(recent, scale));
                    if (shaped is not null) factor = shaped;

                    var rate = acceptedSinceAdapting / (double)proposedSinceAdapting;
                    globalScale = Math.Clamp(
                        globalScale * Math.Exp((rate - TargetAcceptance) * 1.5), 0.02, 20);
                    acceptedSinceAdapting = 0;
                    proposedSinceAdapting = 0;
                }
            }
            else if ((iteration - BurnIn) % Thin == 0)
            {
                kept.Add((double[])position.Clone());
            }
        }

        return (kept, accepted, proposed);
    }

    /// <summary>Sample covariance of the draws so far, nudged to stay invertible.</summary>
    private static double[,] Covariance(IReadOnlyList<double[]> draws, double[] scale)
    {
        var dimensions = scale.Length;
        var means = new double[dimensions];
        foreach (var draw in draws)
        {
            for (var i = 0; i < dimensions; i++) means[i] += draw[i] / draws.Count;
        }

        var covariance = new double[dimensions, dimensions];
        foreach (var draw in draws)
        {
            for (var i = 0; i < dimensions; i++)
            {
                for (var j = 0; j < dimensions; j++)
                {
                    covariance[i, j] += (draw[i] - means[i]) * (draw[j] - means[j]) / Math.Max(1, draws.Count - 1);
                }
            }
        }

        // A ridge keeps the factorisation defined when a parameter has barely
        // moved, which happens early and whenever the data say nothing about it.
        for (var i = 0; i < dimensions; i++)
        {
            covariance[i, i] += scale[i] * scale[i] * 1e-4;
        }

        return covariance;
    }

    private static double[,] Diagonal(double[] values)
    {
        var matrix = new double[values.Length, values.Length];
        for (var i = 0; i < values.Length; i++) matrix[i, i] = values[i];
        return matrix;
    }

    /// <summary>Log prior density, and the bounds outside which a draw is impossible.</summary>
    private static double LogPrior(double[] theta, Priors priors)
    {
        if (theta[1] is < 0.005 or > 0.5) return double.NegativeInfinity;
        if (theta[2] is < 0.3 or > 3.0) return double.NegativeInfinity;
        if (theta[3] is < 0.75 or > 1.25) return double.NegativeInfinity;
        if (theta[4] is < 0.1 or > 6.0) return double.NegativeInfinity;
        if (theta[0] is < 20 or > 85) return double.NegativeInfinity;

        double Term(double value, double mean, double sd)
        {
            var z = (value - mean) / sd;
            return -0.5 * z * z;
        }

        return Term(theta[0], priors.StartVdot, priors.StartVdotSd)
               + Term(theta[1], priors.RatePerMonth, priors.RatePerMonthSd)
               + Term(theta[2], priors.Responsiveness, priors.ResponsivenessSd)
               + Term(theta[3], priors.PaceScale, priors.PaceScaleSd)
               + Term(theta[4], priors.NoiseSd, priors.NoiseSdSd);
    }

    /// <summary>Split-R̂ and effective sample size, over the worst parameter.</summary>
    private static SamplerDiagnostics Diagnose(IReadOnlyList<List<double[]>> chains, double acceptance)
    {
        var dimensions = chains[0][0].Length;
        var worstRHat = 1.0;
        var worstEss = double.MaxValue;

        for (var i = 0; i < dimensions; i++)
        {
            var series = chains.Select(chain => chain.Select(draw => draw[i]).ToArray()).ToArray();
            worstRHat = Math.Max(worstRHat, RHat(series));
            worstEss = Math.Min(worstEss, EffectiveSampleSize(series));
        }

        return new SamplerDiagnostics(
            acceptance,
            worstRHat,
            worstEss,
            worstRHat < 1.05 && worstEss > 200 && acceptance is > 0.05 and < 0.7);
    }

    /// <summary>Gelman-Rubin: between-chain variance against within-chain.</summary>
    private static double RHat(IReadOnlyList<double[]> chains)
    {
        var n = chains.Min(c => c.Length);
        if (n < 4) return double.PositiveInfinity;

        var means = chains.Select(c => c.Take(n).Average()).ToArray();
        var within = chains
            .Select(c =>
            {
                var mean = c.Take(n).Average();
                return c.Take(n).Sum(v => (v - mean) * (v - mean)) / (n - 1);
            })
            .Average();

        if (within <= 0) return 1;

        var grand = means.Average();
        var between = n * means.Sum(m => (m - grand) * (m - grand)) / (means.Length - 1);
        var estimate = ((n - 1) * within + between) / n;

        return Math.Sqrt(estimate / within);
    }

    /// <summary>Draws worth this many independent ones, from the autocorrelation.</summary>
    private static double EffectiveSampleSize(IReadOnlyList<double[]> chains)
    {
        var pooled = chains.SelectMany(c => c).ToArray();
        var n = pooled.Length;
        if (n < 10) return n;

        var mean = pooled.Average();
        var variance = pooled.Sum(v => (v - mean) * (v - mean)) / n;
        if (variance <= 0) return n;

        var sum = 0.0;
        for (var lag = 1; lag < Math.Min(n / 4, 400); lag++)
        {
            var covariance = 0.0;
            for (var i = 0; i < n - lag; i++)
            {
                covariance += (pooled[i] - mean) * (pooled[i + lag] - mean);
            }

            var rho = covariance / (n - lag) / variance;
            if (rho <= 0.02) break;
            sum += rho;
        }

        return n / (1 + 2 * sum);
    }

    private static IReadOnlyList<CalculationStep> Explain(
        IReadOnlyList<ParameterDraw> draws,
        SamplerDiagnostics diagnostics,
        int observations,
        int trials,
        Priors priors)
    {
        var trace = new CalculationTrace();
        var samples = new PosteriorSamples(draws, diagnostics, observations, trials, []);

        void Parameter(string label, Func<ParameterDraw, double> pick, string unit, string format)
        {
            var (median, low, high) = samples.Summary(pick);
            trace.Add(
                label,
                Text($"{draws.Count} posterior draws, 80% of them between"),
                Text($"{median.ToString(format, CultureInfo.InvariantCulture)}{unit} ({low.ToString(format, CultureInfo.InvariantCulture)} to {high.ToString(format, CultureInfo.InvariantCulture)})"),
                Citations.NonlinearRegression.Id);
        }

        trace.Add(
            "Inference",
            Text($"{observations} observations ({trials} time trial(s)), {Chains} chains × {PerChain} kept draws"),
            diagnostics.Converged
                ? Text($"converged — R̂ {diagnostics.RHat:0.000}, effective sample size {diagnostics.EffectiveSampleSize:0}")
                : Text($"NOT converged — R̂ {diagnostics.RHat:0.000}, effective sample size {diagnostics.EffectiveSampleSize:0}; treat the intervals as indicative"),
            Citations.NonlinearRegression.Id);

        Parameter("Approach rate k", d => d.RatePerMonth, "/month", "0.0000");
        Parameter("Responsiveness", d => d.Responsiveness, "× reference", "0.00");
        Parameter("Pace-proxy scale", d => d.PaceScale, "× VDOT", "0.000");
        Parameter("Month-to-month noise", d => d.NoiseSd, " VDOT", "0.00");

        if (trials == 0)
        {
            trace.Add(
                "No time trial in the data",
                Text($"the level rests entirely on the prior of VDOT {priors.StartVdot:0.0} ± {priors.StartVdotSd:0.0}"),
                "intervals below are as wide as that prior — a measured race is the fix",
                Citations.DanielsVdot.Id);
        }

        return trace.Steps;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
