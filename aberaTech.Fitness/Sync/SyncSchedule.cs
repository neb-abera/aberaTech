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
/// thousand allowed. A failed run does not shorten the wait: retrying a dead
/// token every tick is how a rate limit gets spent on errors.
/// </remarks>
public static class SyncSchedule
{
    public const string HevySource = "hevy-api";
    public const string IntervalsIcuSource = "intervals-icu";

    public static readonly Duration HevyInterval = Duration.FromHours(24);
    public static readonly Duration IntervalsIcuInterval = Duration.FromHours(1);

    /// <summary>Whether a source whose last attempt was <paramref name="lastRunAt"/> should run now.</summary>
    public static bool IsDue(string source, Instant? lastRunAt, Instant now)
    {
        if (lastRunAt is not { } last) return true;

        var interval = source switch
        {
            HevySource => HevyInterval,
            IntervalsIcuSource => IntervalsIcuInterval,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Not a synced source.")
        };

        return now - last >= interval;
    }

    /// <summary>
    /// The instant an intervals.icu listing should start from: a little before the
    /// last activity seen, so a late-arriving upload that started earlier than
    /// the last one seen is not skipped for ever.
    /// </summary>
    public static Instant ListFrom(Instant? lastActivityStartedAt) =>
        lastActivityStartedAt is { } last ? last - Duration.FromDays(2) : Instant.FromUtc(2015, 1, 1, 0, 0);
}
