using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class CarriesTests
{
    private static readonly LocalDate Since = new(2026, 6, 1);

    [Theory]
    [InlineData("Farmers Walk", true)]
    [InlineData("Farmer's Carry (Dumbbell)", true)]
    [InlineData("Suitcase Carry", false)]
    [InlineData("Deadlift (Barbell)", false)]
    public void Farmers_carries_are_read_by_name(string exercise, bool expected)
    {
        Assert.Equal(expected, Carries.IsFarmersCarry(exercise));
    }

    [Fact]
    public void The_longest_carry_at_the_load_wins_and_a_light_one_does_not_count()
    {
        // 80 kg athlete: the standard is 120 kg total, so 60 kg in each hand.
        var carries = new[]
        {
            new LoggedCarry(new LocalDate(2026, 7, 1), "Farmers Walk", 60, 40),
            new LoggedCarry(new LocalDate(2026, 8, 1), "Farmers Walk", 62, 55),
            new LoggedCarry(new LocalDate(2026, 8, 15), "Farmers Walk", 50, 120), // too light
            new LoggedCarry(new LocalDate(2026, 3, 1), "Farmers Walk", 70, 200), // before the window
            new LoggedCarry(new LocalDate(2026, 8, 20), "Farmers Walk", 65, null) // no distance logged
        };

        var best = Carries.Longest(carries, bodyKg: 80, Carries.StandardBodyweightMultiple, Since);

        Assert.NotNull(best);
        Assert.Equal(55, best!.DistanceMeters);
        Assert.Equal(124, best.TotalLoadKg);
        Assert.Equal(new LocalDate(2026, 8, 1), best.Date);
    }

    [Fact]
    public void Nothing_at_the_load_means_no_measurement()
    {
        var carries = new[] { new LoggedCarry(new LocalDate(2026, 8, 1), "Farmers Walk", 30, 100) };
        Assert.Null(Carries.Longest(carries, 80, Carries.StandardBodyweightMultiple, Since));
    }
}
