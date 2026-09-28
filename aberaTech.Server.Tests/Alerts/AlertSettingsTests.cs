using aberaTech.Scheduling.Alerts;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The settings on their own: the configuration's values as the defaults,
/// what Pushover is asked for at each priority, and the 50-sound cap.
/// AlertsRouteTests hold the form's bounds through the route.
/// </summary>
public sealed class AlertSettingsTests
{
    [Fact]
    public void With_nothing_configured_the_defaults_are_todays_behaviour()
    {
        var settings = AlertSettings.Defaults(new AlertsOptions());

        Assert.Equal(new PushoverDelivery(2, 60, 10800, null), settings.Delivery);
        Assert.Equal(10, settings.DefaultLeadMinutes);
        Assert.Equal(5, settings.PollMinutes);
        Assert.Equal(48, settings.LookaheadHours);
        Assert.False(settings.IncludeAllDay);
        Assert.Equal("", settings.TimeZone);
        Assert.Empty(settings.OwnerEmails);
    }

    [Fact]
    public void Configured_values_outside_the_bounds_are_pulled_inside_them()
    {
        var settings = AlertSettings.Defaults(new AlertsOptions
        {
            Priority = 7,
            RepeatSeconds = 5,
            StopAfterMinutes = 999,
            Sound = "foghorn",
            DefaultLeadMinutes = -3,
            PollMinutes = 0,
            LookaheadHours = 10_000,
            OwnerEmails = [" neb@work.example ", " "]
        });

        Assert.Equal(new AlertSettings(2, 30, 180, "", 0, 1, 336, false, "", settings.OwnerEmails), settings);
        Assert.Equal(["neb@work.example"], settings.OwnerEmails);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Below_priority_2_pushover_is_asked_for_no_retry_and_no_expiry(int priority)
    {
        var settings = AlertSettings.Defaults(new AlertsOptions()) with { Priority = priority, Sound = "bike" };

        Assert.Equal(new PushoverDelivery(priority, null, null, "bike"), settings.Delivery);
    }

    [Theory]
    // 50 sounds a minute apart end at 50 minutes, before the 180 minute limit.
    [InlineData(60, 180, 3000)]
    // 30 minutes ends it before 50 sounds two minutes apart would.
    [InlineData(120, 30, 1800)]
    // 50 sounds at Pushover's floor: 25 minutes.
    [InlineData(30, 180, 1500)]
    public void The_message_stops_at_the_limit_or_the_fiftieth_sound_whichever_is_first(
        int repeat, int stopAfter, int expected)
    {
        var settings = AlertSettings.Defaults(new AlertsOptions()) with
        {
            RepeatSeconds = repeat,
            StopAfterMinutes = stopAfter
        };

        Assert.Equal(expected, settings.EffectiveStopSeconds);
    }

    [Fact]
    public void A_blank_zone_is_utc_and_a_named_one_is_that_zone()
    {
        var defaults = AlertSettings.Defaults(new AlertsOptions());

        Assert.Equal(NodaTime.DateTimeZone.Utc, defaults.FallbackZone());
        Assert.Equal("Asia/Amman", (defaults with { TimeZone = "Asia/Amman" }).FallbackZone().Id);
    }

    [Fact]
    public void Only_what_the_calendar_read_depends_on_changes_the_plan_key()
    {
        var settings = AlertSettings.Defaults(new AlertsOptions());

        Assert.Equal(settings.PlanKey, (settings with { Priority = 0, RepeatSeconds = 300, Sound = "bike", PollMinutes = 1 }).PlanKey);
        Assert.NotEqual(settings.PlanKey, (settings with { DefaultLeadMinutes = 11 }).PlanKey);
        Assert.NotEqual(settings.PlanKey, (settings with { LookaheadHours = 49 }).PlanKey);
        Assert.NotEqual(settings.PlanKey, (settings with { IncludeAllDay = true }).PlanKey);
        Assert.NotEqual(settings.PlanKey, (settings with { TimeZone = "Asia/Amman" }).PlanKey);
        Assert.NotEqual(settings.PlanKey, (settings with { OwnerEmails = ["neb@work.example"] }).PlanKey);
    }

    [Theory]
    [InlineData(60, "minute")]
    [InlineData(120, "2 minutes")]
    [InlineData(30, "30 seconds")]
    [InlineData(90, "90 seconds")]
    public void The_gap_between_sounds_reads_as_a_person_would_say_it(int seconds, string expected)
    {
        Assert.Equal(expected, AlertText.Every(seconds));
    }
}
