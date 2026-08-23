using aberaTech.Scheduling.Calendar;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Calendar;

/// <summary>
/// Parsing is the seam where a change at Google's end would break this quietly,
/// so it is asserted against response shapes rather than assumed.
/// </summary>
public class GoogleFreeBusyParseTests
{
    private const string CalendarId = "primary";

    [Fact]
    public void A_normal_response_yields_its_busy_periods()
    {
        const string json = """
            {
              "kind": "calendar#freeBusy",
              "timeMin": "2027-06-01T00:00:00.000Z",
              "timeMax": "2027-06-08T00:00:00.000Z",
              "calendars": {
                "primary": {
                  "busy": [
                    { "start": "2027-06-01T13:00:00Z", "end": "2027-06-01T14:00:00Z" },
                    { "start": "2027-06-02T09:30:00Z", "end": "2027-06-02T10:00:00Z" }
                  ]
                }
              }
            }
            """;

        var busy = GoogleCalendarBusySource.Parse(json, CalendarId);

        Assert.Equal(2, busy.Count);
        Assert.Equal(Instant.FromUtc(2027, 6, 1, 13, 0), busy[0].Start);
        Assert.Equal(Instant.FromUtc(2027, 6, 2, 10, 0), busy[1].End);
    }

    [Fact]
    public void Offsets_are_honoured_rather_than_read_as_utc()
    {
        // Google returns offsets for events in a zone. Reading "09:00-04:00" as
        // 09:00 UTC would free five hours the host is actually busy.
        const string json = """
            {"calendars":{"primary":{"busy":[
              {"start":"2027-06-01T09:00:00-04:00","end":"2027-06-01T10:00:00-04:00"}
            ]}}}
            """;

        var busy = GoogleCalendarBusySource.Parse(json, CalendarId);

        Assert.Equal(Instant.FromUtc(2027, 6, 1, 13, 0), Assert.Single(busy).Start);
    }

    [Fact]
    public void Adjacent_and_overlapping_periods_come_back_merged()
    {
        const string json = """
            {"calendars":{"primary":{"busy":[
              {"start":"2027-06-01T09:00:00Z","end":"2027-06-01T10:00:00Z"},
              {"start":"2027-06-01T10:00:00Z","end":"2027-06-01T11:00:00Z"},
              {"start":"2027-06-01T09:30:00Z","end":"2027-06-01T09:45:00Z"}
            ]}}}
            """;

        var busy = Assert.Single(GoogleCalendarBusySource.Parse(json, CalendarId));

        Assert.Equal(Instant.FromUtc(2027, 6, 1, 9, 0), busy.Start);
        Assert.Equal(Instant.FromUtc(2027, 6, 1, 11, 0), busy.End);
    }

    [Fact]
    public void A_free_calendar_yields_nothing()
    {
        Assert.Empty(GoogleCalendarBusySource.Parse("""{"calendars":{"primary":{"busy":[]}}}""", CalendarId));
    }

    [Fact]
    public void An_error_for_the_calendar_yields_nothing_rather_than_throwing()
    {
        // Google reports per-calendar errors in the body with a 200. Treating
        // that as an exception would take the booking page down over a calendar
        // the host renamed.
        const string json = """
            {"calendars":{"primary":{"errors":[{"domain":"global","reason":"notFound"}],"busy":[]}}}
            """;

        Assert.Empty(GoogleCalendarBusySource.Parse(json, CalendarId));
    }

    [Fact]
    public void A_response_for_a_different_calendar_yields_nothing()
    {
        const string json = """
            {"calendars":{"someone@else.test":{"busy":[
              {"start":"2027-06-01T09:00:00Z","end":"2027-06-01T10:00:00Z"}
            ]}}}
            """;

        Assert.Empty(GoogleCalendarBusySource.Parse(json, CalendarId));
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"calendars":{}}""")]
    [InlineData("""{"calendars":{"primary":{}}}""")]
    public void A_response_missing_the_parts_we_read_yields_nothing(string json)
    {
        Assert.Empty(GoogleCalendarBusySource.Parse(json, CalendarId));
    }

    [Fact]
    public void A_malformed_period_is_skipped_rather_than_poisoning_the_rest()
    {
        const string json = """
            {"calendars":{"primary":{"busy":[
              {"start":"not-a-time","end":"2027-06-01T10:00:00Z"},
              {"end":"2027-06-01T12:00:00Z"},
              {"start":"2027-06-01T14:00:00Z","end":"2027-06-01T15:00:00Z"}
            ]}}}
            """;

        var busy = Assert.Single(GoogleCalendarBusySource.Parse(json, CalendarId));

        Assert.Equal(Instant.FromUtc(2027, 6, 1, 14, 0), busy.Start);
    }
}
