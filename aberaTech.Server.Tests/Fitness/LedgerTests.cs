using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>The nudges that keep the prediction ledger in use.</summary>
public sealed class LedgerTests
{
    private static readonly LocalDate Today = new(2026, 9, 7);

    private static LedgerEntry Entry(LocalDate target, double? actual = null, double predicted = 1000) =>
        new(Guid.NewGuid(), Today.PlusDays(-30), target, 2 * Vdot.MileMeters, predicted, actual);

    [Fact]
    public void An_empty_ledger_is_the_nudge_and_names_the_next_test()
    {
        var highlights = Ledger.Highlights([], Today.PlusDays(60), Today);

        var nudge = Assert.Single(highlights);
        Assert.Equal("ledger-empty", nudge.Kind);
        Assert.Contains("2026-11-06", nudge.Evidence);
        Assert.False(nudge.Positive);
    }

    [Fact]
    public void A_pending_prediction_silences_the_nudge_and_a_due_one_asks_for_its_score()
    {
        Assert.Empty(Ledger.Highlights([Entry(Today.PlusDays(30))], null, Today));

        var due = Ledger.Highlights([Entry(Today.PlusDays(-3)), Entry(Today.PlusDays(30))], null, Today);
        var ask = Assert.Single(due);
        Assert.Equal("ledger-due", ask.Kind);
        Assert.Contains("2026-09-04", ask.Evidence);
        Assert.Contains("16:40", ask.Evidence);
    }

    [Fact]
    public void Three_scored_predictions_make_a_record()
    {
        var entries = new[]
        {
            Entry(Today.PlusDays(-90), actual: 1010),
            Entry(Today.PlusDays(-60), actual: 1050),
            Entry(Today.PlusDays(-30), actual: 990),
            Entry(Today.PlusDays(30))
        };

        var record = Assert.Single(Ledger.Highlights(entries, null, Today), h => h.Kind == "ledger-record");
        Assert.Contains("3 predictions scored; 2 within 3%", record.Headline);
        Assert.True(record.Positive);
    }
}
