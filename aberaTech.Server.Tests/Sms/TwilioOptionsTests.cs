using aberaTech.Scheduling.Sms;
using Xunit;

namespace aberaTech.Server.Tests.Sms;

public class TwilioOptionsTests
{
    private static TwilioOptions Full() => new()
    {
        AccountSid = "AC123",
        AuthToken = "token",
        FromNumber = "+15005550006",
        StatusCallbackUrl = "https://abera.tech/api/scheduling/sms-status"
    };

    [Fact]
    public void All_four_settings_are_needed()
    {
        Assert.True(Full().IsConfigured);
    }

    [Fact]
    public void Without_a_callback_url_it_is_not_configured()
    {
        // The one worth stating. Credentials without a callback URL would send
        // successfully and then dead letter every message, because no delivery
        // receipt would ever arrive to settle them.
        var options = Full();
        options.StatusCallbackUrl = string.Empty;

        Assert.False(options.IsConfigured);
        Assert.True(options.IsPartiallyConfigured);
    }

    [Theory]
    [InlineData("AccountSid")]
    [InlineData("AuthToken")]
    [InlineData("FromNumber")]
    public void Any_missing_setting_leaves_it_unconfigured(string missing)
    {
        var options = Full();
        typeof(TwilioOptions).GetProperty(missing)!.SetValue(options, string.Empty);

        Assert.False(options.IsConfigured);
        Assert.True(options.IsPartiallyConfigured);
    }

    [Fact]
    public void Nothing_configured_is_not_a_partial_configuration()
    {
        // Nothing set is a deployment without SMS, which is fine and silent.
        // Something set but not enough is a mistake, and should be noisy.
        var options = new TwilioOptions();

        Assert.False(options.IsConfigured);
        Assert.False(options.IsPartiallyConfigured);
    }
}
