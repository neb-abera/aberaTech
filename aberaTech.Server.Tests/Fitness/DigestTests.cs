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

    private static SummaryDto Summary() =>
        new(
            new SettingsDto(152, 340, 160, 35.4, "2026-04-03", 79.4, 1991, null, 7, null, null, null, null, null, null, 0.1, 15, "2028-04-01", 168),
            [new AerobicPointDto("2026-07", 447, 2, 0), new AerobicPointDto("2026-08", 410, 11, 4)],
            [new WeekVolumeDto("2026-08-31", 210), new WeekVolumeDto("2026-09-07", 85)],
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
                "2028-04-01",
                [new GateDto("sfas-day-one", "SFAS day-one minimums", "", 52, "2027-04-03", "Fail", 1, 2, [], [])],
                new RuckReportDto(20.41, 0.67, [], [], null, null, 0, []),
                new CalisthenicsDto([], []),
                new BodyReportDto([], null, null, null, null),
                []),
            [],
            new ThresholdSuggestionDto(158, null, null, "The test held 158 bpm.", "uphill-athlete-hr-drift"),
            new DurabilityDto(10.5, 7.9, 1.33, 1.4, 14.7, 4, 1, 60, [], []));

    private static OutlookDto Outlook() =>
        new(
            "2028-04-01", 4.5, 4.5, 1.0, 35.4,
            [new OutlookGateDto("sfas-day-one", "SFAS day-one minimums", 52, "2027-04-03", 6.9, 0.62, 2, 3, 4.5, [])],
            "2028-01-22",
            "sfas-day-one",
            []);

    [Fact]
    public void The_page_covers_training_engine_gates_and_what_needs_attention()
    {
        var digest = DigestReports.Compose(Summary(), Outlook(), Today);

        Assert.Equal("2026-09-07", digest.WeekStart);
        var text = digest.Text;

        Assert.Contains("week of 2026-09-07", text);
        Assert.Contains("Endurance: 85 min this week so far, 210 min last week, plan 160.", text);
        Assert.Contains("3.5 h/wk running (89% easy), 1.2 h strength", text);
        Assert.Contains("acute:chronic 1.33, monotony 1.4, 4 days of impact in a row, 1 rest days in 7", text);
        Assert.Contains("AeT pace at 152 bpm: 6:50/km over 11 runs in 2026-08 (8.3% vs 2026-07), 4 on a treadmill", text);
        Assert.Contains("VDOT anchor 35.4 from 2026-04-03", text);
        Assert.Contains("AeT–LT spread 15% (over the 10% line: base volume)", text);
        Assert.Contains("a test suggests AeT 158 bpm", text);
        Assert.Contains("GATES (selection 2028-04-01)", text);
        Assert.Contains("SFAS day-one minimums: 1/3 clear today; 62% by 2027-04-03 at 4.5 h/wk (2/3 lines)", text);
        Assert.Contains("Earliest selection every forecast gate is ready for: 2028-01-22", text);
        Assert.Contains("- Nothing locked in the prediction ledger.", text);
        Assert.Contains("+ Aerobic base up 8% month over month.", text);

        // The lines and the text are the same page.
        Assert.Equal(string.Join("\n", digest.Lines), text);
    }

    [Fact]
    public void Without_a_selection_date_or_a_trend_the_page_says_so_rather_than_breaking()
    {
        var summary = Summary() with
        {
            AerobicTrend = [],
            DeficiencySpread = null,
            ThresholdSuggestion = null,
            Highlights = [],
            Readiness = Summary().Readiness with { SelectionDate = null }
        };
        var outlook = Outlook() with { SelectionDate = null, EarliestSelectionDate = null, BindingGate = null };

        var text = DigestReports.Compose(summary, outlook, Today).Text;

        Assert.Contains("GATES (no selection date set)", text);
        Assert.Contains("No selection date can be named yet", text);
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
