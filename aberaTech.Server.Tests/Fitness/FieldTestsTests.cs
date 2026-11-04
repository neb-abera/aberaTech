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
    public void A_named_outdoor_drift_test_reads_its_drift_from_the_laps()
    {
        // Four 15-minute laps: the second half is a shade slower at a shade
        // higher heart rate — 3.5% decoupling, inside the line.
        var laps = Laps(900, (400, 150), (400, 151), (405, 153), (408, 154));
        var run = Run("HR drift test", 3600, 403, 152, laps);

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
    public void A_training_run_is_never_an_aet_test_however_close_to_the_aet_it_sat()
    {
        // The same steady hour at the AeT heart rate, named as a training run.
        // Until 2026-11-04 this was read as a test and moved the thresholds.
        var laps = Laps(900, (400, 150), (400, 151), (405, 153), (408, 154));
        Assert.Null(FieldTests.AsAetTest(Run("Aerobic base run Zone 2", 3600, 403, 152, laps), Bands));
        Assert.Null(FieldTests.AsAetTest(Run("Morning Run", 3600, 403, 152, laps), Bands));

        // A named test shorter than the protocol's 30 minutes is not one either.
        Assert.Null(FieldTests.AsAetTest(Run("HR drift test", 1500, 400, 152), Bands));
    }

    [Fact]
    public void An_indoor_drift_test_reads_the_heart_rate_rise_at_a_fixed_belt_speed()
    {
        // Treadmill at one speed for an hour: 140 bpm the first half, 146 the
        // second. The watch's pace is ignored. 6 / 140 = 4.3% drift.
        var laps = Laps(900, (400, 139), (395, 141), (410, 145), (390, 147));
        var run = Run("Aerobic threshold test, treadmill", 3600, 400, 143, laps, indoor: true);

        var test = FieldTests.AsAetTest(run, Bands);

        Assert.NotNull(test);
        Assert.Equal(6.0 / 140, test!.DriftPercent!.Value, precision: 6);
        Assert.Contains("fixed belt speed", test.Evidence);
        Assert.Contains("inside the 5% line", test.Evidence);
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
    public void A_tempo_run_is_not_a_threshold_test_and_a_named_five_mile_time_trial_is()
    {
        var threshold = TrainingPaces.For(Vdot).Single(b => b.Zone == "T");
        var pace = threshold.SlowSecPerKm - 2;
        var laps = Laps(300, (pace, 166), (pace, 168), (pace, 169), (pace, 170), (pace, 171));

        Assert.Null(FieldTests.AsThresholdTest(Run("Tempo", 1500, pace, 169, laps), Bands, Vdot));

        // Evoke's Block 3 test: a 5-mile time trial, about 44 minutes here.
        var fiveMile = Laps(330, (330, 160), (330, 168), (330, 171), (330, 172), (330, 173), (330, 173), (330, 174), (330, 174));
        var test = FieldTests.AsThresholdTest(Run("5 mile time trial", 2640, 330, 171, fiveMile), Bands, Vdot);
        Assert.NotNull(test);
        Assert.Contains("final 20 min", test!.Evidence);
    }

    [Fact]
    public void Detect_orders_by_date_and_prefers_the_threshold_reading_for_a_hard_run()
    {
        var aet = Run("MAF test", 3600, 420, 152) with { Date = Day.PlusDays(-10) };
        var training = Run("Aerobic base run", 3600, 420, 152) with { Date = Day.PlusDays(-5) };
        var tt = Run("LTHR test", 1800, 295, 172);

        var tests = FieldTests.Detect([tt, training, aet], Bands, Vdot);

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
