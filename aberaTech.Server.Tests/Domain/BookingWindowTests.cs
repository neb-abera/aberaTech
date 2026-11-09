using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

public class BookingWindowTests
{
    private static readonly DateTimeZone Eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
    private static readonly DateTimeZone Mountain = DateTimeZoneProviders.Tzdb["America/Denver"];

    private static BookingPolicy Policy(LocalDate? earliest, int horizon = 21, int lead = 30) =>
        new(earliest, horizon, lead, 30, false);

    [Fact]
    public void With_no_earliest_date_the_window_opens_today_after_the_lead_time()
    {
        var now = Instant.FromUtc(2026, 11, 9, 15, 0);

        var window = BookingWindow.For(Policy(null), now, Eastern);

        Assert.Equal(new LocalDate(2026, 11, 9), window.From);
        Assert.Equal(new LocalDate(2026, 11, 30), window.To);
        Assert.Equal(now + Duration.FromMinutes(30), window.NotBefore);
    }

    [Fact]
    public void A_future_earliest_date_opens_at_midnight_on_that_date_in_the_hosts_zone()
    {
        var now = Instant.FromUtc(2026, 11, 9, 15, 0);

        var window = BookingWindow.For(Policy(new LocalDate(2026, 11, 16)), now, Eastern);

        Assert.Equal(new LocalDate(2026, 11, 16), window.From);
        // Midnight Eastern on 16 Nov is 05:00 UTC, so 06:00 in Berlin and
        // 22:00 on the 15th in Denver: one instant, whoever is looking.
        Assert.Equal(Instant.FromUtc(2026, 11, 16, 5, 0), window.NotBefore);
    }

    [Fact]
    public void The_hosts_zone_decides_the_instant()
    {
        var now = Instant.FromUtc(2026, 11, 9, 15, 0);

        var window = BookingWindow.For(Policy(new LocalDate(2026, 11, 16)), now, Mountain);

        Assert.Equal(Instant.FromUtc(2026, 11, 16, 7, 0), window.NotBefore);
    }

    [Fact]
    public void A_past_earliest_date_changes_nothing()
    {
        var now = Instant.FromUtc(2026, 11, 20, 15, 0);

        var window = BookingWindow.For(Policy(new LocalDate(2026, 11, 16)), now, Eastern);

        Assert.Equal(new LocalDate(2026, 11, 20), window.From);
        Assert.Equal(now + Duration.FromMinutes(30), window.NotBefore);
    }

    [Fact]
    public void A_lead_time_that_reaches_past_the_earliest_date_wins()
    {
        var now = Instant.FromUtc(2026, 11, 15, 12, 0);

        var window = BookingWindow.For(Policy(new LocalDate(2026, 11, 16), lead: 48 * 60), now, Eastern);

        Assert.Equal(Instant.FromUtc(2026, 11, 17, 12, 0), window.NotBefore);
    }

    [Fact]
    public void An_earliest_date_past_the_horizon_leaves_nothing_to_book()
    {
        var now = Instant.FromUtc(2026, 11, 9, 15, 0);

        var window = BookingWindow.For(Policy(new LocalDate(2026, 12, 31), horizon: 7), now, Eastern);

        Assert.True(window.IsEmpty);
    }

    [Fact]
    public void Today_is_the_hosts_today()
    {
        // 03:00 UTC on 10 Nov is still the evening of 9 Nov in New York.
        var now = Instant.FromUtc(2026, 11, 10, 3, 0);

        var window = BookingWindow.For(Policy(null), now, Eastern);

        Assert.Equal(new LocalDate(2026, 11, 9), window.From);
    }

    [Theory]
    // The day the clocks go back in the US, and the day they go forward.
    [InlineData(2026, 11, 1, 4)]
    [InlineData(2027, 3, 14, 5)]
    public void Midnight_on_a_transition_day_is_midnight_on_the_wall_clock(int year, int month, int day, int utcHour)
    {
        var now = new LocalDate(year, month, day).PlusDays(-7).AtMidnight().InUtc().ToInstant();

        var window = BookingWindow.For(Policy(new LocalDate(year, month, day)), now, Eastern);

        Assert.Equal(Instant.FromUtc(year, month, day, utcHour, 0), window.NotBefore);
    }
}

public class BookingLinkKeysTests
{
    [Fact]
    public void A_key_is_22_url_safe_characters_and_never_repeats()
    {
        var keys = Enumerable.Range(0, 1000).Select(_ => BookingLinkKeys.NewKey()).ToList();

        Assert.All(keys, key => Assert.Matches("^[A-Za-z0-9_-]{22}$", key));
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void The_stored_form_is_the_sha256_and_never_the_key()
    {
        var key = BookingLinkKeys.NewKey();

        var hash = BookingLinkKeys.Hash(key);

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.DoesNotContain(key, hash);
        Assert.Equal(hash, BookingLinkKeys.HashOrNull(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("abc def")]
    [InlineData("k=abc")]
    public void Input_that_cannot_be_a_key_is_not_looked_up(string? input)
    {
        Assert.Null(BookingLinkKeys.HashOrNull(input));
    }

    [Fact]
    public void An_oversized_key_is_refused_before_it_is_hashed()
    {
        Assert.Null(BookingLinkKeys.HashOrNull(new string('a', BookingLinkKeys.MaxLength + 1)));
        Assert.NotNull(BookingLinkKeys.HashOrNull(new string('a', BookingLinkKeys.MaxLength)));
    }
}
