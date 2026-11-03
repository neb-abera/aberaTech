using aberaTech.Fitness;
using aberaTech.Fitness.Api;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The week in one page, and the one key that lets the morning brief read it.
/// </summary>
public sealed class DigestTests
{
    private static readonly LocalDate Today = new(2026, 9, 9); // a Wednesday

    /// <summary>
    /// 28 days to <see cref="Today"/>: 85 minutes in the last seven, 210 in
    /// the seven before, and 105 in the fortnight before that, 400 in all.
    /// </summary>
    private static DayVolumeDto[] Days()
    {
        var minutes = new Dictionary<int, double> { [0] = 45, [3] = 40, [8] = 70, [10] = 70, [12] = 70, [20] = 105 };
        return Enumerable.Range(0, 28).Reverse()
            .Select(back => new DayVolumeDto(Today.PlusDays(-back).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), minutes.GetValueOrDefault(back)))
            .ToArray();
    }

    private static SummaryDto Summary() =>
        new(
            new SettingsDto(152, 340, 160, 35.4, "2026-04-03", 79.4, 1991, null, 7, null, null, null, null, null, null, 0.1, 15, 168),
            [new AerobicPointDto("2026-07", 447, 2, 0), new AerobicPointDto("2026-08", 410, 11, 4)],
            [new WeekVolumeDto("2026-08-31", 210), new WeekVolumeDto("2026-09-07", 85)],
            Days(),
            [],
            [
                new HighlightDto("aerobic-gain", "Aerobic base up 8% month over month", "Median pace 7:27/km → 6:50/km.", true),
                new HighlightDto("ledger-empty", "Nothing locked in the prediction ledger", "Lock the model's 2-mile.", false)
            ],
            [],
            new DoseDto(3.1, 0.3, 0.1, 1.2, 3.5, 5.9, 0.886, []),
            [],
            0.147,
            25,
            new ReadinessDto(
                new RuckReportDto(20.41, 0.67, [], [], null, null, 0, []),
                new CalisthenicsDto([], []),
                new BodyReportDto([], null, null, null, null),
                []),
            [],
            new ThresholdSuggestionDto(158, null, null, "The test held 158 bpm.", "uphill-athlete-hr-drift"),
            new DurabilityDto(10.5, 7.9, 1.33, 1.4, 14.7, 4, 1, 60, [], []));

    private static GoalsDto Goals() =>
        new(
            new GoalSourceDto("OneDrive", true, true, "2026-09-09T06:00:00Z", null, null, "goals"),
            "2027-12-31",
            7,
            "the hours a week the profile says you can train (7.0 h)",
            [
                new GoalSectionDto("Army Fitness Test", "aft", 428, 500, 500, null,
                [
                    new GoalRowDto(11, "2 mile run", "run-2mi", "s", true, 1009, null, 735, "F",
                        new StandingDto("run-2mi", 1009, "Measured", "AFT of 2026-04-03", "2026-04-03"),
                        false, 83, 100, new GoalForecastDto(0.34, 760, "trajectory", "Needs VDOT 51.", 9.1), null)
                ]),
                new GoalSectionDto("Strength", null, null, null, null, null,
                [
                    new GoalRowDto(20, "Farmer carry 1.5x bw m", "farmer-carry-1.5bw-m", "m", false, 25, null, 100, "D",
                        null, null, null, null, null, null)
                ])
            ]);

    [Fact]
    public void The_page_covers_training_engine_goals_and_what_needs_attention()
    {
        var digest = DigestReports.Compose(Summary(), Goals(), Today);

        Assert.Equal("2026-09-07", digest.WeekStart);
        var text = digest.Text;

        Assert.Contains("week of 2026-09-07", text);
        Assert.Contains("Endurance: 85 min in the last 7 days, 210 min the 7 before, 100 min/wk over 28 days; plan 160/wk.", text);
        Assert.Contains("Calendar week so far: 85 min.", text);
        Assert.Contains("3.5 h/wk running (89% easy), 1.2 h strength", text);
        Assert.Contains("acute:chronic 1.33, monotony 1.4, 4 days of impact in a row, 1 rest days in 7", text);
        Assert.Contains("AeT pace at 152 bpm: 6:50/km over 11 runs in 2026-08 (8.3% vs 2026-07), 4 on a treadmill", text);
        Assert.Contains("VDOT anchor 35.4 from 2026-04-03", text);
        Assert.Contains("AeT–LT spread 15% (over the 10% line: base volume)", text);
        Assert.Contains("a test suggests AeT 158 bpm", text);
        Assert.Contains("GOALS (due 2027-12-31)", text);
        Assert.Contains("  Army Fitness Test: 428 now, goal 500 of 500 pts", text);
        Assert.Contains("    2 mile run: 16:49 now, goal 12:15, 34% by the due date at 7.0 h/wk.", text);
        Assert.Contains("    Farmer carry 1.5x bw m: untested now, goal 100 m.", text);
        Assert.DoesNotContain("GATES", text);
        Assert.DoesNotContain("selection", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("- Nothing locked in the prediction ledger.", text);
        Assert.Contains("+ Aerobic base up 8% month over month.", text);

        // The lines and the text are the same page.
        Assert.Equal(string.Join("\n", digest.Lines), text);
    }

    [Fact]
    public void Without_a_workbook_or_a_trend_the_page_says_so_rather_than_breaking()
    {
        var summary = Summary() with
        {
            AerobicTrend = [],
            DeficiencySpread = null,
            ThresholdSuggestion = null,
            Highlights = []
        };
        var goals = Goals() with
        {
            DueOn = null,
            Source = Goals().Source with { Error = "OneDrive did not answer in time." },
            Sections = []
        };

        var text = DigestReports.Compose(summary, goals, Today).Text;

        Assert.Contains("GOALS\n", text);
        Assert.Contains("The workbook could not be read: OneDrive did not answer in time.", text);
        Assert.Contains("Nothing flagged.", text);
        Assert.DoesNotContain("AeT pace at", text);
    }

    [Fact]
    public void The_digest_key_is_bearer_only_exact_and_at_least_32_characters()
    {
        var key = "0123456789abcdef0123456789abcdef";

        Assert.True(FitnessEndpoints.DigestKeyAllows($"Bearer {key}", key));
        Assert.True(FitnessEndpoints.DigestKeyAllows($"Bearer {key} ", $" {key}"));
        Assert.False(FitnessEndpoints.DigestKeyAllows($"Bearer {key}x", key));
        Assert.False(FitnessEndpoints.DigestKeyAllows($"bearer {key}", key));
        Assert.False(FitnessEndpoints.DigestKeyAllows(key, key));
        Assert.False(FitnessEndpoints.DigestKeyAllows("", key));

        // A short key would be guessable; it is refused however it is sent.
        Assert.False(FitnessEndpoints.DigestKeyAllows("Bearer short", "short"));
        Assert.False(new FitnessOptions { DigestKey = "short" }.HasDigestKey);
        Assert.True(new FitnessOptions { DigestKey = key }.HasDigestKey);
    }
}

public sealed class DailyVolumeTests
{
    [Fact]
    public void The_series_is_182_days_ending_today_with_empty_days_at_zero()
    {
        var zone = NodaTime.DateTimeZone.Utc;
        var today = new LocalDate(2026, 9, 9);
        aberaTech.Fitness.Data.Activity Run(LocalDate day, double minutes) => new()
        {
            Id = Guid.NewGuid(),
            Source = "test",
            Sport = "run",
            Name = "Run",
            StartedAt = day.AtMidnight().InUtc().ToInstant().Plus(Duration.FromHours(7)),
            DurationSeconds = minutes * 60
        };

        var days = aberaTech.Fitness.Api.FitnessReports.DailyVolumes(
            [Run(today, 30), Run(today, 15), Run(today.PlusDays(-6), 40), Run(today.PlusDays(-200), 99)], zone, today);

        Assert.Equal(182, days.Count);
        Assert.Equal("2026-09-09", days[^1].Date);
        Assert.Equal(45, days[^1].Minutes);
        Assert.Equal(40, days[^7].Minutes);
        Assert.Equal(85, aberaTech.Fitness.Api.Rolling.Of(days).Last7);
        Assert.Equal(0, aberaTech.Fitness.Api.Rolling.Of(days).Previous7);
        Assert.DoesNotContain(days, d => d.Minutes == 99);
    }
}
