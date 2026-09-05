using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Fitness that can only go up is not a model of training, it is a model of
/// wishing. The ceiling used to be floored at the athlete's starting fitness,
/// so the gap driving the trajectory was never negative and no dose — including
/// none at all — could make anyone slower.
/// </summary>
public sealed class DetrainingTests(ITestOutputHelper output)
{
    private static readonly TrajectoryParameters Trained = new(StartVdot: 48);

    [Fact]
    public void Stopping_makes_you_slower()
    {
        var nothing = new TrainingDose();

        var afterAMonth = Trajectory.VdotAt(Trained, nothing, 1);
        var afterSixMonths = Trajectory.VdotAt(Trained, nothing, 6);

        output.WriteLine($"48.0 -> {afterAMonth:0.0} after a month, {afterSixMonths:0.0} after six");

        Assert.True(afterAMonth < Trained.StartVdot);
        Assert.True(afterSixMonths < afterAMonth);

        // Never below the untrained ceiling: doing nothing does not take you
        // below what doing nothing supports.
        Assert.True(afterSixMonths > DoseResponse.UntrainedCeiling - 0.01);
    }

    [Fact]
    public void A_month_off_costs_about_what_the_detraining_literature_says()
    {
        // Several per cent of a trained athlete's VO2max inside four weeks,
        // most of it rapidly reversible. A model that lost half of it, or one
        // per cent, would not be describing the same phenomenon.
        var lost = Trained.StartVdot - Trajectory.VdotAt(Trained, new TrainingDose(), 1);

        output.WriteLine($"a month of nothing costs {lost:0.0} VDOT ({lost / Trained.StartVdot:P1})");
        Assert.InRange(lost / Trained.StartVdot, 0.04, 0.14);
    }

    [Fact]
    public void Fitness_is_lost_faster_than_it_is_built()
    {
        var week = DoseResponse.Allocate(7).Dose;

        // From the same distance to the same ceiling, in both directions.
        var ceiling = Trajectory.Ceiling(Trained, week);
        var climbing = new TrajectoryParameters(StartVdot: ceiling - 5);
        var falling = new TrajectoryParameters(StartVdot: DoseResponse.UntrainedCeiling + 5);

        var gained = Trajectory.VdotAt(climbing, week, 1) - climbing.StartVdot;
        var lost = falling.StartVdot - Trajectory.VdotAt(falling, new TrainingDose(), 1);

        output.WriteLine($"one month: +{gained:0.00} climbing, -{lost:0.00} falling");
        Assert.True(lost > gained * 3);
    }

    [Fact]
    public void A_plan_with_a_hole_in_it_is_worse_than_the_same_hours_spread_out()
    {
        // The point compliance-as-a-fraction cannot make. Two athletes train
        // the same total over twelve months; one loses a single month whole.
        var plan = DoseSchedule.Constant(DoseResponse.Allocate(7).Dose);
        var evenlyThinner = DoseSchedule.Constant(DoseResponse.Allocate(7 * 11.0 / 12).Dose);

        var withGap = Trajectory.VdotAt(Trained, plan.WithGap(fromMonths: 5, months: 1), 12);
        var spread = Trajectory.VdotAt(Trained, evenlyThinner, 12);

        output.WriteLine($"after 12 months: {withGap:0.00} with a month off, {spread:0.00} spread evenly");
        Assert.True(withGap < spread);
    }

    [Fact]
    public void What_a_gap_costs_is_mostly_paid_back_but_not_all_of_it()
    {
        var plan = DoseSchedule.Constant(DoseResponse.Allocate(7).Dose);

        var uninterrupted = Trajectory.VdotAt(Trained, plan, 12);
        var interrupted = Trajectory.VdotAt(Trained, plan.WithGap(2, 1), 12);

        var duringTheGap = Trajectory.VdotAt(Trained, plan.WithGap(2, 1), 3);
        var wouldHaveBeen = Trajectory.VdotAt(Trained, plan, 3);

        output.WriteLine(
            $"at the end of the gap: {duringTheGap:0.0} against {wouldHaveBeen:0.0}; "
            + $"nine months later: {interrupted:0.00} against {uninterrupted:0.00}");

        Assert.True(duringTheGap < wouldHaveBeen);
        Assert.True(interrupted < uninterrupted);

        var onTheDay = wouldHaveBeen - duringTheGap;
        var nineMonthsLater = uninterrupted - interrupted;

        // Some of it comes back, because the ground is re-taken at the
        // retraining rate rather than broken new. But over half of the loss is
        // still there nine months later, on identical training since — a month
        // off is not the same as being a month behind, which is the intuition
        // it usually gets treated as.
        Assert.True(nineMonthsLater < onTheDay);
        Assert.InRange(nineMonthsLater / onTheDay, 0.25, 0.90);
    }

    [Fact]
    public void Coming_back_is_faster_than_arriving_was()
    {
        // The reclaim lane belongs to regaining, and must not speed up losing.
        var returning = new TrajectoryParameters(StartVdot: 40, ReclaimVdot: 50);

        Assert.True(Trajectory.RateMultiplier(returning, 41) > 1);

        // Falling from above the old peak is ordinary detraining, not a
        // 2.5x-accelerated collapse.
        var high = new TrajectoryParameters(StartVdot: 52, ReclaimVdot: 50);
        Assert.Equal(1, Trajectory.RateMultiplier(high, 52), precision: 6);
    }
}
