using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>The short-term AeT pace forecast: what it waits for, and the line once it has it.</summary>
public sealed class AerobicOutlookTests
{
    private static readonly LocalDate Today = new(2026, 11, 4); // a Wednesday

    /// <summary>Three 40-minute runs at 152 bpm each week, the pace falling 5 s/km a week.</summary>
    private static IEnumerable<SteadyRun> Weeks(int count, int runsPerWeek = 3)
    {
        var thisWeek = new LocalDate(2026, 11, 2);
        for (var w = 0; w < count; w++)
        {
            var start = thisWeek.PlusWeeks(-(count - 1 - w));
            var pace = 420 - 5 * w;
            for (var r = 0; r < runsPerWeek; r++)
            {
                yield return new SteadyRun(start.PlusDays(r), 2400 / (double)pace * 1000, 2400, 152);
            }
        }
    }

    [Fact]
    public void It_waits_for_six_weeks_of_three_easy_runs_and_says_how_many_it_has()
    {
        var result = AerobicOutlook.Build(Weeks(4), 152, Today);

        Assert.False(result.Ready);
        Assert.Equal(4, result.QualifyingWeeks);
        Assert.Empty(result.Ahead);

        // Two runs a week do not count, however many weeks there are.
        Assert.Equal(0, AerobicOutlook.Build(Weeks(10, runsPerWeek: 2), 152, Today).QualifyingWeeks);
    }

    [Fact]
    public void Six_weeks_carry_the_line_four_and_eight_weeks_ahead_with_a_range()
    {
        var result = AerobicOutlook.Build(Weeks(6), 152, Today);

        Assert.True(result.Ready);
        Assert.Equal(-5, result.SlopeSecPerKmPerWeek!.Value, precision: 6);

        // This week is 395 s/km; four weeks on the line is 375, eight is 355.
        var four = result.Ahead.Single(a => a.WeeksAhead == 4);
        var eight = result.Ahead.Single(a => a.WeeksAhead == 8);
        Assert.Equal(375, four.SecPerKm, precision: 6);
        Assert.Equal(355, eight.SecPerKm, precision: 6);
        Assert.True(four.LowSecPerKm <= four.SecPerKm && four.SecPerKm <= four.HighSecPerKm);
    }
}
