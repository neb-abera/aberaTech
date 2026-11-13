using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class EffortsTests
{
    private const double Mile = Efforts.Mile;

    [Theory]
    [InlineData("5 mile time trial", 5 * Mile)]
    [InlineData("2 Mile TT", 2 * Mile)]
    [InlineData("Two-mile race", 2 * Mile)]
    [InlineData("Army Ten-Miler race", 10 * Mile)]
    [InlineData("Turkey Trot 10K race", 10_000)]
    [InlineData("5k time trial", 5_000)]
    [InlineData("Half marathon race", 21_097.5)]
    [InlineData("Marine Corps Marathon race", 42_195)]
    [InlineData("1.5 mile time trial", 1.5 * Mile)]
    public void A_test_is_read_from_its_name(string name, double meters)
    {
        Assert.Equal(meters, Efforts.NamedDistance(name)!.Value.Meters, 3);
    }

    [Theory]
    [InlineData("Run (MAF intensity)")]
    [InlineData("5 mile easy run")]
    [InlineData("5K race pace repeats")]
    [InlineData("30 min time trial")]
    [InlineData("HR drift test")]
    [InlineData(null)]
    public void A_name_that_is_not_a_distance_test_is_not_one(string? name)
    {
        Assert.Null(Efforts.NamedDistance(name));
    }

    [Fact]
    public void A_recording_within_three_percent_is_read_at_its_pace()
    {
        var (seconds, reason) = Efforts.TimeFor(5 * Mile, 5 * Mile * 1.02, 2040, null);
        Assert.Equal(2040 / 1.02, seconds!.Value, 6);
        Assert.Null(reason);
    }

    [Fact]
    public void A_longer_recording_uses_its_fastest_stretch_or_says_why_it_cannot()
    {
        Assert.Equal(2000, Efforts.TimeFor(5 * Mile, 11_000, 3300, 2000).Seconds);
        var (none, why) = Efforts.TimeFor(5 * Mile, 11_000, 3300, null);
        Assert.Null(none);
        Assert.Contains("warm-up", why);
    }

    private static readonly LocalDate Day = new(2026, 4, 3);

    private static Effort Fresh(int days, double seconds) =>
        new(EffortKind.Recording, Day.PlusDays(days), "5K race", 5000, seconds, 0, 5000);

    private static Effort Aft(int days, double seconds) =>
        new(EffortKind.Aft2Mile, Day.PlusDays(days), "AFT 2-mile", 2 * Mile, seconds, 0, 2 * Mile);

    [Fact]
    public void The_newest_fresh_test_is_the_anchor()
    {
        var older = Fresh(-30, 1200);
        var newer = Fresh(-5, 1260);
        var choice = Efforts.Choose([older, newer], 37);
        Assert.Same(newer, choice.Effort);
        Assert.False(choice.Floor);
    }

    [Fact]
    public void An_aft_two_mile_only_raises_the_anchor_never_lowers_it()
    {
        var race = Fresh(-30, 1200);
        var slowAft = Aft(-5, 1100);
        var fastAft = Aft(-5, 700);

        Assert.Same(race, Efforts.Choose([race, slowAft], 37).Effort);
        var raised = Efforts.Choose([race, fastAft], 37);
        Assert.Same(fastAft, raised.Effort);
        Assert.True(raised.Floor);
    }

    [Fact]
    public void An_older_aft_does_not_override_a_newer_race()
    {
        var race = Fresh(-5, 1200);
        Assert.Same(race, Efforts.Choose([race, Aft(-30, 700)], 37).Effort);
    }

    [Fact]
    public void With_only_an_aft_it_is_the_anchor_as_a_floor()
    {
        var choice = Efforts.Choose([Aft(0, 1009)], 37);
        Assert.True(choice.Floor);
        Assert.Contains("floor", choice.Why);
    }

    [Fact]
    public void With_nothing_the_default_stands()
    {
        var choice = Efforts.Choose([], 37);
        Assert.Null(choice.Effort);
        Assert.Equal(37, choice.Vdot);
    }

    [Fact]
    public void A_place_covers_its_days_and_an_open_one_covers_everything_before()
    {
        PlacePeriod[] places =
        [
            new(null, 1186, "El Paso"),
            new(new LocalDate(2022, 12, 1), 15, "Washington"),
            new(new LocalDate(2026, 2, 13), 1186, "El Paso"),
        ];

        Assert.Equal(1186, Places.AltitudeOn(places, new LocalDate(2018, 6, 1), 0));
        Assert.Equal(15, Places.AltitudeOn(places, new LocalDate(2025, 6, 1), 0));
        Assert.Equal(1186, Places.AltitudeOn(places, new LocalDate(2026, 4, 3), 0));
        Assert.Equal(42, Places.AltitudeOn([], new LocalDate(2026, 4, 3), 42));
    }
}
