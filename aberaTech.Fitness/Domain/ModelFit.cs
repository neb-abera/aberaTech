using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>One month of the athlete's own history, as the fit sees it.</summary>
/// <param name="Months">Months since the first observation.</param>
/// <param name="ObservedVdot">Fitness that month, scored in VDOT.</param>
/// <param name="Dose">The training week actually carried out that month.</param>
public sealed record FitObservation(double Months, double ObservedVdot, TrainingDose Dose);

/// <summary>A fitted parameter, with the uncertainty that comes with it.</summary>
public sealed record Estimate(string Name, double Value, double StandardError, double PriorValue)
{
    /// <summary>The interval containing the true value with the given confidence.</summary>
    public (double Low, double High) Interval(double confidence = 0.80)
    {
        var z = Linear.NormalQuantile(0.5 + confidence / 2);
        return (Value - z * StandardError, Value + z * StandardError);
    }

    /// <summary>How far the data moved this parameter off its prior, in prior standard deviations.</summary>
    public double MovedFromPrior(double priorSd) => priorSd > 0 ? (Value - PriorValue) / priorSd : 0;
}

/// <summary>What fitting the trajectory to an athlete's history produced.</summary>
/// <param name="StartVdot">Fitted fitness at the first observation.</param>
/// <param name="RatePerMonth">Fitted approach rate k.</param>
/// <param name="Responsiveness">Fitted trainability multiplier r.</param>
/// <param name="Covariance">Parameter covariance, in the order (V0, k, r).</param>
/// <param name="ResidualSd">Spread of the residuals, in VDOT.</param>
/// <param name="RSquared">Share of the variance in the observations the fit explains.</param>
/// <param name="Observations">How many months went in.</param>
/// <param name="DataWeight">
/// 0 when the answer is entirely the literature prior, 1 when it is entirely
/// this athlete's data. Reported so nobody mistakes a two-month history for a
/// personalised model.
/// </param>
/// <param name="Steps">The arithmetic, for the athlete to check.</param>
public sealed record FitResult(
    Estimate StartVdot,
    Estimate RatePerMonth,
    Estimate Responsiveness,
    double[,] Covariance,
    double ResidualSd,
    double RSquared,
    int Observations,
    double DataWeight,
    IReadOnlyList<CalculationStep> Steps)
{
    public TrajectoryParameters ToParameters(double? reclaimVdot) =>
        new(StartVdot.Value, reclaimVdot, RatePerMonth.Value, Responsiveness.Value);
}

/// <summary>
/// Fits the trajectory model to the athlete's own training history, instead of
/// asking them to trust constants fitted to somebody else.
/// </summary>
/// <remarks>
/// <b>What is fitted.</b> Three parameters: where the athlete's fitness
/// actually started (V0), how fast they close on a ceiling (k), and how much
/// ceiling their training buys them (r, the responsiveness in
/// <see cref="DoseResponse"/>). The prediction for month i is the trajectory
/// integrated through the training they actually did, so the fit is scored
/// against history rather than against a smooth curve.
///
/// <b>How.</b> Penalised nonlinear least squares by Levenberg-Marquardt. The
/// model has no closed-form derivatives — the prediction is an ODE solve — so
/// the Jacobian is central differences, which for three parameters costs six
/// integrations per iteration and is exact to about seven digits. The penalty
/// term is a Gaussian prior on each parameter centred on the literature value,
/// which is what stops four noisy months from concluding an athlete is twice
/// as trainable as anyone alive. With no data the fit returns the prior; with
/// a long history the prior washes out. <see cref="FitResult.DataWeight"/>
/// says which regime the answer is in.
///
/// <b>Uncertainty.</b> The parameter covariance is s²·(JᵀJ + Λ)⁻¹ with Λ the
/// prior precisions — the standard nonlinear-regression result, with the ridge
/// term the priors contribute. <see cref="Forecast"/> propagates it forward
/// into the bands and probabilities the athlete actually reads.
///
/// Citations: <see cref="Citations.NonlinearRegression"/>,
/// <see cref="Citations.BanisterModel"/>.
/// </remarks>
public static class ModelFit
{
    /// <summary>Prior mean and spread for each fitted parameter.</summary>
    public sealed record Priors(
        double StartVdot,
        double StartVdotSd = 1.5,
        double RatePerMonth = 0.0676,
        double RatePerMonthSd = 0.030,
        double Responsiveness = 1.0,
        double ResponsivenessSd = 0.20);

    /// <summary>Fewer months than this and a three-parameter fit is fitting noise.</summary>
    public const int MinimumObservations = 4;

    public static FitResult Fit(
        IReadOnlyList<FitObservation> observations,
        Priors priors,
        double? reclaimVdot = null)
    {
        var ordered = observations.OrderBy(o => o.Months).ToArray();
        double[] theta = [priors.StartVdot, priors.RatePerMonth, priors.Responsiveness];
        double[] priorMean = [priors.StartVdot, priors.RatePerMonth, priors.Responsiveness];
        double[] priorSd = [priors.StartVdotSd, priors.RatePerMonthSd, priors.ResponsivenessSd];

        if (ordered.Length < MinimumObservations)
        {
            return Prior(priors, priorMean, priorSd, ordered.Length);
        }

        var lambda = 1e-3;
        var cost = Cost(theta, ordered, priorMean, priorSd);
        for (var iteration = 0; iteration < 60; iteration++)
        {
            var jacobian = Jacobian(theta, ordered);
            var residuals = Residuals(theta, ordered);

            // Normal equations with the prior precisions on the diagonal, then
            // Marquardt's damping on top of that.
            var normal = new double[3, 3];
            var gradient = new double[3];
            for (var a = 0; a < 3; a++)
            {
                for (var b = 0; b < 3; b++)
                {
                    var sum = 0.0;
                    for (var i = 0; i < ordered.Length; i++) sum += jacobian[i, a] * jacobian[i, b];
                    normal[a, b] = sum;
                }

                normal[a, a] += 1.0 / (priorSd[a] * priorSd[a]);

                var g = 0.0;
                for (var i = 0; i < ordered.Length; i++) g += jacobian[i, a] * residuals[i];
                gradient[a] = g - (theta[a] - priorMean[a]) / (priorSd[a] * priorSd[a]);
            }

            var damped = (double[,])normal.Clone();
            for (var a = 0; a < 3; a++) damped[a, a] *= 1 + lambda;

            var step = Linear.Solve(damped, gradient);
            if (step is null) break;

            double[] candidate =
            [
                theta[0] + step[0],
                Math.Clamp(theta[1] + step[1], 0.005, 0.5),
                Math.Clamp(theta[2] + step[2], 0.3, 3.0)
            ];

            var candidateCost = Cost(candidate, ordered, priorMean, priorSd);
            if (candidateCost < cost)
            {
                var improvement = cost - candidateCost;
                theta = candidate;
                cost = candidateCost;
                lambda = Math.Max(lambda / 3, 1e-9);
                if (improvement < 1e-10) break;
            }
            else
            {
                lambda *= 4;
                if (lambda > 1e9) break;
            }
        }

        return Summarise(theta, ordered, priorMean, priorSd);
    }

    /// <summary>The answer when there is not enough history to say anything else.</summary>
    private static FitResult Prior(Priors priors, double[] mean, double[] sd, int observations)
    {
        var covariance = new double[3, 3];
        for (var a = 0; a < 3; a++) covariance[a, a] = sd[a] * sd[a];

        var steps = new CalculationTrace()
            .Add(
                "Model parameters",
                $"{observations} month(s) of history, fewer than the {MinimumObservations} a three-parameter fit needs",
                "literature priors used unchanged",
                Citations.NonlinearRegression.Id)
            .Steps;

        return new FitResult(
            new Estimate("Starting VDOT", mean[0], sd[0], mean[0]),
            new Estimate("Approach rate k", mean[1], sd[1], mean[1]),
            new Estimate("Responsiveness", mean[2], sd[2], mean[2]),
            covariance,
            ResidualSd: priors.StartVdotSd,
            RSquared: 0,
            Observations: observations,
            DataWeight: 0,
            Steps: steps);
    }

    private static FitResult Summarise(
        double[] theta, IReadOnlyList<FitObservation> data, double[] priorMean, double[] priorSd)
    {
        var residuals = Residuals(theta, data);
        var rss = residuals.Sum(r => r * r);
        var degreesOfFreedom = Math.Max(1, data.Count - 3);
        var variance = rss / degreesOfFreedom;
        var residualSd = Math.Sqrt(variance);

        var mean = data.Average(o => o.ObservedVdot);
        var total = data.Sum(o => (o.ObservedVdot - mean) * (o.ObservedVdot - mean));
        var rSquared = total > 0 ? Math.Clamp(1 - rss / total, 0, 1) : 0;

        var jacobian = Jacobian(theta, data);
        var normal = new double[3, 3];
        for (var a = 0; a < 3; a++)
        {
            for (var b = 0; b < 3; b++)
            {
                var sum = 0.0;
                for (var i = 0; i < data.Count; i++) sum += jacobian[i, a] * jacobian[i, b];
                normal[a, b] = sum;
            }
        }

        // How much of each parameter's precision came from the data rather
        // than from the prior: the honest answer to "is this my model or the
        // textbook's?".
        var dataPrecision = 0.0;
        var priorPrecision = 0.0;
        for (var a = 0; a < 3; a++)
        {
            dataPrecision += normal[a, a] / variance;
            priorPrecision += 1.0 / (priorSd[a] * priorSd[a]);
        }

        var penalised = (double[,])normal.Clone();
        for (var a = 0; a < 3; a++) penalised[a, a] += variance / (priorSd[a] * priorSd[a]);

        var inverse = Linear.Invert(penalised);
        var covariance = new double[3, 3];
        for (var a = 0; a < 3; a++)
        {
            for (var b = 0; b < 3; b++)
            {
                covariance[a, b] = inverse is null
                    ? (a == b ? priorSd[a] * priorSd[a] : 0)
                    : variance * inverse[a, b];
            }
        }

        var names = new[] { "Starting VDOT", "Approach rate k", "Responsiveness" };
        var estimates = new Estimate[3];
        for (var a = 0; a < 3; a++)
        {
            estimates[a] = new Estimate(names[a], theta[a], Math.Sqrt(Math.Max(0, covariance[a, a])), priorMean[a]);
        }

        var steps = new CalculationTrace()
            .Add(
                "Fitted to your history",
                Text($"{data.Count} months of imported runs, three parameters, penalised least squares"),
                Text($"R² {rSquared:0.00}, residual spread {residualSd:0.0} VDOT"),
                Citations.NonlinearRegression.Id)
            .Add(
                "Approach rate k",
                Text($"prior {priorMean[1]:0.0000} ± {priorSd[1]:0.0000} updated by your data"),
                Text($"{estimates[1].Value:0.0000} ± {estimates[1].StandardError:0.0000} per month"),
                Citations.BanisterModel.Id)
            .Add(
                "Responsiveness",
                Text($"prior {priorMean[2]:0.00} ± {priorSd[2]:0.00} updated by your data"),
                Text($"{estimates[2].Value:0.00} ± {estimates[2].StandardError:0.00} × the reference athlete"),
                Citations.NonlinearRegression.Id)
            .Steps;

        var dataWeight = dataPrecision + priorPrecision > 0
            ? dataPrecision / (dataPrecision + priorPrecision)
            : 0;

        return new FitResult(
            estimates[0], estimates[1], estimates[2],
            covariance, residualSd, rSquared, data.Count,
            Math.Clamp(dataWeight, 0, 1), steps);
    }

    /// <summary>What the model says the given month should have looked like.</summary>
    public static double Predict(double[] theta, IReadOnlyList<FitObservation> data, double months) =>
        Trajectory.VdotAt(
            new TrajectoryParameters(theta[0], null, theta[1], theta[2]), History(data), months);

    /// <summary>
    /// The dose actually trained, as a step function of month: the fit is
    /// scored against the athlete's real weeks rather than an average one.
    /// </summary>
    public static Func<double, TrainingDose> History(IReadOnlyList<FitObservation> data)
    {
        var months = data.Select(o => o.Months).ToArray();
        var doses = data.Select(o => o.Dose).ToArray();
        if (doses.Length == 0) return _ => new TrainingDose();

        return at =>
        {
            var index = 0;
            for (var i = 0; i < months.Length; i++)
            {
                if (months[i] <= at + 1e-9) index = i;
            }

            return doses[index];
        };
    }

    private static double[] Residuals(double[] theta, IReadOnlyList<FitObservation> data)
    {
        var history = History(data);
        var p = new TrajectoryParameters(theta[0], null, theta[1], theta[2]);
        return data.Select(o => o.ObservedVdot - Trajectory.VdotAt(p, history, o.Months)).ToArray();
    }

    private static double Cost(
        double[] theta, IReadOnlyList<FitObservation> data, double[] priorMean, double[] priorSd)
    {
        var rss = Residuals(theta, data).Sum(r => r * r);
        for (var a = 0; a < 3; a++)
        {
            var z = (theta[a] - priorMean[a]) / priorSd[a];
            rss += z * z;
        }

        return rss;
    }

    /// <summary>∂prediction/∂parameter by central differences.</summary>
    private static double[,] Jacobian(double[] theta, IReadOnlyList<FitObservation> data)
    {
        var jacobian = new double[data.Count, 3];
        for (var a = 0; a < 3; a++)
        {
            var step = Math.Max(1e-5, Math.Abs(theta[a]) * 1e-4);
            var up = (double[])theta.Clone();
            var down = (double[])theta.Clone();
            up[a] += step;
            down[a] -= step;

            var history = History(data);
            var pUp = new TrajectoryParameters(up[0], null, up[1], up[2]);
            var pDown = new TrajectoryParameters(down[0], null, down[1], down[2]);

            for (var i = 0; i < data.Count; i++)
            {
                var months = data[i].Months;
                jacobian[i, a] =
                    (Trajectory.VdotAt(pUp, history, months) - Trajectory.VdotAt(pDown, history, months))
                    / (2 * step);
            }
        }

        return jacobian;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

}
