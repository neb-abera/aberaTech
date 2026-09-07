using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The recovery budget was a constant, so the model believed a beginner could
/// hold a full-time athlete's volume as readily as someone years into
/// consistent base. What grows with training age is the capacity to absorb
/// load, and the athlete is asked for it rather than it being modelled from a
/// proxy.
/// </summary>
public sealed class SustainableWeekTests(ITestOutputHelper output)
{
    [Fact]
    public void A_stated_week_becomes_the_budget_the_plan_is_measured_against()
    {
        var modest = DoseResponse.StrainFor(5);
        var serious = DoseResponse.StrainFor(12);

        output.WriteLine($"5 h/week -> {modest:0.0} strain, 12 h/week -> {serious:0.0}");

        Assert.True(modest < serious);
        Assert.True(serious < DoseResponse.EliteStrain);
    }

    [Fact]
    public void Nobody_is_credited_with_more_than_a_full_time_athlete()
    {
        // A misremembered or aspirational answer cannot buy a superhuman
        // ceiling.
        // To the last decimal a strain figure is ever read at; the budget is
        // found by bisection and lands a rounding error short of the cap.
        Assert.Equal(DoseResponse.EliteStrain, DoseResponse.StrainFor(40), precision: 6);
        Assert.Equal(DoseResponse.EliteStrain, DoseResponse.StrainFor(100), precision: 6);
    }

    [Fact]
    public void A_smaller_budget_lowers_what_training_can_ever_reach()
    {
        // The point of the change: the ceiling a beginner can build to is not
        // the ceiling a full-time athlete can build to.
        var beginner = DoseResponse.MaxReachableCeiling(new DoseLimits(DoseResponse.StrainFor(4)));
        var experienced = DoseResponse.MaxReachableCeiling(new DoseLimits(DoseResponse.StrainFor(14)));
        var elite = DoseResponse.MaxReachableCeiling(new DoseLimits());

        output.WriteLine($"reachable ceiling: {beginner:0.0} at 4 h/wk sustained, {experienced:0.0} at 14, {elite:0.0} unstated");

        Assert.True(beginner < experienced);
        Assert.True(experienced < elite);
    }

    [Fact]
    public void A_week_already_held_is_a_floor_and_not_a_cap()
    {
        // Evidence the body absorbed it, not a limit on building further: a
        // plan may still ramp beyond the week that set the budget.
        var limits = new DoseLimits(DoseResponse.StrainFor(6));
        var beyond = DoseResponse.Allocate(9, limits).Dose;

        Assert.True(beyond.RunningHours > 6);
        Assert.True(beyond.Strain <= limits.MaxStrain + 1e-9);
    }

    [Fact]
    public void An_unstated_week_keeps_the_previous_behaviour()
    {
        // Back-compatible by construction: a profile that has never answered
        // gets exactly the budget it got before, and the assumptions say so.
        Assert.Equal(DoseResponse.EliteStrain, new DoseLimits().MaxStrain);
    }

    [Fact]
    public void Zero_is_an_answer_and_not_a_crash()
    {
        Assert.Equal(0, DoseResponse.StrainFor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DoseResponse.StrainFor(-1));
    }
}
