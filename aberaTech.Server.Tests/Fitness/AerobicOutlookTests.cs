using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>The short-term AeT pace outlook: a drift test carried ahead at a published rate.</summary>
public sealed class AerobicOutlookTests
{
    private static readonly LocalDate Today = new(2026, 11, 24);

    /// <summary>
    /// His easy runs as the log held them on 2026-11-24, as pace at 158 bpm:
    /// about 6:08/km through October, about 7:10/km from 1 November, on
    /// other treadmills, at twice the length, one week at 1,200 m.
    /// </summary>
    private static IEnumerable<SteadyRun> HisRuns()
    {
        (LocalDate Day, double Pace)[] runs =
        [
            (new(2026, 10, 21), 357), (new(2026, 10, 23), 362), (new(2026, 10, 25), 373), (new(2026, 10, 26), 370),
            (new(2026, 10, 30), 378), (new(2026, 10, 31), 399), (new(2026, 11, 1), 433), (new(2026, 11, 2), 407),
            (new(2026, 11, 4), 449), (new(2026, 11, 8), 440), (new(2026, 11, 9), 436), (new(2026, 11, 11), 439),
            (new(2026, 11, 13), 411), (new(2026, 11, 17), 434), (new(2026, 11, 19), 386), (new(2026, 11, 21), 465),
        ];
        return runs.Select(r => new SteadyRun(r.Day, 2700 / r.Pace * 1000, 2700, 158, Indoor: true));
    }

    // 4.5 mph at 2%: 497.1 s/km on the belt, 456.1 s/km flat.
    private static FieldTest Test(LocalDate on) =>
        new(FieldTests.AetKind, Guid.NewGuid(), on, 456.1, 159, 0.012, true, "", 2);

    [Fact]
    public void Without_a_drift_test_it_gives_no_number_and_asks_for_one()
    {
        var result = AerobicOutlook.Build(HisRuns(), 158, Today);

        Assert.False(result.Ready);
        Assert.Empty(result.Ahead);
        Assert.Equal(AerobicOutlook.NeedsTest, result.Needs);
        Assert.NotEmpty(result.Weeks);
    }

    [Fact]
    public void A_step_in_the_easy_runs_is_never_carried_ahead_as_a_slowdown()
    {
        // The defect of 2026-11-24: a straight line through these weeks
        // projected 8:05/km, 13:00 a mile, in 8 weeks.
        var result = AerobicOutlook.Build(HisRuns(), 158, Today, Test(new(2026, 11, 23)));

        Assert.True(result.Ready);
        var four = result.Ahead.Single(a => a.WeeksAhead == 4);
        var eight = result.Ahead.Single(a => a.WeeksAhead == 8);

        // Every projection is faster than the test, and later is faster.
        Assert.True(eight.SecPerKm < four.SecPerKm);
        Assert.True(four.SecPerKm < 456.1);

        // 3.6% in 8 weeks (Festa 2019): 456.1 ÷ 1.036 = 440.3; the two groups give 3.2% and 4.0%.
        Assert.Equal(456.1 / 1.036, eight.SecPerKm, precision: 6);
        Assert.Equal(456.1 / 1.040, eight.LowSecPerKm, precision: 6);
        Assert.Equal(456.1 / 1.032, eight.HighSecPerKm, precision: 6);
        Assert.Equal(456.1 / 1.018, four.SecPerKm, precision: 6);

        // The easy runs change nothing: the same test with no runs gives the same answer.
        var alone = AerobicOutlook.Build([], 158, Today, Test(new(2026, 11, 23)));
        Assert.Equal(eight, alone.Ahead.Single(a => a.WeeksAhead == 8));
    }

    [Fact]
    public void A_test_older_than_the_study_is_shown_and_not_carried_ahead()
    {
        var result = AerobicOutlook.Build(HisRuns(), 158, Today, Test(Today.PlusDays(-70)));

        Assert.False(result.Ready);
        Assert.NotNull(result.Anchor);
        Assert.Empty(result.Ahead);
        Assert.Contains("A new drift test: the last was 10 weeks ago", result.Needs);
    }
}
