using System.Text.RegularExpressions;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>A logged run with what a field test is read from.</summary>
public sealed record FieldTestRun(
    Guid ActivityId,
    LocalDate Date,
    string Name,
    double? DistanceMeters,
    double Seconds,
    int? AverageHr,
    bool? Indoor,
    IReadOnlyList<LoggedLap> Laps);

/// <summary>A field test the log turned out to contain.</summary>
/// <param name="Kind">aet or threshold.</param>
/// <param name="SecPerKm">The pace held, over the part of the run that is the measurement.</param>
/// <param name="AverageHr">The heart rate held, likewise.</param>
/// <param name="DriftPercent">Pace-to-heart-rate decoupling between the halves, when laps allowed it.</param>
/// <param name="Evidence">How it was recognised and what it showed, in one sentence.</param>
public sealed record FieldTest(
    string Kind,
    Guid ActivityId,
    LocalDate Date,
    double SecPerKm,
    int AverageHr,
    double? DriftPercent,
    bool Indoor,
    string Evidence);

/// <summary>What the latest tests say the profile's thresholds should be.</summary>
/// <param name="AetHr">A new aerobic-threshold heart rate, or null to leave it.</param>
/// <param name="LtHr">A new lactate-threshold heart rate, or null to leave it.</param>
/// <param name="LtSecPerKm">A new lactate-threshold pace, or null to leave it.</param>
/// <param name="Reason">Why, in a sentence.</param>
/// <param name="Basis">The citation id the rule comes from.</param>
public sealed record ThresholdSuggestion(int? AetHr, int? LtHr, double? LtSecPerKm, string Reason, string Basis);

/// <summary>
/// Finds the field tests inside an ordinary log, so the thresholds the models
/// run on are measured rather than typed once and forgotten.
/// </summary>
/// <remarks>
/// <b>Aerobic-threshold test.</b> A run of thirty minutes or more held at the
/// aerobic-threshold heart rate — Maffetone's MAF test, Uphill Athlete's
/// heart-rate-drift test. Recognised by its name, or by an average heart
/// rate within a few beats of the profile's AeT on a steady run. When laps
/// allow it, the pace-to-heart-rate decoupling between the two halves is the
/// verdict: 5% or less and the heart rate was at or below the aerobic
/// threshold; more and it was above it (Johnston; Friel).
///
/// <b>Lactate-threshold test.</b> A continuous hard effort of twenty to
/// forty-five minutes — Friel's 30-minute time trial. Recognised by its
/// name, or by a steady run held at threshold pace with the heart rate to
/// match. The lactate-threshold heart rate is the average of the final
/// twenty minutes, not of the whole, because the first ten are the climb.
///
/// A run whose name says what it was is believed; a run that merely looks
/// like a test is reported as one with the evidence attached, so the athlete
/// can disagree.
///
/// Citations: <see cref="Citations.UphillAthleteHrDrift"/>,
/// <see cref="Citations.FrielLthr"/>, <see cref="Citations.MaffetoneMaf"/>.
/// </remarks>
public static class FieldTests
{
    public const string AetKind = "aet";
    public const string ThresholdKind = "threshold";

    /// <summary>How close to the AeT heart rate an unnamed run has to sit to be read as a test.</summary>
    public const int AetHrTolerance = 5;

    /// <summary>The shortest run that can be an aerobic-threshold test.</summary>
    public const double MinimumAetSeconds = 30 * 60;

    /// <summary>Decoupling at or below this puts the heart rate at or below the aerobic threshold.</summary>
    public const double DriftLimit = 0.05;

    /// <summary>When drift says the heart rate was too high, this is how far to lower it.</summary>
    public const int DriftStepDown = 5;

    /// <summary>Bounds of a continuous lactate-threshold effort.</summary>
    public const double MinimumThresholdSeconds = 18 * 60;
    public const double MaximumThresholdSeconds = 45 * 60;

    /// <summary>The window of a threshold test whose heart rate is the measurement.</summary>
    public const double ThresholdTailSeconds = 20 * 60;

    /// <summary>Each half of a drift test has to be at least this long to mean anything.</summary>
    public const double MinimumHalfSeconds = 10 * 60;

    /// <summary>Tests older than this no longer speak for the profile.</summary>
    public const int SuggestionWindowDays = 120;

    private static readonly Regex AetName = new(
        @"\b(maf|aet|aerobic[- ]threshold|hr[- ]drift|heart[- ]rate[- ]drift)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ThresholdName = new(
        @"\b(lthr|lactate|threshold[- ]test|time[- ]trial|tt|30[- ]?min(ute)?[- ]test)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Every test in the runs given, oldest first.</summary>
    public static IReadOnlyList<FieldTest> Detect(IEnumerable<FieldTestRun> runs, HeartRateBands bands, double vdot)
    {
        return runs
            .Select(run => AsThresholdTest(run, bands, vdot) ?? AsAetTest(run, bands))
            .Where(test => test is not null)
            .Select(test => test!)
            .OrderBy(test => test.Date)
            .ToArray();
    }

    public static FieldTest? AsAetTest(FieldTestRun run, HeartRateBands bands)
    {
        if (run.DistanceMeters is not > 0 || run.Seconds < MinimumAetSeconds) return null;
        if (run.AverageHr is not { } hr || hr <= 0) return null;

        var named = AetName.IsMatch(run.Name);
        var session = Session(run);
        if (!named && (Math.Abs(hr - bands.AetHr) > AetHrTolerance || !SessionMix.IsSteady(session, bands)))
        {
            return null;
        }

        var drift = Drift(run.Laps);
        var pace = run.Seconds / (run.DistanceMeters.Value / 1000.0);
        var how = named ? "Named as an aerobic-threshold test" : $"Held within {AetHrTolerance} bpm of your AeT";
        var verdict = drift switch
        {
            null => "no laps, so drift could not be read",
            <= DriftLimit => $"pace-to-HR drift {drift:0.0%}, inside the 5% line: {hr} bpm is at or below your AeT",
            _ => $"pace-to-HR drift {drift:0.0%}, past the 5% line: {hr} bpm is above your AeT"
        };

        return new FieldTest(
            AetKind, run.ActivityId, run.Date, pace, hr, drift, run.Indoor == true,
            $"{how}; {Format.Pace(pace)}/km at {hr} bpm over {run.Seconds / 60:0} min; {verdict}.");
    }

    public static FieldTest? AsThresholdTest(FieldTestRun run, HeartRateBands bands, double vdot)
    {
        if (run.DistanceMeters is not > 0) return null;
        if (run.Seconds < MinimumThresholdSeconds || run.Seconds > MaximumThresholdSeconds) return null;
        if (run.AverageHr is not { } hr || hr <= 0) return null;

        var pace = run.Seconds / (run.DistanceMeters.Value / 1000.0);
        var named = ThresholdName.IsMatch(run.Name);
        if (!named)
        {
            var threshold = TrainingPaces.For(vdot).Single(b => b.Zone == "T");
            var atPace = run.Indoor != true && pace <= threshold.SlowSecPerKm;
            var atHeart = hr > bands.EasyCeiling;
            if (!(atPace && atHeart) || !SessionMix.IsSteady(Session(run), null)) return null;
        }

        // Friel: the last twenty minutes, when the laps let us take them.
        var tail = Tail(run.Laps, ThresholdTailSeconds);
        var ltHr = tail?.Hr ?? hr;
        var ltPace = tail?.SecPerKm ?? pace;
        var how = named ? "Named as a threshold test" : "A steady effort at threshold pace with the heart rate to match";
        var window = tail is null ? "whole run (no laps to take the last 20 min from)" : "final 20 min";

        return new FieldTest(
            ThresholdKind, run.ActivityId, run.Date, ltPace, ltHr, null, run.Indoor == true,
            $"{how}; {Format.Pace(ltPace)}/km at {ltHr} bpm over the {window}.");
    }

    /// <summary>
    /// What the most recent tests say the profile should read, or null when
    /// they agree with it already.
    /// </summary>
    public static ThresholdSuggestion? Suggest(
        IReadOnlyList<FieldTest> tests, HeartRateBands current, double? currentLtSecPerKm, LocalDate today)
    {
        var since = today.PlusDays(-SuggestionWindowDays);
        var aet = tests.LastOrDefault(t => t.Kind == AetKind && t.Date >= since);
        var lt = tests.LastOrDefault(t => t.Kind == ThresholdKind && t.Date >= since);

        int? aetHr = null;
        var reasons = new List<string>();
        var basis = Citations.UphillAthleteHrDrift.Id;

        if (aet is not null)
        {
            if (aet.DriftPercent is > DriftLimit)
            {
                var lowered = aet.AverageHr - DriftStepDown;
                if (lowered < current.AetHr)
                {
                    aetHr = lowered;
                    reasons.Add($"The {aet.Date:yyyy-MM-dd} test drifted {aet.DriftPercent:0.0%} at {aet.AverageHr} bpm, so that is above your AeT; lower it {DriftStepDown} bpm and retest.");
                }
            }
            else if (aet.AverageHr > current.AetHr + 2)
            {
                aetHr = aet.AverageHr;
                reasons.Add(aet.DriftPercent is { } drift
                    ? $"The {aet.Date:yyyy-MM-dd} test held {aet.AverageHr} bpm with {drift:0.0%} drift, so your AeT is at least that."
                    : $"The {aet.Date:yyyy-MM-dd} test was run at {aet.AverageHr} bpm; without laps its drift is unknown, so take it only if the effort stayed conversational.");
            }
        }

        int? ltHr = null;
        double? ltPace = null;
        if (lt is not null)
        {
            if (current.LtHr is null || Math.Abs(lt.AverageHr - current.LtHr.Value) > 2)
            {
                ltHr = lt.AverageHr;
            }

            if (currentLtSecPerKm is not { } pace || Math.Abs(lt.SecPerKm - pace) / pace > 0.02)
            {
                ltPace = lt.SecPerKm;
            }

            if (ltHr is not null || ltPace is not null)
            {
                basis = Citations.FrielLthr.Id;
                reasons.Add($"The {lt.Date:yyyy-MM-dd} threshold test held {Format.Pace(lt.SecPerKm)}/km at {lt.AverageHr} bpm over its final 20 minutes.");
            }
        }

        if (aetHr is null && ltHr is null && ltPace is null) return null;
        return new ThresholdSuggestion(aetHr, ltHr, ltPace, string.Join(" ", reasons), basis);
    }

    /// <summary>
    /// Friel's decoupling: how much the speed-per-heartbeat of the second half
    /// fell short of the first. Null when the laps cannot make two halves.
    /// </summary>
    public static double? Drift(IReadOnlyList<LoggedLap> laps)
    {
        var usable = laps.Where(l => l.Seconds > 0 && l.DistanceMeters is > 0 && l.AverageHr is > 0).ToArray();
        if (usable.Length < 2) return null;

        var total = usable.Sum(l => l.Seconds);
        var first = new List<LoggedLap>();
        var second = new List<LoggedLap>();
        var elapsed = 0.0;
        foreach (var lap in usable)
        {
            (elapsed < total / 2 ? first : second).Add(lap);
            elapsed += lap.Seconds;
        }

        if (first.Count == 0 || second.Count == 0) return null;
        if (first.Sum(l => l.Seconds) < MinimumHalfSeconds || second.Sum(l => l.Seconds) < MinimumHalfSeconds) return null;

        var early = SpeedPerBeat(first);
        var late = SpeedPerBeat(second);
        return (early - late) / early;
    }

    private static double SpeedPerBeat(List<LoggedLap> laps)
    {
        var seconds = laps.Sum(l => l.Seconds);
        var meters = laps.Sum(l => l.DistanceMeters!.Value);
        var hr = laps.Sum(l => l.AverageHr!.Value * l.Seconds) / seconds;
        return meters / seconds / hr;
    }

    private static (int Hr, double SecPerKm)? Tail(IReadOnlyList<LoggedLap> laps, double seconds)
    {
        var usable = laps.Where(l => l.Seconds > 0 && l.DistanceMeters is > 0 && l.AverageHr is > 0).ToArray();
        if (usable.Length < 2) return null;

        var taken = new List<LoggedLap>();
        var covered = 0.0;
        for (var i = usable.Length - 1; i >= 0 && covered < seconds; i--)
        {
            taken.Add(usable[i]);
            covered += usable[i].Seconds;
        }

        if (covered < seconds * 0.75 || taken.Count == usable.Length) return null;

        var meters = taken.Sum(l => l.DistanceMeters!.Value);
        var hr = taken.Sum(l => l.AverageHr!.Value * l.Seconds) / covered;
        return ((int)Math.Round(hr), covered / (meters / 1000.0));
    }

    private static LoggedSession Session(FieldTestRun run) =>
        new("run", run.DistanceMeters, run.Seconds, run.Laps, run.Indoor, run.AverageHr);
}
