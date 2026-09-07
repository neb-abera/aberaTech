using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class CalisthenicsTests
{
    [Theory]
    [InlineData("Pull Up", Calisthenics.PullUps)]
    [InlineData("Chin Up (Weighted)", Calisthenics.PullUps)]
    [InlineData("Lat Pulldown (Cable)", null)]
    [InlineData("Push Up", Calisthenics.PushUps)]
    [InlineData("Hand Release Push Up", Calisthenics.HandReleasePushUps)]
    [InlineData("HRP", Calisthenics.HandReleasePushUps)]
    [InlineData("Sit Up", Calisthenics.SitUps)]
    [InlineData("Plank", Calisthenics.Plank)]
    [InlineData("Side Plank", null)]
    [InlineData("Copenhagen Plank", null)]
    [InlineData("Bench Press (Barbell)", null)]
    public void Movements_are_read_by_name(string exercise, string? metric)
    {
        Assert.Equal(metric, Calisthenics.Classify(exercise));
    }

    [Fact]
    public void Best_set_per_day_ignores_weighted_reps_and_reads_the_plank_as_time()
    {
        var day = new LocalDate(2026, 8, 20);
        var sets = new[]
        {
            new BodyweightSet(day, "Pull Up", 8),
            new BodyweightSet(day, "Pull Up", 11),
            new BodyweightSet(day, "Pull Up", 20, AddedKg: 10), // weighted, not a count
            new BodyweightSet(day, "Plank", 1, DurationSeconds: 150),
            new BodyweightSet(day.PlusDays(1), "Plank", 1, DurationSeconds: 170),
            new BodyweightSet(day, "Bench Press (Barbell)", 5, AddedKg: 80)
        };

        var best = Calisthenics.BestPerDay(sets);

        Assert.Equal(3, best.Count);
        Assert.Contains(best, p => p.Date == day && p.Metric == Calisthenics.PullUps && p.Value == 11);
        Assert.Contains(best, p => p.Date == day && p.Metric == Calisthenics.Plank && p.Value == 150);
        Assert.Contains(best, p => p.Date == day.PlusDays(1) && p.Metric == Calisthenics.Plank && p.Value == 170);
    }

    [Fact]
    public void Best_in_a_window_is_the_highest_recent_value()
    {
        var points = new[]
        {
            new BestSetPoint(new LocalDate(2026, 3, 1), Calisthenics.PullUps, 14),
            new BestSetPoint(new LocalDate(2026, 8, 1), Calisthenics.PullUps, 9),
            new BestSetPoint(new LocalDate(2026, 8, 20), Calisthenics.PullUps, 10)
        };

        var recent = Calisthenics.Best(points, Calisthenics.PullUps, since: new LocalDate(2026, 6, 1));

        Assert.NotNull(recent);
        Assert.Equal(10, recent!.Value);
        Assert.Null(Calisthenics.Best(points, Calisthenics.Plank, since: new LocalDate(2026, 1, 1)));
    }
}
