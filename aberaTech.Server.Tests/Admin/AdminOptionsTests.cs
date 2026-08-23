using aberaTech.Scheduling.Admin;
using Xunit;

namespace aberaTech.Server.Tests.Admin;

public class AdminOptionsTests
{
    private static AdminOptions Configured() => new()
    {
        GoogleClientId = "id",
        GoogleClientSecret = "secret",
        AllowedEmails = ["nebyouabera@gmail.com"]
    };

    [Fact]
    public void An_allowed_address_is_recognised_whatever_its_case()
    {
        // Google may return a differently cased address than the one configured,
        // and an ordinal comparison would lock the owner out of his own queue.
        var options = Configured();

        Assert.True(options.Allows("nebyouabera@gmail.com"));
        Assert.True(options.Allows("NebyouAbera@Gmail.com"));
    }

    [Theory]
    [InlineData("someone.else@gmail.com")]
    [InlineData("nebyouabera@gmail.com.attacker.test")]
    [InlineData("")]
    [InlineData(null)]
    public void Everybody_else_is_refused(string? email)
    {
        Assert.False(Configured().Allows(email));
    }

    [Fact]
    public void An_empty_allowlist_admits_nobody()
    {
        // The dangerous default. An empty list must mean "nobody", never
        // "everybody", so a half-finished configuration fails closed.
        var options = new AdminOptions { GoogleClientId = "id", GoogleClientSecret = "secret" };

        Assert.False(options.Allows("nebyouabera@gmail.com"));
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Configuration_needs_credentials_and_at_least_one_address()
    {
        Assert.False(new AdminOptions { AllowedEmails = ["a@b.test"] }.IsConfigured);
        Assert.False(new AdminOptions { GoogleClientId = "id", AllowedEmails = ["a@b.test"] }.IsConfigured);
        Assert.True(Configured().IsConfigured);
    }
}
