using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The fit and the uncertainty that comes out of it. A model that reports a
/// date without a spread is claiming a precision it has not got, so these are
/// the tests that keep the error bars honest.
/// </summary>
public sealed class ModelFitTests
{
    private static IReadOnlyList<FitObservation> Synthetic(
        double startVdot, double rate, double responsiveness, double hours, int months)
    {
        var truth = new TrajectoryParameters(startVdot, null, rate, responsiveness);
        var dose = DoseResponse.Allocate(hours, truth.Limits()).Dose;
        return Enumerable.Range(0, months)
            .Select(m => new FitObservation(m, Trajectory.VdotAt(truth, dose, m), dose))
            .ToArray();
    }

    [Fact]
    public void Recovers_the_parameters_that_generated_the_data()
    {
        // The priors are deliberately wrong, so this tests the optimiser and
        // the shrinkage together: fourteen clean months pull every parameter
        // most of the way to the truth, and the prior keeps the last fraction.
        var data = Synthetic(startVdot: 39, rate: 0.09, responsiveness: 1.15, hours: 7, months: 14);
        var fit = ModelFit.Fit(data, new ModelFit.Priors(StartVdot: 37));

        Assert.InRange(fit.StartVdot.Value, 38.5, 39.1);
        Assert.InRange(fit.RatePerMonth.Value, 0.080, 0.098);
        Assert.InRange(fit.Responsiveness.Value, 1.08, 1.20);
        Assert.True(fit.RSquared > 0.99);
        Assert.True(fit.ResidualSd < 0.3);
        Assert.True(fit.DataWeight > 0.9);
    }

    [Fact]
    public void Too_little_history_returns_the_literature_prior_and_says_so()
    {
        var fit = ModelFit.Fit(
            Synthetic(39, 0.09, 1.15, 7, 2), new ModelFit.Priors(StartVdot: 37));

        Assert.Equal(0, fit.DataWeight);
        Assert.Equal(37, fit.StartVdot.Value);
        Assert.Equal(0.0676, fit.RatePerMonth.Value, precision: 6);
        Assert.Contains(fit.Steps, s => s.Value.Contains("literature priors"));
    }

    [Fact]
    public void More_history_shifts_the_answer_from_the_prior_towards_the_data()
    {
        var shortRun = ModelFit.Fit(Synthetic(39, 0.09, 1.15, 7, 5), new ModelFit.Priors(37));
        var longRun = ModelFit.Fit(Synthetic(39, 0.09, 1.15, 7, 20), new ModelFit.Priors(37));

        Assert.True(longRun.DataWeight > shortRun.DataWeight);
        Assert.True(longRun.RatePerMonth.StandardError < shortRun.RatePerMonth.StandardError);
    }

    [Fact]
    public void Noise_widens_the_standard_errors_rather_than_being_ignored()
    {
        var clean = Synthetic(39, 0.09, 1.15, 7, 16).ToArray();
        var noisy = clean
            .Select((o, i) => o with { ObservedVdot = o.ObservedVdot + (i % 2 == 0 ? 1.2 : -1.2) })
            .ToArray();

        var cleanFit = ModelFit.Fit(clean, new ModelFit.Priors(37));
        var noisyFit = ModelFit.Fit(noisy, new ModelFit.Priors(37));

        Assert.True(noisyFit.ResidualSd > cleanFit.ResidualSd);
        Assert.True(noisyFit.RatePerMonth.StandardError > cleanFit.RatePerMonth.StandardError);
    }

    [Fact]
    public void The_fit_is_scored_against_the_weeks_actually_trained()
    {
        // Two months of nothing, then real training: a model fitted against an
        // average week would blame the athlete's physiology for the layoff.
        var dose = DoseResponse.Allocate(8).Dose;
        var history = ModelFit.History(
        [
            new FitObservation(0, 37, new TrainingDose()),
            new FitObservation(1, 37, new TrainingDose()),
            new FitObservation(2, 38, dose)
        ]);

        Assert.Equal(0, history(0).RunningHours);
        Assert.Equal(0, history(1.5).RunningHours);
        Assert.Equal(8, history(2).RunningHours, precision: 9);
        Assert.Equal(8, history(9).RunningHours, precision: 9);
    }
}

/// <summary>Prediction bands and the probabilities they imply.</summary>
public sealed class ForecastTests
{
    private static (TrajectoryParameters P, FitResult Fit, DoseSchedule Schedule) Setup(int months = 14)
    {
        var truth = new TrajectoryParameters(39, null, 0.09, 1.1);
        var dose = DoseResponse.Allocate(7, truth.Limits()).Dose;
        var data = Enumerable.Range(0, months)
            .Select(m => new FitObservation(
                m,
                Trajectory.VdotAt(truth, dose, m) + (m % 3 - 1) * 0.4,
                dose))
            .ToArray();

        var fit = ModelFit.Fit(data, new ModelFit.Priors(38));
        return (fit.ToParameters(null), fit, DoseSchedule.Constant(dose));
    }

    [Fact]
    public void The_band_widens_with_the_horizon()
    {
        var (p, fit, schedule) = Setup();

        var soon = Forecast.At(p, fit, schedule, 3);
        var later = Forecast.At(p, fit, schedule, 24);

        Assert.True(later.StandardDeviation > soon.StandardDeviation);
        Assert.True(soon.Low < soon.Vdot && soon.Vdot < soon.High);
        Assert.True(later.High - later.Low > soon.High - soon.Low);
    }

    [Fact]
    public void The_interval_covers_what_its_confidence_claims()
    {
        var (p, fit, schedule) = Setup();
        var band = Forecast.At(p, fit, schedule, 12, confidence: 0.80);

        // 80% two-sided is ±1.2816 standard deviations.
        Assert.Equal(1.2816, (band.High - band.Vdot) / band.StandardDeviation, precision: 3);
    }

    [Fact]
    public void A_target_the_projection_lands_on_is_a_coin_flip()
    {
        var (p, fit, schedule) = Setup();
        var landing = Trajectory.VdotAt(p, schedule, 18);

        Assert.Equal(0.5, Forecast.Probability(p, fit, schedule, landing, 18), precision: 3);
    }

    [Fact]
    public void Probability_rises_with_time_and_falls_with_ambition()
    {
        var (p, fit, schedule) = Setup();
        var target = Trajectory.VdotAt(p, schedule, 18);

        Assert.True(
            Forecast.Probability(p, fit, schedule, target, 12) <
            Forecast.Probability(p, fit, schedule, target, 30));

        Assert.True(
            Forecast.Probability(p, fit, schedule, target + 2, 18) <
            Forecast.Probability(p, fit, schedule, target, 18));
    }

    [Fact]
    public void The_date_for_a_given_confidence_roundtrips()
    {
        var (p, fit, schedule) = Setup();
        var target = Trajectory.VdotAt(p, schedule, 20);

        var months = Forecast.MonthsForProbability(p, fit, schedule, target, 0.8);
        Assert.NotNull(months);
        Assert.Equal(0.8, Forecast.Probability(p, fit, schedule, target, months!.Value), precision: 3);
        Assert.True(months > 20);
    }

    [Fact]
    public void A_target_past_the_ceiling_never_reaches_confidence()
    {
        var (p, fit, schedule) = Setup();
        Assert.Null(Forecast.MonthsForProbability(p, fit, schedule, 75, 0.8));
    }
}

/// <summary>The record book, and where a target sits against it.</summary>
public sealed class HumanLimitsTests
{
    [Fact]
    public void The_record_book_scores_where_the_physiology_literature_puts_it()
    {
        // Elite distance runners measure in the mid-eighties; if the equations
        // and the records disagree with that, one of them is wrong.
        Assert.InRange(HumanLimits.OpenCeiling(female: false), 84, 90);
        Assert.InRange(HumanLimits.OpenCeiling(female: true), 74, 82);
    }

    [Fact]
    public void Age_grading_discounts_the_ceiling_but_not_before_the_plateau()
    {
        var open = HumanLimits.OpenCeiling(false);

        Assert.Equal(open, HumanLimits.AgeGradedCeiling(false, 30));
        Assert.Equal(open, HumanLimits.AgeGradedCeiling(false, null));
        Assert.True(HumanLimits.AgeGradedCeiling(false, 50) < open);
        Assert.True(HumanLimits.AgeGradedCeiling(false, 70) < HumanLimits.AgeGradedCeiling(false, 50));
    }

    [Fact]
    public void The_bands_are_the_age_grading_convention()
    {
        Assert.Equal("past the world record", HumanLimits.Band(1.02));
        Assert.Equal("world class", HumanLimits.Band(0.93));
        Assert.Equal("national class", HumanLimits.Band(0.83));
        Assert.Equal("regional class", HumanLimits.Band(0.74));
        Assert.Equal("local class", HumanLimits.Band(0.63));
        Assert.Equal("recreational", HumanLimits.Band(0.45));
    }

    [Fact]
    public void A_record_equivalent_time_is_quoted_for_any_distance_asked_about()
    {
        // Five miles has no ratified record; the ceiling still answers for it.
        var seconds = HumanLimits.RecordEquivalentSeconds(5 * Vdot.MileMeters, false, null);
        Assert.InRange(seconds, 20 * 60, 23 * 60);
    }

    [Fact]
    public void Grading_is_explained_step_by_step()
    {
        var steps = HumanLimits.Explain(vdot: 60, female: false, age: 45);

        Assert.Contains(steps, s => s.Label == "Human ceiling");
        Assert.Contains(steps, s => s.Label == "Age-graded ceiling");
        Assert.Contains(steps, s => s.Label == "Where the target sits");
    }
}
