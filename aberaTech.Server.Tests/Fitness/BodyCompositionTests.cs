using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class BodyCompositionTests
{
    [Fact]
    public void Lean_mass_is_what_is_left_after_the_fat()
    {
        Assert.Equal(72.0, BodyComposition.LeanMassKg(80, 10), precision: 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyComposition.LeanMassKg(80, 100));
    }

    [Fact]
    public void Body_fat_rates_run_between_the_published_quartiles()
    {
        Assert.Equal(0.516, BodyComposition.SelectionRateByBodyFat(14), precision: 3);
        Assert.Equal(0.138, BodyComposition.SelectionRateByBodyFat(25), precision: 3);
        Assert.Equal(0.327, BodyComposition.SelectionRateByBodyFat(19.5), precision: 3);
        // Outside the cohort the study has nothing to say, so the ends hold.
        Assert.Equal(0.516, BodyComposition.SelectionRateByBodyFat(8), precision: 3);
        Assert.Equal(0.138, BodyComposition.SelectionRateByBodyFat(35), precision: 3);
    }

    [Fact]
    public void Lean_mass_rates_rise_with_muscle()
    {
        Assert.Equal(0.200, BodyComposition.SelectionRateByLeanMass(54), precision: 3);
        Assert.Equal(0.586, BodyComposition.SelectionRateByLeanMass(73), precision: 3);
        Assert.True(BodyComposition.SelectionRateByLeanMass(65) > BodyComposition.SelectionRateByLeanMass(60));
    }
}
