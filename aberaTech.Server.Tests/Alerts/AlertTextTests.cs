using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// What the phone shows, and when the two mute buttons end, in whatever
/// zone the calendar says it keeps.
/// </summary>
public sealed class AlertTextTests
{
    private static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];

    [Fact]
    public void The_message_is_the_start_in_new_york_time_and_the_location()
    {
        var alert = new PlannedAlert(
            "k", "Standup", "Room 1", Instant.FromUtc(2026, 10, 28, 13, 0), Instant.FromUtc(2026, 10, 28, 12, 50), AlertSource.DefaultLead);

        Assert.Equal("Starts 9:00 AM EDT, Wed 28 Oct\nRoom 1", AlertText.Message(alert, NewYork));
    }

    [Fact]
    public void Without_a_location_the_message_is_the_start_alone_and_winter_time_says_EST()
    {
        var alert = new PlannedAlert(
            "k", "Standup", null, Instant.FromUtc(2026, 11, 2, 14, 0), Instant.FromUtc(2026, 11, 2, 13, 50), AlertSource.DefaultLead);

        Assert.Equal("Starts 9:00 AM EST, Mon 2 Nov", AlertText.Message(alert, NewYork));
    }

    [Fact]
    public void Title_and_message_are_cut_to_what_pushover_accepts()
    {
        var alert = new PlannedAlert(
            "k", new string('t', 400), new string('l', 2000), Instant.FromUtc(2026, 11, 2, 14, 0), Instant.FromUtc(2026, 11, 2, 13, 50), AlertSource.DefaultLead);

        Assert.Equal(PushoverClient.MaxTitle, AlertText.Title(alert.Title).Length);
        Assert.Equal(PushoverClient.MaxMessage, AlertText.Message(alert, NewYork).Length);
    }

    [Fact]
    public void Mute_for_an_hour_is_sixty_minutes_from_now()
    {
        var now = Instant.FromUtc(2026, 10, 28, 1, 30);

        Assert.Equal(now + Duration.FromHours(1), AlertMute.ForAnHour(now));
    }

    [Fact]
    public void Mute_until_tomorrow_on_an_amman_calendar_ends_at_six_in_amman()
    {
        // 23:00 in Amman on Wednesday 28 October is 16:00 in New York.
        var until = AlertMute.UntilMorning(Instant.FromUtc(2026, 10, 28, 20, 0), DateTimeZoneProviders.Tzdb["Asia/Amman"]);

        Assert.Equal(Instant.FromUtc(2026, 10, 29, 3, 0), until);
    }

    [Theory]
    // 21:00 on Tuesday evening: 06:00 on Wednesday.
    [InlineData(2026, 10, 28, 1, 0, 2026, 10, 28, 10, 0)]
    // 01:00 on Wednesday, after midnight: still 06:00 that Wednesday, the
    // morning the owner means by "tomorrow" at that hour.
    [InlineData(2026, 10, 28, 5, 0, 2026, 10, 28, 10, 0)]
    // 06:00 exactly: the next day's 06:00, never now.
    [InlineData(2026, 10, 28, 10, 0, 2026, 10, 29, 10, 0)]
    // Saturday evening before the clocks go back: 06:00 on Sunday is EST.
    [InlineData(2026, 11, 1, 1, 0, 2026, 11, 1, 11, 0)]
    // Saturday evening before they go forward: 06:00 on Sunday is EDT.
    [InlineData(2026, 3, 8, 2, 0, 2026, 3, 8, 10, 0)]
    public void Mute_until_tomorrow_ends_at_the_next_six_in_the_morning_new_york_time(
        int y, int mo, int d, int h, int mi, int ey, int emo, int ed, int eh, int emi)
    {
        var until = AlertMute.UntilMorning(Instant.FromUtc(y, mo, d, h, mi), NewYork);

        Assert.Equal(Instant.FromUtc(ey, emo, ed, eh, emi), until);
    }
}
