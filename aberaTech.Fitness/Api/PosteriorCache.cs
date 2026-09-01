using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;

namespace aberaTech.Fitness.Api;

/// <summary>
/// The fitted posterior, computed once per version of the athlete's data.
/// </summary>
/// <remarks>
/// Sampling takes a couple of seconds — cheap for a fit, far too slow to sit
/// inside a slider drag. It also does not depend on anything the athlete is
/// dragging: the posterior is a function of the training history, and moving a
/// what-if factor does not change history. So it is computed on the first
/// question after an import and reused until the data change, which is what
/// makes the rest of the console feel immediate.
///
/// The key is the history itself, so an import invalidates the cache without
/// anything having to remember to.
/// </remarks>
public sealed class PosteriorCache
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _key;
    private PosteriorSamples? _cached;

    /// <summary>The posterior for this history, sampling it only if it is new.</summary>
    public async Task<PosteriorSamples> GetAsync(
        IReadOnlyList<FitObservation> observations,
        Posterior.Priors priors,
        double? reclaimVdot,
        CancellationToken cancellationToken)
    {
        var key = KeyOf(observations, priors, reclaimVdot);

        if (_key == key && _cached is { } ready) return ready;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have sampled the same history while this one
            // waited, which is the whole point of holding the lock.
            if (_key == key && _cached is { } justArrived) return justArrived;

            var samples = await Task.Run(
                () => Posterior.Sample(observations, priors, reclaimVdot), cancellationToken);

            _cached = samples;
            _key = key;
            return samples;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Drop the cache, for an import that has just changed the history.</summary>
    public void Invalidate()
    {
        _key = null;
        _cached = null;
    }

    private static string KeyOf(
        IReadOnlyList<FitObservation> observations, Posterior.Priors priors, double? reclaimVdot)
    {
        var text = string.Join(
            "|",
            observations.Select(o =>
                $"{o.Months:0.####}:{o.ObservedVdot:0.####}:{o.Kind}:{o.Dose.EasyHours:0.###}"
                + $",{o.Dose.ThresholdHours:0.###},{o.Dose.IntervalHours:0.###},{o.Dose.StrengthHours:0.###}"));

        return $"{text}#{priors.StartVdot:0.####}#{reclaimVdot:0.####}";
    }
}
