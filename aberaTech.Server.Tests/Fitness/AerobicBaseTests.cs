using aberaTech.Fitness.Domain;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Maximising the ceiling is the wrong objective at low volume. Nothing is
/// saturated at ninety minutes a week, so every zone's first hour looks
/// valuable and the optimiser spread them evenly — barely forty per cent of the
/// week into easy running, for an athlete whose measured deficit is base.
/// </summary>
public sealed class AerobicBaseTests(ITestOutputHelper output)
{
    private static DoseLimits Deficient => new(MaxIntensityShare: DoseResponse.IntensityCapFor(0.15));
    private static DoseLimits Sound => new(MaxIntensityShare: DoseResponse.IntensityCapFor(0.05));

    [Fact]
    public void A_deficient_athletes_short_week_is_nearly_all_easy()
    {
        var unconstrained = DoseResponse.Allocate(1.5).Dose;
        var capped = DoseResponse.Allocate(1.5, Deficient).Dose;

        output.WriteLine(
            $"90 minutes a week: {unconstrained.EasyShare:P0} easy unconstrained, {capped.EasyShare:P0} capped");

        Assert.True(unconstrained.EasyShare < 0.6, "the behaviour being fixed");
        Assert.True(capped.EasyShare >= 0.89);
        Assert.Equal(1.5, capped.RunningHours, precision: 6);
    }

    [Fact]
    public void A_sound_base_keeps_its_intensity()
    {
        // The cap is a correction for a measured deficit, not a general
        // suspicion of hard running.
        var capped = DoseResponse.Allocate(8, Sound).Dose;
        var hard = capped.ThresholdHours + capped.IntervalHours;

        Assert.True(hard / capped.RunningHours <= DoseResponse.SoundIntensityShare + 1e-9);
        Assert.InRange(capped.EasyShare, 0.75, 0.90);
    }

    [Fact]
    public void An_unmeasured_base_is_treated_as_the_cautious_case()
    {
        // No lactate-threshold pace recorded is not evidence of a sound base.
        Assert.Equal(DoseResponse.DeficientIntensityShare, DoseResponse.IntensityCapFor(null));
        Assert.Equal(DoseResponse.SoundIntensityShare, DoseResponse.IntensityCapFor(0.04));
        Assert.Equal(
            DoseResponse.DeficientIntensityShare,
            DoseResponse.IntensityCapFor(AerobicAnalysis.DeficiencyThreshold + 0.01));
    }

    [Fact]
    public void The_cap_still_spends_every_hour_it_was_given()
    {
        foreach (var hours in new[] { 1.0, 3.0, 6.0, 9.0 })
        {
            var dose = DoseResponse.Allocate(hours, Deficient).Dose;
            Assert.Equal(hours, dose.RunningHours, precision: 6);
            Assert.True(dose.ThresholdHours >= 0 && dose.IntervalHours >= 0);
        }
    }

    [Fact]
    public void What_hard_hours_survive_are_split_the_way_the_optimiser_chose()
    {
        // A binding constraint is still an optimum. The cap changes how much
        // hard running there is, not which kind.
        var free = DoseResponse.Allocate(4).Dose;
        var capped = DoseResponse.Allocate(4, Deficient).Dose;

        var freeRatio = free.ThresholdHours / free.IntervalHours;
        var cappedRatio = capped.ThresholdHours / capped.IntervalHours;

        output.WriteLine($"threshold:interval {freeRatio:0.000} uncapped, {cappedRatio:0.000} capped");
        Assert.Equal(freeRatio, cappedRatio, precision: 6);
    }

    [Fact]
    public void Rucking_counts_towards_the_running_ceiling_at_a_discount()
    {
        // The same hour, rucked and run.
        var run = SessionMix.WeeklyDose([new LoggedSession("run", 10_000, 3600)], weeks: 1, vdot: 37);
        var ruck = SessionMix.WeeklyDose([new LoggedSession("ruck", 8_000, 3600)], weeks: 1, vdot: 37);

        output.WriteLine($"one hour: {run.EasyHours:0.00} run, {ruck.EasyHours:0.00} rucked");

        Assert.Equal(1.0, run.EasyHours, precision: 6);
        Assert.Equal(SessionMix.RuckTransfer, ruck.EasyHours, precision: 6);
        Assert.True(ruck.EasyHours < run.EasyHours);
        Assert.True(ruck.EasyHours > 0.5, "rucking is aerobic work, not nothing");
    }
}
