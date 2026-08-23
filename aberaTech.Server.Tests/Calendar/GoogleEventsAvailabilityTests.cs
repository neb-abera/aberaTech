using aberaTech.Scheduling.Calendar;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Calendar;

/// <summary>
/// Reading open windows off a calendar. The failure modes here are quiet ones —
/// an all-day entry claiming the whole night, a deleted block coming back — so
/// each is asserted rather than assumed.
/// </summary>
public class GoogleEventsAvailabilityTests
{
    [Fact]
    public void A_timed_event_becomes_an_open_window()
    {
        const string json = """
            {"items":[
              {"status":"confirmed","summary":"Open for bookings",
               "start":{"dateTime":"2027-06-01T09:00:00-04:00"},
               "end":{"dateTime":"2027-06-01T17:00:00-04:00"}}
            ]}
            """;

        var window = Assert.Single(GoogleEventsAvailabilitySource.ParseWindows(json));

        Assert.Equal(Instant.FromUtc(2027, 6, 1, 13, 0), window.Start);
        Assert.Equal(Instant.FromUtc(2027, 6, 1, 21, 0), window.End);
    }

    [Fact]
    public void An_all_day_entry_is_ignored_rather_than_offering_the_whole_night()
    {
        // The dangerous one. An all-day "Open" carries a date and no time, and
        // treating it as midnight to midnight would offer every hour of the
        // host's night to anybody with the link.
        const string json = """
            {"items":[
              {"status":"confirmed","summary":"Open",
               "start":{"date":"2027-06-01"},"end":{"date":"2027-06-02"}}
            ]}
            """;

        Assert.Empty(GoogleEventsAvailabilitySource.ParseWindows(json));
    }

    [Fact]
    public void A_cancelled_occurrence_does_not_come_back()
    {
        // Google returns deleted occurrences of a recurring block with status
        // "cancelled" so callers can notice the deletion. Treating one as open
        // would resurrect a block the host removed.
        const string json = """
            {"items":[
              {"status":"cancelled",
               "start":{"dateTime":"2027-06-01T09:00:00Z"},
               "end":{"dateTime":"2027-06-01T17:00:00Z"}},
              {"status":"confirmed",
               "start":{"dateTime":"2027-06-02T09:00:00Z"},
               "end":{"dateTime":"2027-06-02T17:00:00Z"}}
            ]}
            """;

        var window = Assert.Single(GoogleEventsAvailabilitySource.ParseWindows(json));

        Assert.Equal(Instant.FromUtc(2027, 6, 2, 9, 0), window.Start);
    }

    [Fact]
    public void Overlapping_open_blocks_become_one_run()
    {
        // An "office hours" block inside a longer "available" one should not
        // produce two interleaved sets of slots with duplicate start times.
        const string json = """
            {"items":[
              {"status":"confirmed","start":{"dateTime":"2027-06-01T09:00:00Z"},"end":{"dateTime":"2027-06-01T17:00:00Z"}},
              {"status":"confirmed","start":{"dateTime":"2027-06-01T11:00:00Z"},"end":{"dateTime":"2027-06-01T12:00:00Z"}}
            ]}
            """;

        var window = Assert.Single(GoogleEventsAvailabilitySource.ParseWindows(json));

        Assert.Equal(Instant.FromUtc(2027, 6, 1, 9, 0), window.Start);
        Assert.Equal(Instant.FromUtc(2027, 6, 1, 17, 0), window.End);
    }

    [Fact]
    public void Adjacent_blocks_join_into_a_continuous_run()
    {
        const string json = """
            {"items":[
              {"status":"confirmed","start":{"dateTime":"2027-06-01T09:00:00Z"},"end":{"dateTime":"2027-06-01T12:00:00Z"}},
              {"status":"confirmed","start":{"dateTime":"2027-06-01T12:00:00Z"},"end":{"dateTime":"2027-06-01T15:00:00Z"}}
            ]}
            """;

        var window = Assert.Single(GoogleEventsAvailabilitySource.ParseWindows(json));

        Assert.Equal(Duration.FromHours(6), window.End - window.Start);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"items":[]}""")]
    [InlineData("""{"items":[{"status":"confirmed"}]}""")]
    [InlineData("""{"items":[{"status":"confirmed","start":{"dateTime":"nonsense"},"end":{"dateTime":"2027-06-01T17:00:00Z"}}]}""")]
    public void A_listing_with_nothing_usable_yields_nothing(string json)
    {
        Assert.Empty(GoogleEventsAvailabilitySource.ParseWindows(json));
    }

    [Fact]
    public void A_backwards_event_is_discarded()
    {
        const string json = """
            {"items":[{"status":"confirmed",
              "start":{"dateTime":"2027-06-01T17:00:00Z"},
              "end":{"dateTime":"2027-06-01T09:00:00Z"}}]}
            """;

        Assert.Empty(GoogleEventsAvailabilitySource.ParseWindows(json));
    }
}
