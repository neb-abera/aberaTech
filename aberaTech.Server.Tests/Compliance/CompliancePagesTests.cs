using aberaTech.Scheduling.Compliance;
using Xunit;

namespace aberaTech.Server.Tests.Compliance;

/// <summary>
/// Pins the phrases A2P 10DLC campaign vetting checks for (Twilio error
/// 30908). The first campaign submission was rejected over the privacy
/// policy, and a rewording that drops one of these phrases would fail the
/// next one — silently, months later, when the campaign is next reviewed.
/// </summary>
public class CompliancePagesTests
{
    [Fact]
    public void The_privacy_policy_carries_the_non_sharing_statement_vetting_requires()
    {
        // The statement must say mobile information and consent are not
        // shared with third parties or affiliates for marketing. Twilio's
        // own passing example uses "share, sell, or provide"; ours keeps
        // all three verbs.
        Assert.Contains("not shared, sold, or provided to third parties or affiliates", CompliancePages.Privacy);
        Assert.Contains("marketing", CompliancePages.Privacy);
        Assert.Contains("opt-in data and consent are never shared", CompliancePages.Privacy, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_privacy_policy_itself_carries_the_messaging_disclosures()
    {
        // 30908 lists these as rejection causes when absent from the privacy
        // policy specifically — having them only on the terms page is not
        // enough.
        Assert.Contains("Message frequency varies", CompliancePages.Privacy);
        Assert.Contains("Message and data rates may apply", CompliancePages.Privacy);
        Assert.Contains("STOP", CompliancePages.Privacy);
        Assert.Contains("HELP", CompliancePages.Privacy);
    }

    [Fact]
    public void The_privacy_policy_says_what_is_collected_and_what_it_is_used_for()
    {
        Assert.Contains("What is collected", CompliancePages.Privacy);
        Assert.Contains("your mobile number", CompliancePages.Privacy);
    }

    [Fact]
    public void The_terms_carry_frequency_rates_and_the_keywords()
    {
        Assert.Contains("Message frequency varies", CompliancePages.Terms);
        Assert.Contains("Message and data rates may apply", CompliancePages.Terms);
        Assert.Contains("STOP", CompliancePages.Terms);
        Assert.Contains("HELP", CompliancePages.Terms);
    }

    [Fact]
    public void The_pages_link_each_other_so_review_finds_one_policy_not_two()
    {
        Assert.Contains("/sms-privacy", CompliancePages.Terms);
        Assert.Contains("/sms-terms", CompliancePages.Privacy);
    }
}
