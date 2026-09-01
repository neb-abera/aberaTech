using System.Diagnostics;
using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// One engine, five questions. What the console used to do in two hard-coded
/// directions it now does in any direction, and every answer arrives as a
/// distribution rather than a number.
/// </summary>
public sealed class SolverTests(ITestOutputHelper output)
{
    private const double Mile = 1609.344;

    private static SolverContext Context(double anchorVdot = 37, double? mass = 79)
    {
        var truth = new TrajectoryParameters(anchorVdot, null, 0.09, 1.1);
        var dose = DoseResponse.Allocate(6).Dose;
        var rng = new Rng(11);

        var history = Enumerable.Range(0, 14)
            .Select(m => new FitObservation(
                m, Trajectory.VdotAt(truth, dose, m) + rng.Normal(0, 0.5), dose))
            .ToArray();

        return new SolverContext(
            Posterior.Sample(history, new Posterior.Priors(history[0].ObservedVdot)),
            anchorVdot,
            ReclaimVdot: null,
            LoggedDose: DoseResponse.Allocate(3).Dose,
            CurrentMassKg: mass,
            AltitudeMeters: 1190,
            Limits: new DoseLimits());
    }

    private static Scenario Base(double hours = 7, double months = 18) =>
        new(5 * Mile, months, hours, Compliance: 0.85, RaceMassKg: 79);

    [Fact]
    public void Predicts_a_race_time_as_a_spread_not_a_number()
    {
        var spread = Solver.Predict(Context(), Base());

        output.WriteLine($"5 mile at +18 months: {spread.Median:0} s ({spread.Low:0}-{spread.High:0})");
        Assert.True(spread.Low < spread.Median && spread.Median < spread.High);
        Assert.Equal(0, spread.Impossible);
    }

    [Theory]
    [InlineData(Factor.WeeklyHours, 9.0)]
    [InlineData(Factor.Compliance, 0.95)]
    [InlineData(Factor.RaceMassKg, 75.0)]
    [InlineData(Factor.Months, 24.0)]
    public void Solves_for_whichever_factor_is_the_unknown(Factor unknown, double truth)
    {
        // Round trip: take the time that setting this factor to a known value
        // produces, hand it back as the target, and the solver should find the
        // value again — whichever of the five factors is the blank one.
        var context = Context();
        var scenario = Base();
        var target = Solver.Predict(context, scenario.With(unknown, truth)).Median;

        var answer = Solver.Solve(context, scenario, unknown, target);
        output.WriteLine(
            $"{Solver.Name(unknown),-15} truth {truth:0.000} -> solved {answer.Median:0.000} "
            + $"({answer.Low:0.000}-{answer.High:0.000}), no answer for {answer.Impossible:P0}");

        Assert.True(answer.Impossible < 0.5);
        Assert.Equal(truth, answer.Median, tolerance: Math.Abs(truth) * 0.10);

        // And it round-trips in time as well as in the factor.
        Assert.Equal(
            target,
            Solver.Predict(context, scenario.With(unknown, answer.Median)).Median,
            tolerance: target * 0.02);
    }

    [Fact]
    public void Compliance_alone_cannot_buy_what_hours_can()
    {
        // A bounded lever is bounded: going from 85% to a perfect 100% is worth
        // a couple of percent, and the solver says "no answer" for the rest of
        // the draws rather than quietly returning 100%.
        var context = Context();
        var scenario = Base();
        var ambitious = Solver.Predict(context, scenario).Median * 0.97;

        var byCompliance = Solver.Solve(context, scenario, Factor.Compliance, ambitious);
        var byHours = Solver.Solve(context, scenario, Factor.WeeklyHours, ambitious);

        output.WriteLine($"3% faster: compliance has no answer for {byCompliance.Impossible:P0}, hours for {byHours.Impossible:P0}");
        Assert.True(byCompliance.Impossible > 0.4);
        Assert.Equal(0, byHours.Impossible);
    }

    [Fact]
    public void A_target_nothing_reaches_is_reported_as_impossible_not_clamped()
    {
        var context = Context();

        // Two minutes for five miles: no dose, no date, no diet.
        var answer = Solver.Solve(context, Base(), Factor.WeeklyHours, targetSeconds: 120);

        Assert.Equal(1, answer.Impossible);
        Assert.True(double.IsNaN(answer.Median));
    }

    [Fact]
    public void A_target_already_met_reports_that_nothing_has_to_change()
    {
        var context = Context();
        var scenario = Base();
        var easy = Solver.Predict(context, scenario).High * 1.5;

        var answer = Solver.Solve(context, scenario, Factor.WeeklyHours, easy);

        // "No change needed" is its own answer. Folding it into the median as
        // a zero would read as "stop training", which is not what was meant.
        Assert.Equal(1, answer.AlreadyMet, precision: 6);
        Assert.Equal(scenario.WeeklyHours, answer.Median, precision: 6);
        Assert.Equal(0, answer.Impossible);
    }

    [Fact]
    public void Ranks_the_factors_by_how_much_race_time_they_actually_move()
    {
        var sensitivities = Solver.Sensitivities(Context(), Base());

        foreach (var s in sensitivities)
        {
            output.WriteLine(
                $"{Solver.Name(s.Factor),-15} swing {s.Swing:0} s over {s.LowValue:0.00}-{s.HighValue:0.00}, "
                + $"elasticity {s.Elasticity:0.000}, {s.PerUnitSeconds:0.0} s per unit");
        }

        // Sorted by swing, which is what a tornado chart is.
        Assert.Equal(
            sensitivities.Select(s => s.Swing).OrderByDescending(x => x),
            sensitivities.Select(s => s.Swing));

        // More training and more time make you faster; more weight makes you
        // slower. A model that got these signs wrong would be worse than none.
        Assert.True(First(sensitivities, Factor.WeeklyHours).PerUnitSeconds < 0);
        Assert.True(First(sensitivities, Factor.Compliance).PerUnitSeconds < 0);
        Assert.True(First(sensitivities, Factor.Months).PerUnitSeconds < 0);
        Assert.True(First(sensitivities, Factor.RaceMassKg).PerUnitSeconds > 0);
    }

    [Fact]
    public void Elasticities_are_comparable_across_units()
    {
        // The point of an elasticity: hours, kilograms and percentage points
        // are not comparable, but "percent of race time per percent of factor"
        // is, and that is what decides where the next effort goes.
        var sensitivities = Solver.Sensitivities(Context(), Base());
        Assert.All(sensitivities, s => Assert.InRange(Math.Abs(s.Elasticity), 0, 2));

        var hours = First(sensitivities, Factor.WeeklyHours);
        var strength = First(sensitivities, Factor.StrengthHours);
        Assert.True(Math.Abs(hours.Elasticity) > Math.Abs(strength.Elasticity));
    }

    [Fact]
    public void The_surface_moves_the_right_way_in_both_directions()
    {
        var context = Context();
        var grid = Solver.Surface(
            context, Base(), Factor.WeeklyHours, Factor.Months, (3, 12), (6, 30), resolution: 8);

        // More hours across, more months down: times fall in both directions.
        for (var row = 0; row < 8; row++)
        {
            Assert.True(grid[row, 7] < grid[row, 0], $"row {row} did not improve with hours");
        }

        for (var column = 0; column < 8; column++)
        {
            Assert.True(grid[7, column] < grid[0, column], $"column {column} did not improve with time");
        }
    }

    [Fact]
    public void Probability_falls_as_the_target_gets_faster()
    {
        var context = Context();
        var scenario = Base();
        var middle = Solver.Predict(context, scenario).Median;

        var generous = Solver.Probability(context, scenario, middle * 1.05);
        var even = Solver.Probability(context, scenario, middle);
        var hard = Solver.Probability(context, scenario, middle * 0.95);

        output.WriteLine($"chance at +5% {generous:P0}, at the median {even:P0}, at −5% {hard:P0}");
        Assert.True(generous > even && even > hard);
        Assert.InRange(even, 0.4, 0.6);
    }

    [Fact]
    public void A_solve_stays_within_an_order_of_magnitude_of_interactive()
    {
        // The bound is deliberately loose. A solve takes about a second on a
        // quiet machine and three under load, so a tight wall-clock assertion
        // is a flaky gate rather than a useful one — it fails on a busy runner
        // and tells you nothing. What is worth catching is a return to the
        // behaviour this replaced, where the likelihood was integrated once
        // per observation instead of once per proposal and a fit took
        // thirty-seven seconds. The measured time is printed either way.
        var context = Context();
        var scenario = Base();
        var target = Solver.Predict(context, scenario).Median * 0.97;

        var clock = Stopwatch.StartNew();
        Solver.Solve(context, scenario, Factor.WeeklyHours, target);
        Solver.Sensitivities(context, scenario);
        clock.Stop();

        output.WriteLine($"solve plus tornado in {clock.ElapsedMilliseconds} ms");
        Assert.True(clock.ElapsedMilliseconds < 15_000, $"took {clock.ElapsedMilliseconds} ms");
    }

    private static FactorSensitivity First(IEnumerable<FactorSensitivity> all, Factor factor) =>
        all.Single(s => s.Factor == factor);
}

/// <summary>
/// What to go and measure next. The model's real limit is the thinness of the
/// record it is fitted to, and this is the part that says what fixing it costs
/// and what it is worth.
/// </summary>
public sealed class InformationTests(ITestOutputHelper output)
{
    private const double Mile = 1609.344;

    private static SolverContext Context()
    {
        var truth = new TrajectoryParameters(37, null, 0.09, 1.1);
        var dose = DoseResponse.Allocate(6).Dose;
        var rng = new Rng(23);
        var history = Enumerable.Range(0, 10)
            .Select(m => new FitObservation(m, Trajectory.VdotAt(truth, dose, m) + rng.Normal(0, 0.6), dose))
            .ToArray();

        return new SolverContext(
            Posterior.Sample(history, new Posterior.Priors(history[0].ObservedVdot)),
            37, null, DoseResponse.Allocate(3).Dose, 79, 1190, new DoseLimits());
    }

    [Fact]
    public void A_time_trial_is_worth_more_than_another_month_of_the_same_proxy()
    {
        var context = Context();
        var scenario = new Scenario(5 * Mile, 18, 7, 0.85, 79);

        var trial = Information.Value(context, scenario, atMonths: 2, horizonMonths: 18);
        var proxy = Information.Value(
            context, scenario, atMonths: 2, horizonMonths: 18, ObservationKind.NormalizedPace);

        output.WriteLine($"time trial cuts {trial.Reduction:P0}, another proxy month cuts {proxy.Reduction:P0}");
        Assert.True(trial.Reduction > proxy.Reduction);
        Assert.InRange(trial.Reduction, 0, 1);
    }

    [Fact]
    public void A_measurement_nearer_the_date_pins_that_date_harder()
    {
        // Worth knowing, and not obvious: a trial run next month says less
        // about a race two years out than one run a month before it. The early
        // trial is still the one worth doing, because it is the one that
        // arrives while the plan can still be changed — but it buys less
        // certainty about the far date, and the model should not pretend
        // otherwise.
        var context = Context();
        var scenario = new Scenario(5 * Mile, 24, 7, 0.85, 79);

        var soon = Information.Value(context, scenario, 1, 24);
        var late = Information.Value(context, scenario, 18, 24);

        output.WriteLine($"at +1 month cuts {soon.Reduction:P0}, at +18 months cuts {late.Reduction:P0}");
        Assert.True(late.Reduction > soon.Reduction);
        Assert.True(soon.Reduction >= 0);
    }

    [Fact]
    public void The_options_come_back_ranked_with_their_arithmetic()
    {
        var context = Context();
        var options = Information.Options(context, new Scenario(5 * Mile, 18, 7, 0.85, 79));

        Assert.NotEmpty(options);
        Assert.Equal(options.Select(o => o.Reduction).OrderByDescending(x => x), options.Select(o => o.Reduction));
        Assert.All(Information.Explain(options), step => Assert.Contains("cuts the uncertainty", step.Value));

        foreach (var option in options.Take(4))
        {
            output.WriteLine(
                $"{option.Kind} at +{option.AtMonths:0.#}mo: {option.WidthBefore / 60:0.00} -> {option.WidthAfter / 60:0.00} min ({option.Reduction:P0})");
        }
    }
}
