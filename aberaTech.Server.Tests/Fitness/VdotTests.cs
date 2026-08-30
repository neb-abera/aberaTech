using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The Daniels-Gilbert equations against independently computed values —
/// including the athlete's own measured performances, so a regression here is
/// a regression in every prediction the site shows.
/// </summary>
public sealed class VdotTests
{
    [Theory]
    // 2 miles in 16:49 — the measured Apr 2026 outdoor time trial.
    [InlineData(2 * Vdot.MileMeters, 16 + 49 / 60.0, 35.5, 0.15)]
    // 5 miles in 39:00 — the spreadsheet's (stale) anchor.
    [InlineData(5 * Vdot.MileMeters, 39.0, 40.9, 0.15)]
    // 5K in 20:38 sits near VDOT 48 in Daniels' published tables.
    [InlineData(5000, 20 + 38 / 60.0, 48.0, 0.25)]
    public void Scores_known_performances(double meters, double minutes, double expected, double tolerance)
    {
        Assert.InRange(Vdot.FromRace(meters, minutes), expected - tolerance, expected + tolerance);
    }

    [Fact]
    public void Goal_two_mile_implies_vdot_51()
    {
        // The 12:15 two-mile goal converts to ~VDOT 51 — the number the whole
        // goal analysis hangs off.
        Assert.InRange(Vdot.FromRace(2 * Vdot.MileMeters, 12.25), 50.7, 51.3);
    }

    [Theory]
    [InlineData(2 * Vdot.MileMeters, 35.5)]
    [InlineData(5 * Vdot.MileMeters, 48.0)]
    [InlineData(5000, 43.0)]
    public void Inverse_roundtrips(double meters, double vdot)
    {
        var minutes = Vdot.MinutesFor(meters, vdot);
        Assert.InRange(Vdot.FromRace(meters, minutes), vdot - 0.01, vdot + 0.01);
    }

    [Fact]
    public void Faster_time_scores_higher()
    {
        Assert.True(Vdot.FromRace(5000, 20) > Vdot.FromRace(5000, 25));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(5000, 0)]
    public void Rejects_nonsense(double meters, double minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Vdot.FromRace(meters, minutes));
    }
}
