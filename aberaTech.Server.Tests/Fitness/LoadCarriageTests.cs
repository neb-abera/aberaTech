using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class LoadCarriageTests
{
    private const double Mile = 1609.344;
    private const double FortyFivePounds = 45 / 2.2046226218;

    [Fact]
    public void Pandolf_unloaded_walk_matches_the_equation_by_hand()
    {
        // 1.5·70 + 0 + 1·70·(1.5·1²) = 105 + 105.
        Assert.Equal(210, LoadCarriage.MetabolicWatts(70, 0, 1.0), precision: 6);
    }

    [Fact]
    public void Load_and_grade_both_cost_energy()
    {
        var flat = LoadCarriage.MetabolicWatts(86, FortyFivePounds, 1.79);
        var uphill = LoadCarriage.MetabolicWatts(86, FortyFivePounds, 1.79, gradePercent: 5);
        var unloaded = LoadCarriage.MetabolicWatts(86, 0, 1.79);

        Assert.True(flat > unloaded);
        Assert.True(uphill > flat);
        // ~650 W for a 190 lb soldier at 15:00/mile under 45 lb.
        Assert.InRange(flat, 620, 690);
    }

    [Fact]
    public void Speed_for_a_cost_inverts_the_cost_of_a_speed()
    {
        var cost = LoadCarriage.OxygenCost(80, 15, 1.6);
        Assert.Equal(1.6, LoadCarriage.SpeedFor(cost, 80, 15), precision: 6);
    }

    [Theory]
    // The published pairs the efficiency was calibrated on: a day-one
    // minimum runner should ruck twelve miles in about three hours, a
    // competitive one in about 2:45.
    [InlineData(15 * 60 + 12, 3 * 3600, 6 * 60)]
    [InlineData(13 * 60 + 30, 2 * 3600 + 45 * 60, 5 * 60)]
    public void Twelve_mile_at_45_lb_agrees_with_the_published_benchmark_pairs(
        double twoMileSeconds, double expectedRuckSeconds, double tolerance)
    {
        var vdot = Vdot.FromRace(2 * Mile, twoMileSeconds / 60);

        var predicted = LoadCarriage.PredictSeconds(vdot, 86, FortyFivePounds, 12 * Mile);

        Assert.InRange(predicted, expectedRuckSeconds - tolerance, expectedRuckSeconds + tolerance);
    }

    [Fact]
    public void Heavier_load_means_a_slower_march()
    {
        var light = LoadCarriage.PredictSeconds(42, 84, 35 / 2.2046226218, 12 * Mile);
        var heavy = LoadCarriage.PredictSeconds(42, 84, 55 / 2.2046226218, 12 * Mile);
        Assert.True(heavy > light);
    }

    [Fact]
    public void A_timed_march_reads_back_to_the_vdot_that_predicted_it()
    {
        const double vdot = 41.5;
        var seconds = LoadCarriage.PredictSeconds(vdot, 82, FortyFivePounds, 12 * Mile);

        var implied = LoadCarriage.ImpliedVdot(new LoadedMarch(82, FortyFivePounds, 12 * Mile, seconds));

        Assert.Equal(vdot, implied, precision: 2);
    }

    [Fact]
    public void Normalized_pace_moves_a_light_ruck_onto_the_reference_load()
    {
        // The same effort under 25 lb would have been slower under 45 lb.
        var march = new LoadedMarch(86, 25 / 2.2046226218, 8000, 8000 / 1.9);
        var rawSecPerKm = 1000 / 1.9;

        var normalized = LoadCarriage.NormalizedSecPerKm(march, averageHr: 150, referenceHr: 150);

        Assert.True(normalized > rawSecPerKm);
    }

    [Fact]
    public void Normalized_pace_credits_a_low_heart_rate()
    {
        var march = new LoadedMarch(86, FortyFivePounds, 8000, 8000 / 1.8);

        var easy = LoadCarriage.NormalizedSecPerKm(march, averageHr: 135, referenceHr: 150);
        var hard = LoadCarriage.NormalizedSecPerKm(march, averageHr: 165, referenceHr: 150);

        Assert.True(easy < hard);
    }

    [Fact]
    public void Explanation_carries_the_sources()
    {
        var steps = LoadCarriage.Explain(40, 86, FortyFivePounds, 12 * Mile);

        Assert.Equal(3, steps.Count);
        Assert.Contains(steps, s => s.CitationId == Citations.PandolfLoadCarriage.Id);
        Assert.Contains(steps, s => s.CitationId == Citations.DanielsVdot.Id);
        Assert.Contains(steps, s => s.Value.Contains("/mile"));
    }
}
