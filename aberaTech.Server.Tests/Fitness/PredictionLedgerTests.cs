using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A model nothing ever checks is a calculator with good manners. This is the
/// ledger that makes it falsifiable: what was said, what it assumed, and what
/// happened.
/// </summary>
public sealed class PredictionLedgerTests
{
    private static readonly LocalDate Today = new(2026, 9, 5);

    private static LockedPrediction Made(
        LocalDate target, double predicted = 2100, double fast = 2000, double slow = 2200,
        double? actual = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            MadeOn = new LocalDate(2026, 9, 1),
            TargetDate = target,
            DistanceMeters = 8046.72,
            PredictedSeconds = predicted,
            PredictedFastSeconds = fast,
            PredictedSlowSeconds = slow,
            WeeklyHours = 7,
            Compliance = 0.85,
            ActualSeconds = actual
        };

    [Fact]
    public void A_prediction_waits_until_its_date_then_asks_to_be_answered()
    {
        Assert.Equal("pending", PredictionLedger.StatusOf(Made(Today.PlusDays(30)), Today));
        Assert.Equal("due", PredictionLedger.StatusOf(Made(Today), Today));
        Assert.Equal("due", PredictionLedger.StatusOf(Made(Today.PlusDays(-1)), Today));
    }

    [Fact]
    public void Once_answered_it_stays_answered()
    {
        var scored = Made(Today.PlusDays(-10), actual: 2150);
        Assert.Equal("scored", PredictionLedger.StatusOf(scored, Today));
    }

    [Fact]
    public void The_error_is_signed_so_the_direction_of_the_miss_survives()
    {
        // Positive is slower than predicted. A model that is wrong in one
        // direction is biased; one that is wrong in both is merely imprecise,
        // and averaging unsigned errors hides which.
        var slower = PredictionLedger.Dto(Made(Today.PlusDays(-1), actual: 2160), Today);
        var faster = PredictionLedger.Dto(Made(Today.PlusDays(-1), actual: 2040), Today);

        Assert.Equal(60, slower.ErrorSeconds);
        Assert.Equal(-60, faster.ErrorSeconds);
    }

    [Fact]
    public void Whether_the_interval_held_is_recorded_separately_from_the_miss()
    {
        // The interval is the claim worth scoring. A median that is off by a
        // minute inside an interval that contained the day is a model working
        // correctly; the same miss outside it is not.
        var inside = PredictionLedger.Dto(Made(Today.PlusDays(-1), actual: 2180), Today);
        var outside = PredictionLedger.Dto(Made(Today.PlusDays(-1), actual: 2400), Today);

        Assert.True(inside.InsideInterval);
        Assert.False(outside.InsideInterval);

        Assert.True(Math.Abs(inside.ErrorSeconds!.Value) > 0);
    }

    [Fact]
    public void An_unanswered_prediction_scores_nothing_rather_than_zero()
    {
        // A pending prediction with an error of zero would read as a perfect
        // one and quietly flatter every average taken over the ledger.
        var pending = PredictionLedger.Dto(Made(Today.PlusDays(30)), Today);

        Assert.Null(pending.ErrorSeconds);
        Assert.Null(pending.InsideInterval);
        Assert.Null(pending.ActualSeconds);
    }

    [Fact]
    public void The_plan_it_assumed_travels_with_it()
    {
        // A miss is only attributable if the assumptions are attached: the
        // difference between the model being wrong and the plan not happening.
        var dto = PredictionLedger.Dto(Made(Today.PlusDays(60)), Today);

        Assert.Equal(7, dto.WeeklyHours);
        Assert.Equal(0.85, dto.Compliance);
        Assert.Equal("2026-09-01", dto.MadeOn);
    }
}
