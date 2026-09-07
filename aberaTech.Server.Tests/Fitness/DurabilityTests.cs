using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>How the load is being carried: the ratio, the monotony, the streak.</summary>
public sealed class DurabilityTests
{
    private static readonly LocalDate Today = new(2026, 9, 7);

    private static LoadedSession Easy(LocalDate date, double hours) =>
        new(date, "run", new TrainingDose(EasyHours: hours));

    private static LoadedSession Lift(LocalDate date, double hours) =>
        new(date, "strength", new TrainingDose(StrengthHours: hours));

    /// <summary>An hour easy every day for the days given, counted back from today.</summary>
    private static List<LoadedSession> Daily(int days, double hours = 1) =>
        Enumerable.Range(0, days).Select(i => Easy(Today.PlusDays(-i), hours)).ToList();

    [Fact]
    public void Load_is_strain_and_the_ratio_is_this_week_over_the_average_week()
    {
        // Four flat weeks of an hour a day, then this week at 1.5 h a day.
        var sessions = Daily(35).Select(s => s.Date > Today.PlusDays(-7) ? Easy(s.Date, 1.5) : s).ToList();

        var report = Durability.Build(sessions, Today);

        Assert.Equal(10.5, report.AcuteLoad, precision: 6);
        // 7 days at 1.5 plus 21 at 1.0 over four weeks: 31.5 / 4.
        Assert.Equal(7.875, report.ChronicLoad, precision: 6);
        Assert.Equal(10.5 / 7.875, report.Acwr!.Value, precision: 6);
        Assert.Equal(35, report.DaysOfLog);
        Assert.Equal(28, report.Days.Count);
    }

    [Fact]
    public void Intervals_weigh_more_than_easy_hours_and_lifting_counts()
    {
        var sessions = new List<LoadedSession>
        {
            new(Today, "run", new TrainingDose(IntervalHours: 1)),
            Lift(Today.PlusDays(-1), 1)
        };

        var report = Durability.Build(sessions, Today);

        Assert.Equal(4.5 + 1.5, report.AcuteLoad, precision: 6);
    }

    [Fact]
    public void The_ratio_waits_for_four_weeks_of_log()
    {
        var report = Durability.Build(Daily(20), Today);

        Assert.Null(report.Acwr);
        Assert.Contains("needs 28 days of log; has 20", report.Steps[1].Value);
    }

    [Fact]
    public void A_flat_week_is_monotonous_and_a_contrasted_one_is_not()
    {
        var flat = Durability.Build(Daily(28), Today);
        Assert.Null(flat.Monotony); // no spread at all: every day identical

        // Six identical days and one slightly different: a huge monotony.
        var nearlyFlat = Daily(28).Select(s => s.Date == Today ? Easy(s.Date, 1.1) : s).ToList();
        Assert.True(Durability.Build(nearlyFlat, Today).Monotony > Durability.MonotonyLimit);

        // Hard/easy/off: low monotony.
        var contrasted = Daily(28)
            .Where(s => (int)s.Date.DayOfWeek % 7 != 0)
            .Select(s => (int)s.Date.DayOfWeek % 3 == 0 ? new LoadedSession(s.Date, "run", new TrainingDose(ThresholdHours: 1)) : s)
            .ToList();
        Assert.True(Durability.Build(contrasted, Today).Monotony < Durability.MonotonyLimit);
    }

    [Fact]
    public void The_streak_counts_impact_days_back_from_the_latest_and_lifting_is_a_rest_from_it()
    {
        var sessions = Daily(12);
        Assert.Equal(12, Durability.Streak(sessions, Today));

        // A lifting day in the middle breaks it.
        var broken = sessions.Select(s => s.Date == Today.PlusDays(-4) ? Lift(s.Date, 1) : s).ToList();
        Assert.Equal(4, Durability.Streak(broken, Today));

        // Nothing logged today yet: yesterday's streak still stands.
        Assert.Equal(11, Durability.Streak(sessions.Where(s => s.Date != Today).ToList(), Today));

        // A five-minute jog is not an impact day.
        Assert.Equal(0, Durability.Streak([Easy(Today, 5.0 / 60)], Today));
    }

    [Fact]
    public void The_highlights_name_the_spike_the_monotony_and_the_streak()
    {
        var spike = new DurabilityReport(12, 6, 2.0, 1.2, 14.4, 3, 2, 40, [], []);
        var h = Durability.Highlights(spike);
        Assert.Contains(h, x => x.Kind == "durability-spike" && !x.Positive);

        var monotonous = new DurabilityReport(7, 7, 1.0, 3.5, 24.5, 12, 0, 40, [], []);
        var kinds = Durability.Highlights(monotonous).Select(x => x.Kind).ToArray();
        Assert.Contains("durability-monotony", kinds);
        Assert.Contains("durability-streak", kinds);
        Assert.DoesNotContain("durability-steady", kinds);

        var steady = new DurabilityReport(7, 7, 1.0, 1.4, 9.8, 3, 2, 40, [], []);
        var one = Assert.Single(Durability.Highlights(steady));
        Assert.Equal("durability-steady", one.Kind);
        Assert.True(one.Positive);

        // No ratio yet, nothing wrong: nothing to say.
        Assert.Empty(Durability.Highlights(new DurabilityReport(7, 7, null, 1.4, 9.8, 3, 2, 10, [], [])));
    }
}
