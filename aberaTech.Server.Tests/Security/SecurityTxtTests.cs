using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// RFC 9116: a Contact, an Expires no more than a year out, and the
/// canonical address of the file itself.
/// </summary>
public sealed class SecurityTxtTests
{
    [Fact]
    public void Expires_is_a_year_from_today_to_the_day()
    {
        // 18:30 UTC on the 21st, whatever the caller's offset says.
        var text = SecurityTxt.Render(new DateTimeOffset(2026, 9, 21, 14, 30, 0, TimeSpan.FromHours(-4)));

        Assert.Contains("Expires: 2027-09-21T00:00:00Z", text);
    }

    [Fact]
    public void Names_both_channels_and_itself()
    {
        var lines = SecurityTxt.Render(DateTimeOffset.UtcNow).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Contact: " + SecurityTxt.Advisories, lines[0].TrimEnd());
        Assert.Contains("Contact: " + SecurityTxt.Contact, lines.Select(line => line.TrimEnd()));
        Assert.Contains("Canonical: " + SecurityTxt.Canonical, lines.Select(line => line.TrimEnd()));
        Assert.Contains("Policy: " + SecurityTxt.Policy, lines.Select(line => line.TrimEnd()));
    }
}
