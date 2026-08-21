using aberaTech.Scheduling.Outbox;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Outbox;

/// <summary>
/// The retry behaviour that the paid tools did not have.
/// </summary>
public class DeliveryPolicyTests
{
    private static readonly Instant Now = Instant.FromUtc(2027, 6, 1, 14, 0);

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(5, 480)]
    public void Backoff_widens_exponentially(int attempt, int expectedSeconds)
    {
        Assert.Equal(Duration.FromSeconds(expectedSeconds), DeliveryPolicy.BackoffFor(attempt));
    }

    [Fact]
    public void Attempts_are_counted_from_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryPolicy.BackoffFor(0));
    }

    [Fact]
    public void A_failure_short_of_the_limit_is_rescheduled()
    {
        var next = DeliveryPolicy.NextAttemptAt(attemptsMade: 1, Now);

        Assert.Equal(Now + Duration.FromSeconds(30), next);
    }

    [Fact]
    public void The_last_attempt_schedules_nothing_further()
    {
        Assert.Null(DeliveryPolicy.NextAttemptAt(DeliveryPolicy.MaxAttempts, Now));
        Assert.True(DeliveryPolicy.ShouldDeadLetter(DeliveryPolicy.MaxAttempts));
    }

    [Fact]
    public void A_message_is_not_dead_lettered_while_attempts_remain()
    {
        Assert.False(DeliveryPolicy.ShouldDeadLetter(DeliveryPolicy.MaxAttempts - 1));
    }

    [Fact]
    public void The_whole_retry_schedule_stays_inside_the_useful_life_of_the_message()
    {
        // Five attempts across the backoff above is 30s + 1m + 2m + 4m, so the
        // last try lands about eight minutes in. Retrying for an hour would
        // deliver an alert about an appointment that has already happened.
        var total = Duration.Zero;
        for (var attempt = 1; attempt < DeliveryPolicy.MaxAttempts; attempt++)
        {
            total += DeliveryPolicy.BackoffFor(attempt);
        }

        Assert.True(total < Duration.FromMinutes(10), $"retry schedule spans {total}");
    }

    [Fact]
    public void An_accepted_message_with_no_receipt_is_eventually_treated_as_lost()
    {
        // The exact failure mode behind "it said it sent and nobody got it".
        var sentAt = Now;

        Assert.False(DeliveryPolicy.ReceiptOverdue(sentAt, Now + Duration.FromMinutes(1)));
        Assert.True(DeliveryPolicy.ReceiptOverdue(sentAt, Now + DeliveryPolicy.ReceiptWindow));
    }
}
