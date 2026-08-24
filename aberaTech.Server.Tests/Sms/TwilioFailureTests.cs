using aberaTech.Scheduling.Sms;
using Xunit;

namespace aberaTech.Server.Tests.Sms;

/// <summary>
/// Which failures are worth another attempt. Getting this wrong in either
/// direction is expensive: retrying the permanent ones spends a daily carrier
/// allowance on messages that cannot arrive, and giving up on the transient
/// ones loses messages that would have.
/// </summary>
public class TwilioFailureTests
{
    [Theory]
    [InlineData(21610)] // replied STOP
    [InlineData(21211)] // not a valid number
    [InlineData(21614)] // not a mobile
    [InlineData(30005)] // number does not exist
    [InlineData(30006)] // landline
    [InlineData(30004)] // blocked
    public void A_number_that_cannot_receive_is_not_retried(int code)
    {
        var (kind, reason) = TwilioFailure.Classify(code, 400);

        Assert.Equal(FailureKind.Permanent, kind);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void An_opt_out_says_so_in_words_somebody_can_act_on()
    {
        // This one matters most. Continuing to attempt a number that replied
        // STOP is futile and is the kind of thing that gets a sending number
        // reviewed, so the reason has to be unmistakable in the log.
        var (_, reason) = TwilioFailure.Classify(21610, 400);

        Assert.Contains("STOP", reason);
    }

    [Fact]
    public void An_opt_out_is_recognised_as_one_rather_than_a_generic_failure()
    {
        // The distinction matters: a permanent failure stops this message, and
        // an opt-out has to stop every future one to that number as well.
        Assert.True(TwilioFailure.IsOptOut(21610));
    }

    [Theory]
    [InlineData(21211)] // invalid number
    [InlineData(30006)] // landline
    [InlineData(429)]
    [InlineData(null)]
    public void Other_failures_are_not_treated_as_opt_outs(int? code)
    {
        // Suppressing a number because a handset was switched off would silently
        // stop somebody hearing from us with no way for them to know why.
        Assert.False(TwilioFailure.IsOptOut(code));
    }

    [Fact]
    public void Rate_limiting_is_worth_another_go()
    {
        var (kind, _) = TwilioFailure.Classify(null, 429);

        Assert.Equal(FailureKind.Transient, kind);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void A_provider_server_error_is_worth_another_go(int status)
    {
        Assert.Equal(FailureKind.Transient, TwilioFailure.Classify(null, status).Kind);
    }

    [Fact]
    public void An_unrecognised_code_is_treated_as_transient()
    {
        // Retrying something retryable costs a little. Giving up on something
        // deliverable costs the message, so the unknown case errs that way.
        var (kind, _) = TwilioFailure.Classify(99999, 400);

        Assert.Equal(FailureKind.Transient, kind);
    }

    [Fact]
    public void A_failure_with_no_code_at_all_is_transient()
    {
        Assert.Equal(FailureKind.Transient, TwilioFailure.Classify(null, 400).Kind);
    }

    [Fact]
    public void The_error_code_is_read_out_of_a_twilio_error_body()
    {
        const string body = """
            {"code":21610,"message":"Attempt to send to unsubscribed recipient","status":400}
            """;

        Assert.Equal(21610, TwilioFailure.ReadErrorCode(body));
    }

    [Theory]
    [InlineData("""{"message":"no code here"}""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void A_body_without_a_usable_code_yields_null(string body)
    {
        Assert.Null(TwilioFailure.ReadErrorCode(body));
    }
}
