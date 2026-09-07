using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Finding the field tests inside an ordinary log: the MAF / heart-rate-drift
/// run and the 30-minute time trial.
/// </summary>
public sealed class FieldTestsTests
{
    private static readonly HeartRateBands Bands = new(AetHr: 152, LtHr: 168);
    private static readonly LocalDate Day = new(2026, 9, 1);
    private const double Vdot = 37;

    /// <summary>Laps of equal length at the paces and heart rates given.</summary>
    private static LoggedLap[] Laps(double lapSeconds, params (double SecPerKm, int Hr)[] laps) =>
        laps.Select(l => new LoggedLap(lapSeconds / l.SecPerKm * 1000, lapSeconds, l.Hr)).ToArray();

    private static FieldTestRun Run(string name, double seconds, double secPerKm, int hr, LoggedLap[]? laps = null, bool? indoor = null) =>
        new(Guid.NewGuid(), Day, name, seconds / secPerKm * 1000, seconds, hr, indoor, laps ?? []);

    [Fact]
    public void A_steady_hour_near_the_aet_heart_rate_is_an_aet_test_and_its_drift_is_read_from_the_laps()
    {
        // Four 15-minute laps: the second half is a shade slower at a shade
        // higher heart rate — 3.5% decoupling, inside the line.
        var laps = Laps(900, (400, 150), (400, 151), (405, 153), (408, 154));
        var run = Run("Morning Run", 3600, 403, 152, laps);

        var test = FieldTests.AsAetTest(run, Bands);

        Assert.NotNull(test);
        Assert.Equal(FieldTests.AetKind, test!.Kind);
        Assert.Equal(152, test.AverageHr);
        Assert.NotNull(test.DriftPercent);
        Assert.InRange(test.DriftPercent!.Value, 0.02, 0.05);
        Assert.Contains("inside the 5% line", test.Evidence);
    }

    [Fact]
    public void A_named_maf_test_counts_whatever_heart_rate_it_was_run_at()
    {
        var run = Run("MAF test, treadmill", 2400, 420, 160, indoor: true);

        var test = FieldTests.AsAetTest(run, Bands);

        Assert.NotNull(test);
        Assert.True(test!.Indoor);
        Assert.Null(test.DriftPercent);
        Assert.Contains("no laps", test.Evidence);
    }

    [Fact]
    public void A_short_run_or_an_interval_workout_is_not_an_aet_test()
    {
        Assert.Null(FieldTests.AsAetTest(Run("Run", 1500, 400, 152), Bands));

        // Repeats with recoveries: paces all over the place at the AeT average.
        var repeats = Laps(300, (280, 170), (520, 135), (280, 172), (520, 138), (280, 173), (520, 140), (280, 174));
        Assert.Null(FieldTests.AsAetTest(Run("Run", 2100, 400, 152, repeats), Bands));
    }

    [Fact]
    public void A_thirty_minute_time_trial_reads_its_threshold_from_the_final_twenty_minutes()
    {
        // Six 5-minute laps: the climb in the first ten minutes, then steady.
        var laps = Laps(300, (300, 155), (296, 164), (295, 170), (295, 172), (294, 173), (293, 174));
        var run = Run("30 min TT", 1800, 295, 168, laps);

        var test = FieldTests.AsThresholdTest(run, Bands, Vdot);

        Assert.NotNull(test);
        Assert.Equal(FieldTests.ThresholdKind, test!.Kind);
        Assert.Equal(172, test.AverageHr);
        Assert.InRange(test.SecPerKm, 293, 296);
        Assert.Contains("final 20 min", test.Evidence);
    }

    [Fact]
    public void An_unnamed_steady_run_at_threshold_pace_with_the_heart_rate_to_match_is_a_threshold_test()
    {
        var threshold = TrainingPaces.For(Vdot).Single(b => b.Zone == "T");
        var pace = threshold.SlowSecPerKm - 2;
        var laps = Laps(300, (pace, 166), (pace, 168), (pace, 169), (pace, 170), (pace, 171));
        var run = Run("Tempo", 1500, pace, 169, laps);

        Assert.NotNull(FieldTests.AsThresholdTest(run, Bands, Vdot));

        // The same effort at an easy heart rate is a fast easy run, not a test.
        Assert.Null(FieldTests.AsThresholdTest(run with { AverageHr = 150 }, Bands, Vdot));

        // And indoors the pace is not trusted to say it was threshold.
        Assert.Null(FieldTests.AsThresholdTest(run with { Indoor = true }, Bands, Vdot));
    }

    [Fact]
    public void Detect_orders_by_date_and_prefers_the_threshold_reading_for_a_hard_run()
    {
        var aet = Run("MAF test", 3600, 420, 152) with { Date = Day.PlusDays(-10) };
        var tt = Run("LTHR test", 1800, 295, 172);

        var tests = FieldTests.Detect([tt, aet], Bands, Vdot);

        Assert.Equal([FieldTests.AetKind, FieldTests.ThresholdKind], tests.Select(t => t.Kind));
    }

    [Fact]
    public void Drift_is_friels_decoupling_between_the_halves()
    {
        // First half 400 s/km at 150; second half 420 s/km at 156.
        var laps = Laps(900, (400, 150), (400, 150), (420, 156), (420, 156));
        var drift = FieldTests.Drift(laps);

        var early = 1000.0 / 400 / 150;
        var late = 1000.0 / 420 / 156;
        Assert.Equal((early - late) / early, drift!.Value, precision: 6);

        // Two laps of five minutes cannot make two ten-minute halves.
        Assert.Null(FieldTests.Drift(Laps(300, (400, 150), (420, 156))));
    }

    [Fact]
    public void A_clean_test_above_the_profile_raises_the_aet_suggestion()
    {
        var test = new FieldTest(FieldTests.AetKind, Guid.NewGuid(), Day, 400, 158, 0.03, false, "");

        var suggestion = FieldTests.Suggest([test], Bands, null, Day.PlusDays(5));

        Assert.NotNull(suggestion);
        Assert.Equal(158, suggestion!.AetHr);
        Assert.Null(suggestion.LtHr);
        Assert.Equal(Citations.UphillAthleteHrDrift.Id, suggestion.Basis);
    }

    [Fact]
    public void A_drifting_test_lowers_the_aet_by_five_and_says_to_retest()
    {
        var test = new FieldTest(FieldTests.AetKind, Guid.NewGuid(), Day, 400, 152, 0.08, false, "");

        var suggestion = FieldTests.Suggest([test], Bands, null, Day);

        Assert.Equal(147, suggestion!.AetHr);
        Assert.Contains("retest", suggestion.Reason);
    }

    [Fact]
    public void A_threshold_test_sets_both_the_heart_rate_and_the_pace_and_a_stale_one_says_nothing()
    {
        var test = new FieldTest(FieldTests.ThresholdKind, Guid.NewGuid(), Day, 295, 172, null, false, "");

        var fresh = FieldTests.Suggest([test], Bands, 340, Day.PlusDays(30));
        Assert.Equal(172, fresh!.LtHr);
        Assert.Equal(295, fresh.LtSecPerKm);
        Assert.Equal(Citations.FrielLthr.Id, fresh.Basis);

        Assert.Null(FieldTests.Suggest([test], Bands, 340, Day.PlusDays(FieldTests.SuggestionWindowDays + 1)));
    }

    [Fact]
    public void Nothing_is_suggested_when_the_profile_already_agrees()
    {
        var aet = new FieldTest(FieldTests.AetKind, Guid.NewGuid(), Day, 400, 153, 0.03, false, "");
        var lt = new FieldTest(FieldTests.ThresholdKind, Guid.NewGuid(), Day, 340, 167, null, false, "");

        Assert.Null(FieldTests.Suggest([aet, lt], Bands, 340, Day));
    }
}
