using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class TrajectoryTests
{
    private static readonly TrajectoryParameters P = new(StartVdot: 37);

    [Fact]
    public void Starts_at_the_anchor()
    {
        Assert.Equal(37, Trajectory.VdotAt(P, effectiveHours: 5.7, months: 0), precision: 6);
    }

    [Fact]
    public void Approaches_but_never_passes_the_ceiling()
    {
        var ceiling = Trajectory.Ceiling(P, 8.55);
        Assert.Equal(DoseResponse.Ceiling(DoseResponse.Allocate(8.55).Dose), ceiling, precision: 6);

        var atTwoYears = Trajectory.VdotAt(P, 8.55, 24);
        var atFiveYears = Trajectory.VdotAt(P, 8.55, 60);
        Assert.True(atTwoYears < atFiveYears);
        Assert.True(atFiveYears < ceiling);
    }

    [Fact]
    public void Committed_dose_matches_the_published_scenario_table()
    {
        // 9.5 h at 90% compliance — the "committed" scenario: ~44.2 at 12
        // months, ~45.2 at 24. These values were computed independently before
        // this class existed; they are the calibration, not its echo.
        var effective = Trajectory.EffectiveHours(9.5, 0.90);
        Assert.InRange(Trajectory.VdotAt(P, effective, 12), 44.7, 45.7);
        Assert.InRange(Trajectory.VdotAt(P, effective, 24), 48.3, 49.3);
    }

    [Fact]
    public void Unreachable_goal_reports_no_date_rather_than_a_wrong_one()
    {
        // An hour and a half a week cannot support VDOT 48, so no date is the
        // honest answer rather than one far enough out to look like arithmetic.
        Assert.True(Trajectory.Ceiling(P, 1.5) < 48);
        Assert.Null(Trajectory.MonthsToReach(P, 1.5, 48));
    }

    [Fact]
    public void Integration_reproduces_the_closed_form_it_replaced()
    {
        // With no trained past the rate is constant, so the differential
        // equation has the exponential solution the old model wrote out by
        // hand. Runge-Kutta must land on it, or the numerics are the model.
        var dose = DoseResponse.Allocate(8.55).Dose;
        var ceiling = Trajectory.Ceiling(P, dose);

        foreach (var months in new[] { 1.0, 6, 12, 24, 60 })
        {
            var closedForm = P.StartVdot
                             + (ceiling - P.StartVdot) * (1 - Math.Exp(-P.RatePerMonth * months));
            Assert.Equal(closedForm, Trajectory.VdotAt(P, dose, months), precision: 8);
        }
    }

    [Fact]
    public void A_plan_that_ramps_arrives_later_than_one_that_teleports()
    {
        // Nobody starts next Monday at nine hours a week from four, and a date
        // computed as though they did is a date that will be missed.
        var target = DoseResponse.Allocate(9).Dose;
        var now = DoseResponse.Allocate(4).Dose;

        var ramped = new DoseSchedule(target, now);
        var immediate = DoseSchedule.Constant(target);

        Assert.True(Trajectory.VdotAt(P, ramped, 6) < Trajectory.VdotAt(P, immediate, 6));

        // And the two converge once the ramp is done and the gap has closed.
        Assert.InRange(ramped.MonthsToFullDose(), 2, 3);
        Assert.Equal(Trajectory.VdotAt(P, immediate, 120), Trajectory.VdotAt(P, ramped, 120), precision: 3);
    }

    [Fact]
    public void The_dose_a_goal_needs_accounts_for_the_ramp_to_it()
    {
        var from = DoseResponse.Allocate(3).Dose;
        var standingStart = Trajectory.HoursToReach(P, targetVdot: 45, months: 12, from: from);
        var alreadyThere = Trajectory.HoursToReach(P, targetVdot: 45, months: 12);

        Assert.NotNull(standingStart);
        Assert.NotNull(alreadyThere);
        Assert.True(standingStart > alreadyThere);
    }

    [Fact]
    public void Reachable_goal_dates_are_consistent_with_the_forward_model()
    {
        var months = Trajectory.MonthsToReach(P, 8.55, 48);
        Assert.NotNull(months);
        Assert.Equal(48, Trajectory.VdotAt(P, 8.55, months!.Value), precision: 6);
    }

    [Fact]
    public void Inverse_dose_roundtrips_through_the_forward_model()
    {
        var hours = Trajectory.HoursToReach(P, targetVdot: 45, months: 12);
        Assert.NotNull(hours);
        Assert.Equal(45, Trajectory.VdotAt(P, hours!.Value, 12), precision: 6);
    }

    [Fact]
    public void Impossible_timelines_are_refused_not_flattered()
    {
        // VDOT 51 in three months would take an absurd dose; the model says so.
        Assert.Null(Trajectory.HoursToReach(P, targetVdot: 51, months: 3));
    }

    [Fact]
    public void Already_met_goals_need_no_dose()
    {
        Assert.Equal(0, Trajectory.HoursToReach(P, targetVdot: 36, months: 6));
        Assert.Equal(0, Trajectory.MonthsToReach(P, 5, targetVdot: 37));
    }
}
