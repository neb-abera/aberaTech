using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A projection is of a plan, not of a log. An athlete coming back after years
/// away has a training history that records the absence of training, and the
/// model used to read a starting point out of it and quietly rewrite the plan
/// into a five-month build-up nobody asked for.
/// </summary>
public sealed class StatedPlanTests(ITestOutputHelper output)
{
    private const double Mile = 1609.344;

    private static SolverContext Context()
    {
        var truth = new TrajectoryParameters(37, null, 0.09, 1.1);
        var dose = DoseResponse.Allocate(6).Dose;
        var rng = new Rng(41);
        var history = Enumerable.Range(0, 10)
            .Select(m => new FitObservation(
                m, Trajectory.VdotAt(truth, dose, m) + rng.Normal(0, 0.5), dose))
            .ToArray();

        return new SolverContext(
            Posterior.Sample(history, new Posterior.Priors(history[0].ObservedVdot)),
            AnchorVdot: 37,
            ReclaimVdot: null,
            LoggedDose: DoseResponse.Allocate(1.5).Dose,
            CurrentMassKg: 79,
            AltitudeMeters: 0,
            Limits: new DoseLimits());
    }

    private static Scenario Plan(double? startHours = null, double ramp = 0) =>
        new(5 * Mile, 18, 7, Compliance: 1.0, RaceMassKg: 79, StrengthHours: 0,
            StartHours: startHours, RampPerWeek: ramp);

    [Fact]
    public void A_plan_is_projected_as_written_by_default()
    {
        // Nothing is read off the log: seven hours a week means seven hours a
        // week from the first week, which is what "predict this plan" means.
        var context = Context();
        var schedule = Plan().ScheduleTo(DoseResponse.Allocate(7).Dose, context.Limits);

        Assert.Equal(0, schedule.MonthsToFullDose());
        Assert.Equal(7, schedule.At(0).RunningHours, precision: 6);
        Assert.Equal(7, schedule.At(12).RunningHours, precision: 6);
    }

    [Fact]
    public void A_stated_build_up_ramps_from_where_the_athlete_says_they_are()
    {
        var context = Context();
        var schedule = Plan(startHours: 2, ramp: 0.08)
            .ScheduleTo(DoseResponse.Allocate(7).Dose, context.Limits);

        Assert.InRange(schedule.At(0).RunningHours, 1.5, 2.5);
        Assert.True(schedule.At(6).RunningHours > schedule.At(0).RunningHours);
        Assert.InRange(schedule.MonthsToFullDose(), 3, 4.5);
    }

    [Fact]
    public void The_log_no_longer_reaches_the_projection()
    {
        // The regression. The context still carries the logged week so the page
        // can offer it as a starting suggestion, but a projection that was not
        // told to ramp must not be slowed by it.
        var loggedLow = Context();
        var loggedHigh = loggedLow with { LoggedDose = DoseResponse.Allocate(9).Dose };

        var asWritten = Plan();
        Assert.Equal(
            Solver.Predict(loggedLow, asWritten).Median,
            Solver.Predict(loggedHigh, asWritten).Median,
            precision: 6);
    }

    [Fact]
    public void A_build_up_arrives_later_than_the_plan_it_builds_to()
    {
        var context = Context();
        var direct = Solver.Predict(context, Plan()).Median;
        var built = Solver.Predict(context, Plan(startHours: 1.5, ramp: 0.08)).Median;

        output.WriteLine($"5 mile at 18 months: as written {direct:0}s, built up {built:0}s");
        Assert.True(built > direct);
    }

    [Fact]
    public void A_returning_athlete_can_refuse_to_be_modelled_on_their_layoff()
    {
        // Months of almost no training carry almost no information about how
        // this athlete responds to training, and what looks like improvement is
        // detraining unwinding. Whether that counts as evidence is the
        // athlete's call, so the two answers must actually differ.
        var layoff = Enumerable.Range(0, 8)
            .Select(m => new FitObservation(m, 34 + m * 0.05, new TrainingDose(EasyHours: 0.4)))
            .ToArray();

        var fitted = Posterior.Sample(layoff, new Posterior.Priors(StartVdot: 37));
        var priorOnly = Posterior.Sample([], new Posterior.Priors(StartVdot: 37));

        output.WriteLine(
            $"responsiveness fitted to a layoff {fitted.Summary(d => d.Responsiveness).Median:0.00}, "
            + $"priors alone {priorOnly.Summary(d => d.Responsiveness).Median:0.00}");

        Assert.NotEqual(
            fitted.Summary(d => d.RatePerMonth).Median,
            priorOnly.Summary(d => d.RatePerMonth).Median,
            precision: 3);
        Assert.Equal(0.0676, priorOnly.Summary(d => d.RatePerMonth).Median, precision: 2);
    }
}
