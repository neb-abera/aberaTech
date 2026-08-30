using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class HighlightsTests
{
    [Fact]
    public void Aerobic_improvement_fires_with_its_evidence()
    {
        var trend = new[]
        {
            new MonthlyAerobicPoint(2026, 7, 447, 2),
            new MonthlyAerobicPoint(2026, 8, 410, 11)
        };

        var highlights = Highlights.Build(trend, [], 160, []);

        var gain = Assert.Single(highlights, h => h.Kind == "aerobic-gain");
        Assert.True(gain.Positive);
        Assert.Contains("7:27", gain.Evidence);
        Assert.Contains("6:50", gain.Evidence);
    }

    [Fact]
    public void Regression_is_reported_not_hidden()
    {
        var trend = new[]
        {
            new MonthlyAerobicPoint(2026, 4, 425, 4),
            new MonthlyAerobicPoint(2026, 5, 478, 2)
        };

        var highlights = Highlights.Build(trend, [], 160, []);

        var loss = Assert.Single(highlights, h => h.Kind == "aerobic-loss");
        Assert.False(loss.Positive);
    }

    [Fact]
    public void Small_month_to_month_noise_stays_quiet()
    {
        var trend = new[]
        {
            new MonthlyAerobicPoint(2026, 7, 410, 5),
            new MonthlyAerobicPoint(2026, 8, 407, 6)
        };

        Assert.DoesNotContain(Highlights.Build(trend, [], 160, []),
            h => h.Kind is "aerobic-gain" or "aerobic-loss");
    }

    [Fact]
    public void Empty_weeks_produce_a_warning_streak()
    {
        var monday = new LocalDate(2026, 8, 3);
        var weeks = new[]
        {
            new WeekVolume(monday, 120),
            new WeekVolume(monday.PlusDays(7), 0),
            new WeekVolume(monday.PlusDays(14), 0)
        };

        var highlights = Highlights.Build([], weeks, 160, []);

        var gap = Assert.Single(highlights, h => h.Kind == "volume-gap");
        Assert.Contains("2 straight weeks", gap.Headline);
    }

    [Fact]
    public void Meeting_plan_repeatedly_is_a_streak()
    {
        var monday = new LocalDate(2026, 8, 3);
        var weeks = new[]
        {
            new WeekVolume(monday, 170),
            new WeekVolume(monday.PlusDays(7), 165),
            new WeekVolume(monday.PlusDays(14), 180)
        };

        var highlights = Highlights.Build([], weeks, 160, []);

        Assert.Single(highlights, h => h.Kind == "volume-streak");
    }

    [Fact]
    public void Strength_pr_and_slide_both_fire()
    {
        var history = new[]
        {
            new E1RmPoint(new LocalDate(2026, 5, 15), "Bench Press (Barbell)", 217),
            new E1RmPoint(new LocalDate(2026, 8, 19), "Bench Press (Barbell)", 196),
            new E1RmPoint(new LocalDate(2026, 4, 10), "Deadlift (Barbell)", 260),
            new E1RmPoint(new LocalDate(2026, 8, 20), "Deadlift (Barbell)", 281)
        };

        var highlights = Highlights.Build([], [], 160, history);

        Assert.Single(highlights, h => h.Kind == "strength-slide" && h.Headline.Contains("Bench"));
        Assert.Single(highlights, h => h.Kind == "strength-pr" && h.Headline.Contains("Deadlift"));
    }
}
