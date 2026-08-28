using aberaTech.Scheduling.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

public class EmailAddressTests
{
    [Theory]
    [InlineData("visitor@example.com")]
    [InlineData("first.last+tag@sub.example.co")]
    [InlineData("  padded@example.com  ")]
    public void Plausible_addresses_parse(string input)
    {
        Assert.True(EmailAddress.TryParse(input, out var email));
        Assert.Equal(input.Trim(), email!.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign.example.com")]
    [InlineData("@example.com")]
    [InlineData("visitor@")]
    [InlineData("two@at@example.com")]
    [InlineData("visitor@nodot")]
    [InlineData("visitor@.com")]
    [InlineData("visitor@example.")]
    [InlineData("has space@example.com")]
    public void Implausible_addresses_do_not(string? input)
    {
        Assert.False(EmailAddress.TryParse(input, out _));
    }

    [Fact]
    public void The_column_width_is_the_ceiling()
    {
        var longest = new string('a', EmailAddress.MaxLength - "@b.co".Length) + "@b.co";

        Assert.True(EmailAddress.TryParse(longest, out _));
        Assert.False(EmailAddress.TryParse("a" + longest, out _));
    }
}
