using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>What a rule matches, and where rules sit among the page's choice, the #critical mark and the default.</summary>
public sealed class AlertTypeRuleTests
{
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 28, 14, 0);

    private static PlannedAlert Event(string title = "Fire drill", string? location = null, string? organizer = null, bool critical = false) =>
        new("drill|1", title, location, Start, Start - Duration.FromMinutes(10), AlertSource.Reminder, "drill",
            Critical: critical, Organizer: organizer);

    private static AlertSettings With(params AlertTypeRule[] rules) =>
        AlertSettings.Defaults(new AlertsOptions()) with { Rules = rules };

    [Theory]
    [InlineData("title", "DRILL", true)]
    [InlineData("title", "dentist", false)]
    [InlineData("organizer", "army.mil", true)]
    [InlineData("organizer", "", true)]
    [InlineData("organizer", "mitre.org", false)]
    [InlineData("location", "", false)]
    public void A_rule_matches_when_its_field_contains_the_text_in_any_case(string field, string text, bool matches) =>
        Assert.Equal(matches, new AlertTypeRule(field, text, AlertTypes.Alarm).Matches(Event(organizer: "OPS@ARMY.MIL")));

    [Fact]
    public void A_blank_location_rule_matches_any_event_with_a_place_and_none_without()
    {
        var rule = new AlertTypeRule(AlertTypeRuleFields.Location, "", AlertTypes.Notification);

        Assert.True(rule.Matches(Event(location: "Room 1")));
        Assert.False(rule.Matches(Event(location: null)));
        Assert.False(rule.Matches(Event(location: "  ")));
    }

    [Fact]
    public void The_first_matching_rule_wins()
    {
        var settings = With(
            new AlertTypeRule(AlertTypeRuleFields.Title, "lunch", AlertTypes.None),
            new AlertTypeRule(AlertTypeRuleFields.Organizer, "army.mil", AlertTypes.Alarm),
            new AlertTypeRule(AlertTypeRuleFields.Title, "drill", AlertTypes.Notification));

        Assert.Equal(new EffectiveType(AlertTypes.Alarm, AlertTypes.FromRule),
            AlertTypes.Resolve(Event(organizer: "ops@army.mil"), null, settings));
    }

    [Fact]
    public void The_page_choice_then_the_mark_come_before_any_rule_and_the_default_comes_after()
    {
        var settings = With(new AlertTypeRule(AlertTypeRuleFields.Title, "drill", AlertTypes.Notification));

        Assert.Equal(AlertTypes.FromPage, AlertTypes.Resolve(Event(), AlertTypes.None, settings).From);
        Assert.Equal(new EffectiveType(AlertTypes.Alarm, AlertTypes.FromMark), AlertTypes.Resolve(Event(critical: true), null, settings));
        Assert.Equal(AlertTypes.FromRule, AlertTypes.Resolve(Event(), null, settings).From);
        Assert.Equal(new EffectiveType(AlertTypes.None, AlertTypes.FromDefault), AlertTypes.Resolve(Event("Review"), null, settings));
    }

    [Fact]
    public void Validation_trims_text_and_keeps_the_order()
    {
        var (rules, errors) = AlertTypeRuleBounds.Validate(
        [
            new AlertTypeRuleRequest("organizer", "  army.mil ", "alarm"),
            new AlertTypeRuleRequest("location", null, "notification"),
        ]);

        Assert.Empty(errors);
        Assert.Equal(
            [new AlertTypeRule("organizer", "army.mil", "alarm"), new AlertTypeRule("location", "", "notification")],
            rules);
    }
}
