using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// The athlete and their whole log, read once per request.
/// </summary>
/// <remarks>
/// Every report is some view of the same activities — the steady runs, the
/// last eight weeks, the rucks, the field tests — and each view used to be its
/// own query, most of them fetching the same rows and the same laps again. One
/// summary was eighteen round trips. The log is a few thousand rows at most,
/// so it is read once, oldest first, and the views are filters over the list.
///
/// Untracked, because nothing here is ever saved: the change tracker's
/// snapshot of every activity and lap was pure overhead.
/// </remarks>
internal sealed record TrainingHistory(AthleteSettings Row, IReadOnlyList<Activity> Activities)
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

        return new TrainingHistory(row, activities);
    }
}

/// <summary>A strength set, with the start of the session it belongs to.</summary>
internal sealed record DatedSet(StrengthSet Set, Instant StartedAt);

/// <summary>What the selection gates are scored from, beyond the activities themselves.</summary>
internal sealed record ReadinessEvidence(
    IReadOnlyList<DatedSet> Sets,
    IReadOnlyList<BodyMetric> BodyMetrics,
    IReadOnlyList<AftResult> AftResults)
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

        return new ReadinessEvidence(sets, bodyMetrics, aftResults);
    }
}
