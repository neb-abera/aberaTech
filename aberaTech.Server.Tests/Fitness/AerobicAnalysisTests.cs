using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class AerobicAnalysisTests
{
    [Fact]
    public void Normalizes_pace_linearly_in_heart_rate()
    {
        // 6:30/km at 160 bpm reads as 6:55.8/km at the 152 reference —
        // pace x 160/152.
        var normalized = AerobicAnalysis.NormalizedSecPerKm(5000, 5 * 390, 160, 152);
        Assert.Equal(390.0 * 160 / 152, normalized, precision: 3);
    }

    [Fact]
    public void A_run_at_the_reference_is_unchanged()
    {
        Assert.Equal(400, AerobicAnalysis.NormalizedSecPerKm(5000, 2000, 152, 152), precision: 6);
    }

    [Fact]
    public void Monthly_trend_uses_the_median_so_one_bad_run_cannot_drag_a_month()
    {
        var august = new LocalDate(2026, 8, 1);
        var runs = new[]
        {
            new SteadyRun(august.PlusDays(5), 5000, 5 * 390, 152),
            new SteadyRun(august.PlusDays(10), 5000, 5 * 395, 152),
            // The outlier: a walk-run logged as a run.
            new SteadyRun(august.PlusDays(15), 5000, 5 * 700, 152)
        };

        var trend = AerobicAnalysis.MonthlyTrend(runs, referenceHr: 152);

        var point = Assert.Single(trend);
        Assert.Equal(395, point.MedianNormalizedSecPerKm, precision: 3);
        Assert.Equal(3, point.RunCount);
    }

    [Fact]
    public void Short_runs_carry_no_signal_and_are_excluded()
    {
        var runs = new[] { new SteadyRun(new LocalDate(2026, 8, 5), 2000, 600, 150) };
        Assert.Empty(AerobicAnalysis.MonthlyTrend(runs, 152));
    }

    [Fact]
    public void Deficiency_spread_matches_the_audit()
    {
        // AeT pace 6:30/km against LT pace 5:40/km: (390-340)/340 = 14.7% —
        // deficient by the 10% rule.
        var spread = AerobicAnalysis.DeficiencySpread(390, 340);
        Assert.Equal(50.0 / 340, spread, precision: 4);
        Assert.True(spread > AerobicAnalysis.DeficiencyThreshold);
    }
}
