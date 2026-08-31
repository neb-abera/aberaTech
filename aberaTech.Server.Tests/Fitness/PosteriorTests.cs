using System.Diagnostics;
using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The posterior, which is the object every interval and probability on the
/// page is drawn from. If it does not recover parameters it generated itself,
/// nothing downstream means anything.
/// </summary>
public sealed class PosteriorTests(ITestOutputHelper output)
{
    private static IReadOnlyList<FitObservation> Synthetic(
        double startVdot,
        double rate,
        double responsiveness,
        double hours,
        int months,
        double noise = 0,
        double paceScale = 1.0,
        ulong seed = 7)
    {
        var truth = new TrajectoryParameters(startVdot, null, rate, responsiveness);
        var dose = DoseResponse.Allocate(hours, truth.Limits()).Dose;
        var rng = new Rng(seed);

        return Enumerable.Range(0, months)
            .Select(m => new FitObservation(
                m,
                (Trajectory.VdotAt(truth, dose, m) + rng.Normal(0, noise)) / paceScale,
                dose))
            .ToArray();
    }

    [Fact]
    public void Its_predictions_are_calibrated_even_where_its_parameters_are_not()
    {
        // The parameters lie on a ridge — a proxy read low, from a low start,
        // approached slowly fits about as well as an accurate one from a high
        // start approached fast — so the marginals lean on their priors and
        // are not the thing to test. Every combination on that ridge agrees
        // about the future, and the future is what the athlete is buying.
        var covered = 0;
        const int trials = 8;

        for (var seed = 1UL; seed <= trials; seed++)
        {
            var truth = new TrajectoryParameters(39, null, 0.09, 1.15);
            var dose = DoseResponse.Allocate(7, truth.Limits()).Dose;
            var data = Synthetic(39, 0.09, 1.15, hours: 7, months: 18, noise: 0.5, seed: seed);

            var posterior = Posterior.Sample(data, new Posterior.Priors(data[0].ObservedVdot));
            var ahead = Statistic.Sorted(
                posterior.Draws.Select(d => Trajectory.VdotAt(d.ToParameters(null), dose, 30)));

            var low = Statistic.Quantile(ahead, 0.10);
            var high = Statistic.Quantile(ahead, 0.90);
            var actual = Trajectory.VdotAt(truth, dose, 30);

            output.WriteLine($"seed {seed}: truth {actual:0.0}, 80% interval {low:0.0}-{high:0.0}");
            if (actual >= low && actual <= high) covered++;
        }

        // An 80% interval that covers the truth in every one of eight draws is
        // as consistent with calibration as one that misses once or twice; a
        // bar of six catches a genuinely overconfident model.
        Assert.True(covered >= 6, $"covered {covered} of {trials}");
    }

    [Fact]
    public void Time_trials_sharpen_the_prediction_the_proxy_alone_cannot()
    {
        var truth = new TrajectoryParameters(39, null, 0.09, 1.15);
        var dose = DoseResponse.Allocate(7, truth.Limits()).Dose;
        var proxy = Synthetic(39, 0.09, 1.15, 7, 18, noise: 0.5);

        var raced = proxy
            .Append(new FitObservation(0, Trajectory.VdotAt(truth, dose, 0), dose, ObservationKind.TimeTrial))
            .Append(new FitObservation(17, Trajectory.VdotAt(truth, dose, 17), dose, ObservationKind.TimeTrial))
            .OrderBy(o => o.Months)
            .ToArray();

        double Width(IReadOnlyList<FitObservation> data)
        {
            var posterior = Posterior.Sample(data, new Posterior.Priors(proxy[0].ObservedVdot));
            var ahead = Statistic.Sorted(
                posterior.Draws.Select(d => Trajectory.VdotAt(d.ToParameters(null), dose, 30)));
            return Statistic.Quantile(ahead, 0.9) - Statistic.Quantile(ahead, 0.1);
        }

        var withoutRacing = Width(proxy);
        var withRacing = Width(raced);

        output.WriteLine($"80% width at +30 months: proxy only {withoutRacing:0.00}, with two trials {withRacing:0.00}");
        Assert.True(withRacing < withoutRacing * 0.75);
    }

    [Fact]
    public void Reports_when_it_did_not_converge_rather_than_pretending()
    {
        var posterior = Posterior.Sample(
            Synthetic(39, 0.09, 1.15, 7, 12, noise: 0.4), new Posterior.Priors(38));

        output.WriteLine(
            $"acceptance {posterior.Diagnostics.AcceptanceRate:0.00}, "
            + $"R-hat {posterior.Diagnostics.RHat:0.000}, "
            + $"ESS {posterior.Diagnostics.EffectiveSampleSize:0}");

        Assert.InRange(posterior.Diagnostics.AcceptanceRate, 0.05, 0.7);
        Assert.True(posterior.Diagnostics.RHat < 1.05, $"R-hat {posterior.Diagnostics.RHat}");
        Assert.True(posterior.Diagnostics.EffectiveSampleSize > 200);
        Assert.Contains(posterior.Steps, s => s.Value.Contains("converged"));
    }

    [Fact]
    public void The_same_data_always_gives_the_same_posterior()
    {
        // An answer that changes on a refresh is an answer nobody can check.
        var data = Synthetic(39, 0.09, 1.15, 7, 14, noise: 0.5);
        var first = Posterior.Sample(data, new Posterior.Priors(38));
        var second = Posterior.Sample(data, new Posterior.Priors(38));

        Assert.Equal(first.Draws.Count, second.Draws.Count);
        Assert.Equal(
            first.Summary(d => d.RatePerMonth).Median,
            second.Summary(d => d.RatePerMonth).Median,
            precision: 12);
    }

    [Fact]
    public void With_no_data_the_posterior_is_the_prior()
    {
        var posterior = Posterior.Sample([], new Posterior.Priors(StartVdot: 37));
        var rate = posterior.Summary(d => d.RatePerMonth);

        Assert.Equal(0.0676, rate.Median, precision: 2);
        // An 80% normal interval is ±1.28 sd, so ±0.038 on a 0.03 spread.
        Assert.InRange(rate.High - rate.Low, 0.055, 0.095);
        Assert.Contains(posterior.Steps, s => s.Label == "No time trial in the data");
    }

    [Fact]
    public void More_data_narrows_the_posterior()
    {
        var thin = Posterior.Sample(Synthetic(39, 0.09, 1.15, 7, 5, 0.5), new Posterior.Priors(38));
        var thick = Posterior.Sample(Synthetic(39, 0.09, 1.15, 7, 30, 0.5), new Posterior.Priors(38));

        var thinWidth = Width(thin.Summary(d => d.Responsiveness));
        var thickWidth = Width(thick.Summary(d => d.Responsiveness));

        output.WriteLine($"responsiveness interval: 5 months {thinWidth:0.000}, 30 months {thickWidth:0.000}");
        Assert.True(thickWidth < thinWidth);
    }

    [Fact]
    public void A_biased_pace_proxy_is_discovered_rather_than_believed()
    {
        // Every run on a treadmill reading 4% fast. A time trial disagrees with
        // the proxy, and the scale parameter is what absorbs the difference.
        var truth = new TrajectoryParameters(40, null, 0.09, 1.1);
        var dose = DoseResponse.Allocate(7, truth.Limits()).Dose;

        var data = Enumerable.Range(0, 18)
            .Select(m => new FitObservation(m, Trajectory.VdotAt(truth, dose, m) / 0.96, dose))
            .Append(new FitObservation(
                17, Trajectory.VdotAt(truth, dose, 17), dose, ObservationKind.TimeTrial))
            .ToArray();

        var posterior = Posterior.Sample(data, new Posterior.Priors(StartVdot: 40));
        var scale = posterior.Summary(d => d.PaceScale);

        output.WriteLine($"pace scale {scale.Median:0.000} ({scale.Low:0.000}-{scale.High:0.000})");
        Assert.True(scale.Median < 1.0);
        Assert.InRange(0.96, scale.Low, scale.High);
    }

    [Fact]
    public void A_fit_is_fast_enough_to_sit_behind_a_page()
    {
        var data = Synthetic(39, 0.09, 1.15, 7, 24, 0.5);

        var clock = Stopwatch.StartNew();
        var posterior = Posterior.Sample(data, new Posterior.Priors(38));
        clock.Stop();

        output.WriteLine($"{posterior.Draws.Count} draws over {data.Count} months in {clock.ElapsedMilliseconds} ms");
        Assert.True(clock.ElapsedMilliseconds < 20_000, $"took {clock.ElapsedMilliseconds} ms");
    }

    private static double Width((double Median, double Low, double High) summary) =>
        summary.High - summary.Low;
}
