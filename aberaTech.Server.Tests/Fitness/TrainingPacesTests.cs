using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class TrainingPacesTests
{
    [Fact]
    public void Produces_the_five_daniels_zones_in_order()
    {
        var paces = TrainingPaces.For(37);

        Assert.Equal(["E", "M", "T", "I", "R"], paces.Select(p => p.Zone).ToArray());

        // Each zone is faster than the one before it, and each band's fast end
        // is faster than its slow end.
        for (var i = 1; i < paces.Count; i++)
        {
            Assert.True(paces[i].FastSecPerKm < paces[i - 1].FastSecPerKm);
        }

        Assert.All(paces, p => Assert.True(p.FastSecPerKm < p.SlowSecPerKm));
    }

    [Fact]
    public void Vdot_50_threshold_lands_near_daniels_published_tables()
    {
        // Daniels' printed T pace for VDOT 50 is about 4:31/km; the band
        // computed from his published intensity range should bracket the
        // mid-4:20s. The tables carry empirical smoothing, so the assertion is
        // a window, not a second.
        var threshold = TrainingPaces.For(50).Single(p => p.Zone == "T");

        Assert.InRange(threshold.FastSecPerKm, 245, 275);
        Assert.InRange(threshold.SlowSecPerKm, 255, 285);
    }

    [Fact]
    public void Easy_pace_for_the_current_athlete_matches_the_observed_base()
    {
        // At VDOT 37 the easy band should contain ~6:30-7:30/km — the pace the
        // athlete's own August base runs actually happened at. The model and
        // the training data describing the same runner is the whole check.
        var easy = TrainingPaces.For(37).Single(p => p.Zone == "E");

        Assert.True(easy.FastSecPerKm < 420);
        Assert.True(easy.SlowSecPerKm > 440);
    }

    [Fact]
    public void A_fitter_athlete_gets_faster_paces_across_the_board()
    {
        var slower = TrainingPaces.For(37);
        var faster = TrainingPaces.For(51);

        for (var i = 0; i < slower.Count; i++)
        {
            Assert.True(faster[i].FastSecPerKm < slower[i].FastSecPerKm);
            Assert.True(faster[i].SlowSecPerKm < slower[i].SlowSecPerKm);
        }
    }
}
