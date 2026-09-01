namespace aberaTech.Fitness.Domain;

/// <summary>
/// The random numbers the sampler runs on, made reproducible.
/// </summary>
/// <remarks>
/// A model that answers a question differently on a refresh is a model nobody
/// can check, so the generator is written out rather than taken from the
/// runtime: <c>Random</c> is only guaranteed deterministic for a given .NET
/// version, and these answers should survive an upgrade.
///
/// xoshiro256++ with a SplitMix64 seeder — the standard small, fast,
/// well-tested pair, and short enough to read.
/// </remarks>
public sealed class Rng
{
    private ulong _s0, _s1, _s2, _s3;
    private double? _spare;

    public Rng(ulong seed)
    {
        _s0 = SplitMix(ref seed);
        _s1 = SplitMix(ref seed);
        _s2 = SplitMix(ref seed);
        _s3 = SplitMix(ref seed);
    }

    private static ulong SplitMix(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Rotate(ulong x, int bits) => (x << bits) | (x >> (64 - bits));

    private ulong Next()
    {
        var result = Rotate(_s0 + _s3, 23) + _s0;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotate(_s3, 45);

        return result;
    }

    /// <summary>Uniform on [0, 1).</summary>
    public double Uniform() => (Next() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Standard normal, by Box-Muller with the second draw kept.</summary>
    public double Normal()
    {
        if (_spare is { } kept)
        {
            _spare = null;
            return kept;
        }

        // Guard the log: Uniform() can return exactly zero.
        var u1 = Math.Max(Uniform(), 1e-300);
        var u2 = Uniform();
        var radius = Math.Sqrt(-2 * Math.Log(u1));
        var angle = 2 * Math.PI * u2;

        _spare = radius * Math.Sin(angle);
        return radius * Math.Cos(angle);
    }

    public double Normal(double mean, double sd) => mean + sd * Normal();
}

/// <summary>Summary statistics over a set of posterior draws.</summary>
public static class Statistic
{
    /// <summary>The value below which <paramref name="probability"/> of the draws fall.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double probability)
    {
        if (sorted.Count == 0) throw new ArgumentException("No draws.", nameof(sorted));
        if (sorted.Count == 1) return sorted[0];

        var position = Math.Clamp(probability, 0, 1) * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    public static double[] Sorted(IEnumerable<double> values)
    {
        var array = values.ToArray();
        Array.Sort(array);
        return array;
    }

    public static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Average();

    public static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }

    /// <summary>The share of draws satisfying a condition — a probability, by counting.</summary>
    public static double Share<T>(IReadOnlyList<T> draws, Func<T, bool> holds) =>
        draws.Count == 0 ? 0 : draws.Count(holds) / (double)draws.Count;
}
