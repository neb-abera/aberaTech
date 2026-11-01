using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;

namespace aberaTech.Fitness.Sync;

/// <summary>
/// When each source is due, as a pure decision the worker asks every tick.
/// </summary>
/// <remarks>
/// Written out rather than configured because the two cadences have reasons.
/// Hevy is a lifting log that changes a few times a week and whose API walks
/// the whole history every call, so once a day is plenty. intervals.icu is the
/// watch's outlet and the readiness gates want yesterday's run today, and its
/// rate limit prices an hourly check at a few dozen requests a day of the
/// thousand allowed. A failed run retries after 15 minutes for its first hour,
/// then hourly: a wait of a full day would cost a lift two days, and retrying
/// a dead token every tick is how a rate limit gets spent on errors.
/// </remarks>
public static class SyncSchedule
{
    public const string HevySource = "hevy-api";
    public const string IntervalsIcuSource = "intervals-icu";

    public static readonly Duration HevyInterval = Duration.FromHours(24);
    public static readonly Duration IntervalsIcuInterval = Duration.FromHours(1);

    public static readonly Duration QuickRetry = Duration.FromMinutes(15);
    public static readonly Duration SlowRetry = Duration.FromHours(1);

    /// <summary>A missed run or two past each cadence, so one failure does not raise a finding.</summary>
    public static readonly Duration HevyStaleAfter = Duration.FromHours(26);
    public static readonly Duration IntervalsIcuStaleAfter = Duration.FromHours(3);

    /// <summary>Whether a source last tried at <paramref name="lastRunAt"/> and last finished at <paramref name="lastSuccessAt"/> should run now.</summary>
    public static bool IsDue(string source, Instant? lastRunAt, Instant? lastSuccessAt, Instant now)
    {
        var interval = source switch
        {
            HevySource => HevyInterval,
            IntervalsIcuSource => IntervalsIcuInterval,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Not a synced source.")
        };

        if (lastRunAt is not { } last) return true;
        if (lastSuccessAt == last) return now - last >= interval;

        var failingSince = lastSuccessAt is { } success ? now - success : Duration.MaxValue;
        var retry = failingSince < SlowRetry + interval ? QuickRetry : SlowRetry;
        return now - last >= Duration.Min(retry, interval);
    }

    /// <summary>A finding for each source that has gone quiet.</summary>
    public static IReadOnlyList<Highlight> StaleFindings(IEnumerable<SyncState> states, Instant now)
    {
        var findings = new List<Highlight>();
        foreach (var state in states.Where(s => s.LastRunAt is not null))
        {
            var (label, limit) = state.Source == HevySource ? ("Hevy", HevyStaleAfter) : ("intervals.icu", IntervalsIcuStaleAfter);
            if (state.LastSuccessAt is { } success && now - success < limit) continue;

            var since = state.LastSuccessAt is { } at
                ? $"since {at.InUtc().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                : "yet";
            findings.Add(new Highlight("sync-stale", $"{label} has not synced {since}", state.LastOutcome ?? "No outcome recorded.", false));
        }

        return findings;
    }

    /// <summary>
    /// The instant an intervals.icu listing should start from: a little before the
    /// last activity seen, so a late-arriving upload that started earlier than
    /// the last one seen is not skipped for ever.
    /// </summary>
    public static Instant ListFrom(Instant? lastActivityStartedAt) =>
        lastActivityStartedAt is { } last ? last - Duration.FromDays(2) : Instant.FromUtc(2015, 1, 1, 0, 0);
}
