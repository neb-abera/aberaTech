using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// The athlete and their whole log, read once per request.
/// </summary>
/// <remarks>
/// Every report is some view of the same activities: the steady runs, the
/// last eight weeks, the rucks, and the field tests. Each view used to be its
/// own query, most of them fetching the same rows and the same laps again. One
/// summary was eighteen round trips. The log is a few thousand rows at most,
/// so it is read once, oldest first, and the views are filters over the list.
///
/// Untracked, because nothing here is ever saved: the change tracker's
/// snapshot of every activity and lap was pure overhead.
/// </remarks>
internal sealed record TrainingHistory(
    AthleteSettings Row,
    IReadOnlyList<Activity> Activities,
    AnchorChoice? Anchor = null,
    IReadOnlyList<Effort>? Efforts = null,
    IReadOnlyList<SkippedTest>? Skipped = null,
    IReadOnlyList<TrainingPlace>? Places = null)
{
    public static async Task<TrainingHistory> LoadAsync(FitnessDbContext database, CancellationToken cancellationToken)
    {
        var row = await database.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
                  ?? new AthleteSettings { Id = 1 };

        var activities = await database.Activities
            .AsNoTracking()
            .Include(a => a.Laps)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);

        var races = await database.RaceResults.AsNoTracking().ToListAsync(cancellationToken);
        var afts = await database.AftResults.AsNoTracking().ToListAsync(cancellationToken);
        var places = await database.TrainingPlaces.AsNoTracking().ToListAsync(cancellationToken);

        return Resolve(row, activities, races, afts, places, SystemClock.Instance.GetCurrentInstant());
    }

    /// <summary>
    /// The anchor and the altitudes, worked out from the tests and the place
    /// log every time the log is read, so nothing derived is ever stored to go
    /// stale. The row is untracked, so what is set on it here is never saved.
    /// </summary>
    internal static TrainingHistory Resolve(
        AthleteSettings row,
        IReadOnlyList<Activity> activities,
        IReadOnlyList<RaceResult> races,
        IReadOnlyList<AftResult> afts,
        IReadOnlyList<TrainingPlace> places,
        Instant now)
    {
        var periods = places.Select(p => new PlacePeriod(p.Since, p.AltitudeMeters, p.Place)).ToArray();
        var fallback = row.HomeAltitudeMeters;
        double AltitudeOn(LocalDate date) => Domain.Places.AltitudeOn(periods, date, fallback);

        var efforts = new List<Effort>();
        var skipped = new List<SkippedTest>();

        foreach (var activity in activities)
        {
            if (activity.Sport != "run" || Domain.Efforts.NamedDistance(activity.Name) is not { } named) continue;

            var date = activity.StartedAt.InUtc().Date;
            double? best = named.Meters switch
            {
                var m when Math.Abs(m - 1.5 * Domain.Efforts.Mile) < 1 => activity.Best1_5MileSeconds,
                var m when Math.Abs(m - 2 * Domain.Efforts.Mile) < 1 => activity.Best2MileSeconds,
                var m when Math.Abs(m - 5 * Domain.Efforts.Mile) < 1 => activity.Best5MileSeconds,
                var m when Math.Abs(m - 10 * Domain.Efforts.Mile) < 1 => activity.Best10MileSeconds,
                _ => null,
            };
            var (seconds, reason) = Domain.Efforts.TimeFor(named.Meters, activity.DistanceMeters, activity.DurationSeconds, best);
            if (seconds is not { } time)
            {
                skipped.Add(new SkippedTest(activity.Id, date, activity.Name, reason ?? "no time could be read"));
                continue;
            }

            var grade = TreadmillGrade.For(activity.Indoor == true, date, activity.Name, row.TreadmillGradePercent, row.TreadmillGradeSince);
            var flat = grade > 0 ? TreadmillGrade.FlatEquivalentMeters(named.Meters, grade) : named.Meters;
            var note = reason;
            if (grade > 0) note = Join(note, FormattableString.Invariant($"a treadmill at {grade:0.#}%, read as {flat / 1000:0.00} km on the flat"));

            efforts.Add(new Effort(EffortKind.Recording, date, activity.Name, named.Meters, time, AltitudeOn(date), flat, activity.Id, note));
        }

        efforts.AddRange(races.Select(r => new Effort(
            EffortKind.Entered, r.Date, r.Name, r.DistanceMeters, r.Seconds,
            r.AltitudeMeters ?? AltitudeOn(r.Date), r.DistanceMeters, r.Id)));

        efforts.AddRange(afts.Where(a => a.TwoMileSeconds > 0).Select(a => new Effort(
            EffortKind.Aft2Mile, a.Date, "AFT 2-mile", 2 * Domain.Efforts.Mile, a.TwoMileSeconds,
            AltitudeOn(a.Date), 2 * Domain.Efforts.Mile, a.Id,
            "run after four other events, so it sets a floor")));

        // A VDOT typed into the profile counts as a fresh value from its date.
        if (row.VdotMeasuredOn is { } typedOn)
        {
            efforts.Add(new Effort(EffortKind.TypedVdot, typedOn, "VDOT typed into the profile", 0, 0, 0, 0, TypedVdot: row.StartVdot));
        }

        var anchor = Domain.Efforts.Choose(efforts, row.StartVdot);
        row.StartVdot = anchor.Vdot;
        row.VdotMeasuredOn = anchor.On;
        row.AnchorDistanceMeters = anchor.Effort is { Kind: not EffortKind.TypedVdot } e ? e.DistanceMeters : null;
        row.AnchorSeconds = anchor.Effort is { Kind: not EffortKind.TypedVdot } f ? f.Seconds : null;
        row.AnchorSource = Domain.Efforts.Describe(anchor);

        // The times shown are for where the athlete is now; the lifetime best
        // is scored where it was run, from the middle of its year.
        if (periods.Length > 0)
        {
            row.HomeAltitudeMeters = AltitudeOn(now.InUtc().Date);
            if (row.PastPeakYear is { } year)
            {
                row.PastPeakAltitudeMeters = AltitudeOn(new LocalDate(year, 7, 1));
            }
        }

        return new TrainingHistory(
            row,
            activities,
            anchor,
            efforts.Where(x => x.Kind != EffortKind.TypedVdot).OrderByDescending(x => x.Date).ToArray(),
            skipped,
            places.OrderByDescending(p => p.Since ?? LocalDate.MinIsoValue).ToArray());
    }

    private static string Join(string? first, string second) => first is null ? second : $"{first}; {second}";
}

/// <summary>A recording named as a test whose time could not be read, and why.</summary>
internal sealed record SkippedTest(Guid ActivityId, LocalDate Date, string Name, string Reason);

/// <summary>A strength set, with the start of the session it belongs to.</summary>
internal sealed record DatedSet(StrengthSet Set, Instant StartedAt);

/// <summary>What the selection gates are scored from, beyond the activities themselves.</summary>
internal sealed record ReadinessEvidence(
    IReadOnlyList<DatedSet> Sets,
    IReadOnlyList<BodyMetric> BodyMetrics,
    IReadOnlyList<AftResult> AftResults,
    IReadOnlyList<FbiResult>? FbiResults = null)
{
    /// <summary>The most recent weigh-in; the weigh-ins are held oldest first.</summary>
    public BodyMetric? LatestWeight => BodyMetrics.Count > 0 ? BodyMetrics[^1] : null;

    public static async Task<ReadinessEvidence> LoadAsync(FitnessDbContext database, CancellationToken cancellationToken)
    {
        var sets = await database.StrengthSets
            .AsNoTracking()
            .Join(database.Activities, s => s.ActivityId, a => a.Id, (s, a) => new DatedSet(s, a.StartedAt))
            .ToListAsync(cancellationToken);

        var bodyMetrics = await database.BodyMetrics.AsNoTracking()
            .OrderBy(m => m.Date)
            .ToListAsync(cancellationToken);

        var aftResults = await database.AftResults.AsNoTracking()
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        var fbiResults = await database.FbiResults.AsNoTracking()
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return new ReadinessEvidence(sets, bodyMetrics, aftResults, fbiResults);
    }
}
