using System.Text.RegularExpressions;
using NodaTime;

namespace aberaTech.Fitness.Domain;

/// <summary>What kind of all-out effort a result is.</summary>
public enum EffortKind
{
    /// <summary>A race or time trial run fresh, from a named recording.</summary>
    Recording,

    /// <summary>A race or time trial entered by hand.</summary>
    Entered,

    /// <summary>
    /// The 2-mile of an AFT, run after four other events. It reads slower than
    /// a fresh 2-mile by an amount nobody has measured, so it only ever sets a
    /// floor: fitness is at least this (Neb, 2026-11-13).
    /// </summary>
    Aft2Mile,

    /// <summary>A VDOT typed into the profile before tests were read.</summary>
    TypedVdot,
}

/// <summary>One all-out effort over a known distance, scored where it was run.</summary>
/// <param name="AltitudeMeters">Where it was run, which sets its sea-level equivalent.</param>
/// <param name="FlatMeters">The distance as run on the flat: a treadmill's incline adds to it.</param>
public sealed record Effort(
    EffortKind Kind,
    LocalDate Date,
    string Name,
    double DistanceMeters,
    double Seconds,
    double AltitudeMeters,
    double FlatMeters,
    Guid? Id = null,
    string? Note = null,
    double? TypedVdot = null)
{
    /// <summary>The effort in VDOT, at its sea-level equivalent.</summary>
    public double Vdot => TypedVdot
        ?? Domain.Vdot.FromRace(FlatMeters, Altitude.ToSeaLevel(Seconds, AltitudeMeters) / 60.0);
}

/// <summary>The effort the projections start from, and why it was chosen.</summary>
public sealed record AnchorChoice(Effort? Effort, double Vdot, LocalDate? On, bool Floor, string Why);

/// <summary>
/// Reads named tests out of the log and picks the anchor. Any distance from
/// a mile to the marathon counts: the model scores each by Daniels' VDOT, so
/// a 5K and a 10-mile land on the same scale (Neb, 2026-11-13).
/// </summary>
public static partial class Efforts
{
    public const double Mile = Domain.Vdot.MileMeters;

    /// <summary>The distances a test may be named for, longest names first so "half marathon" wins over "marathon".</summary>
    public static readonly (string Label, double Meters, Regex Pattern)[] Distances =
    [
        ("half marathon", 21_097.5, new Regex(@"\bhalf[\s-]?marathon\b", RegexOptions.IgnoreCase)),
        ("marathon", 42_195, new Regex(@"\bmarathon\b", RegexOptions.IgnoreCase)),
        ("1.5 mile", 1.5 * Mile, new Regex(@"\b1\.5[\s-]?(miles?|miler|mi)\b", RegexOptions.IgnoreCase)),
        ("10 mile", 10 * Mile, new Regex(@"\b(10|ten)[\s-]?(miles?|miler|mi)\b", RegexOptions.IgnoreCase)),
        ("5 mile", 5 * Mile, new Regex(@"\b(5|five)[\s-]?(miles?|miler|mi)\b", RegexOptions.IgnoreCase)),
        ("3 mile", 3 * Mile, new Regex(@"\b(3|three)[\s-]?(miles?|miler|mi)\b", RegexOptions.IgnoreCase)),
        ("2 mile", 2 * Mile, new Regex(@"\b(2|two)[\s-]?(miles?|miler|mi)\b", RegexOptions.IgnoreCase)),
        ("1 mile", Mile, new Regex(@"\b(1|one)[\s-]?(miles?|miler|mi)\b", RegexOptions.IgnoreCase)),
        ("10K", 10_000, new Regex(@"\b10[\s-]?k(m)?\b", RegexOptions.IgnoreCase)),
        ("5K", 5_000, new Regex(@"\b5[\s-]?k(m)?\b", RegexOptions.IgnoreCase)),
    ];

    [GeneratedRegex(@"\b(time[\s-]?trial|tt|race)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TestWord();

    /// <summary>"5K race pace repeats" is a workout, not a race.</summary>
    [GeneratedRegex(@"\brace[\s-]?pace\b", RegexOptions.IgnoreCase)]
    private static partial Regex RacePace();

    /// <summary>A recording's share of slack around its named distance before it reads as something else.</summary>
    public const double DistanceTolerance = 0.03;

    /// <summary>
    /// The distance a recording is named for, when its name says it was a race
    /// or a time trial: "5 mile time trial", "10K race", "Half marathon TT".
    /// </summary>
    public static (string Label, double Meters)? NamedDistance(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !TestWord().IsMatch(name) || RacePace().IsMatch(name)) return null;
        foreach (var (label, meters, pattern) in Distances)
        {
            if (pattern.IsMatch(name)) return (label, meters);
        }

        return null;
    }

    /// <summary>
    /// The named distance's time out of a recording: the whole recording when
    /// it covers the distance, else the fastest stretch of that length when
    /// the import kept one. Null, with the reason, when neither holds.
    /// </summary>
    public static (double? Seconds, string? Reason) TimeFor(
        double namedMeters,
        double? recordedMeters,
        double durationSeconds,
        double? bestEffortSeconds)
    {
        if (recordedMeters is not { } recorded || recorded <= 0)
        {
            return (null, "the recording has no distance");
        }

        if (Math.Abs(recorded - namedMeters) / namedMeters <= DistanceTolerance)
        {
            // Within 3%: the same pace over the named distance.
            return (durationSeconds * namedMeters / recorded, null);
        }

        if (recorded > namedMeters && bestEffortSeconds is { } best)
        {
            return (best, Text($"the fastest stretch of that length inside a {recorded / 1000:0.0} km recording"));
        }

        return (null, Text($"the recording covers {recorded / 1000:0.00} km, not the {namedMeters / 1000:0.00} km its name says; save the warm-up as its own run"));
    }

    /// <summary>
    /// The anchor: the newest fresh effort, entered or recorded, or a typed
    /// VDOT newer than all of them. An AFT 2-mile on or after that date can
    /// only raise it, and only when it shows more fitness, because it is run
    /// tired. With nothing else, the newest AFT 2-mile is the anchor, as a
    /// floor.
    /// </summary>
    public static AnchorChoice Choose(IReadOnlyList<Effort> efforts, double defaultVdot)
    {
        var fresh = efforts
            .Where(e => e.Kind is EffortKind.Recording or EffortKind.Entered or EffortKind.TypedVdot)
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Kind == EffortKind.TypedVdot)
            .FirstOrDefault();
        var aft = efforts
            .Where(e => e.Kind == EffortKind.Aft2Mile)
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();

        if (fresh is null && aft is null)
        {
            return new AnchorChoice(null, defaultVdot, null, false, "no race, time trial, or AFT is recorded, so this is the default");
        }

        if (aft is not null && (fresh is null || (aft.Date >= fresh.Date && aft.Vdot >= fresh.Vdot)))
        {
            var why = fresh is null
                ? "no fresh race or time trial is recorded, so your newest AFT 2-mile sets a floor: run after four other events, it reads slower than a fresh 2-mile"
                : "your AFT 2-mile, run on or after your last fresh value, shows at least as much fitness, so it sets the floor";
            return new AnchorChoice(aft, aft.Vdot, aft.Date, true, why);
        }

        return new AnchorChoice(
            fresh,
            fresh!.Vdot,
            fresh.Date,
            false,
            fresh.Kind == EffortKind.TypedVdot ? "the VDOT typed into your profile, newer than any race or time trial" : "your newest race or time trial");
    }

    /// <summary>The anchor's test, in words a page can use: "your AFT 2-mile of 2026-04-03".</summary>
    public static string Describe(AnchorChoice anchor) => anchor.Effort switch
    {
        null => "the default VDOT",
        { Kind: EffortKind.TypedVdot } e => Text($"the VDOT typed into your profile on {e.Date:yyyy-MM-dd}"),
        { Kind: EffortKind.Aft2Mile } e => Text($"your AFT 2-mile of {e.Date:yyyy-MM-dd}, as a floor"),
        var e => Text($"your {e.Name} of {e.Date:yyyy-MM-dd}"),
    };

    private static string Text(FormattableString text) => FormattableString.Invariant(text);
}

/// <summary>Where the athlete lived and trained, by date, for the altitude of a day.</summary>
public sealed record PlacePeriod(LocalDate? Since, double AltitudeMeters, string Place);

public static class Places
{
    /// <summary>
    /// The altitude on a day: the latest place that began on or before it. A
    /// place with no start covers everything before the first dated one.
    /// </summary>
    public static double AltitudeOn(IReadOnlyList<PlacePeriod> places, LocalDate date, double fallbackMeters)
    {
        var dated = places.Where(p => p.Since is { } since && since <= date).OrderByDescending(p => p.Since).FirstOrDefault();
        if (dated is not null) return dated.AltitudeMeters;
        var open = places.FirstOrDefault(p => p.Since is null);
        if (open is not null) return open.AltitudeMeters;
        var earliest = places.Where(p => p.Since is not null).OrderBy(p => p.Since).FirstOrDefault();
        return earliest?.AltitudeMeters ?? fallbackMeters;
    }
}
