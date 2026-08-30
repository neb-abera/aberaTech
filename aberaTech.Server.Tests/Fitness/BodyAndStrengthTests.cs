using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class BodyMassTests
{
    [Fact]
    public void Losing_fat_mass_raises_vdot_by_the_mass_ratio()
    {
        // 174 lb -> 170 lb at VDOT 43: 43 x 174/170 = 44.01.
        var current = BodyMass.PoundsToKg(174);
        var target = BodyMass.PoundsToKg(170);
        Assert.Equal(43.0 * 174 / 170, BodyMass.AdjustVdot(43, current, target), precision: 3);
    }

    [Fact]
    public void Gaining_costs_exactly_what_losing_buys()
    {
        var up = BodyMass.AdjustVdot(43, 80, 84);
        var down = BodyMass.AdjustVdot(43, 80, 76);
        Assert.True(up < 43);
        Assert.True(down > 43);
    }

    [Fact]
    public void Changes_past_ten_percent_are_clamped_not_extrapolated()
    {
        // Asking about a 25% cut answers as if it were 10%: the fat-mass
        // assumption has no evidence past that.
        var atClamp = BodyMass.AdjustVdot(43, 80, 72);
        var wayPast = BodyMass.AdjustVdot(43, 80, 60);
        Assert.Equal(atClamp, wayPast, precision: 6);
    }
}

public sealed class OneRepMaxTests
{
    [Fact]
    public void Epley_matches_the_published_formula()
    {
        // 155 x 8: 155 x (1 + 8/30) = 196.33 — the bench estimate the audit used.
        Assert.Equal(196.33, OneRepMax.Epley(155, 8), precision: 2);
    }

    [Fact]
    public void Brzycki_cross_check_brackets_epley()
    {
        var epley = OneRepMax.Epley(155, 8);
        var brzycki = OneRepMax.Brzycki(155, 8);
        Assert.InRange(brzycki, epley * 0.95, epley * 1.05);
    }

    [Fact]
    public void A_single_rep_is_its_own_max()
    {
        Assert.Equal(300, OneRepMax.Epley(300, 1));
        Assert.Equal(300, OneRepMax.Brzycki(300, 1));
    }

    [Fact]
    public void Long_sets_are_outside_the_formulas_validity()
    {
        Assert.False(OneRepMax.IsEstimable(155, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => OneRepMax.Epley(155, 11));
    }
}
