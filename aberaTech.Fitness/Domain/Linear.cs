namespace aberaTech.Fitness.Domain;

/// <summary>
/// The small dense linear algebra the model fit needs: solving normal
/// equations and inverting them for a covariance matrix.
/// </summary>
/// <remarks>
/// Three parameters means 3×3 systems, so this is LU with partial pivoting
/// written out rather than a matrix package added to the dependency tree.
/// </remarks>
internal static class Linear
{
    /// <summary>Solves A·x = b for a square A, by Gaussian elimination with partial pivoting.</summary>
    public static double[]? Solve(double[,] a, double[] b)
    {
        var n = b.Length;
        var m = (double[,])a.Clone();
        var x = (double[])b.Clone();

        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < n; row++)
            {
                if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col])) pivot = row;
            }

            if (Math.Abs(m[pivot, col]) < 1e-14) return null;

            if (pivot != col)
            {
                for (var k = 0; k < n; k++) (m[col, k], m[pivot, k]) = (m[pivot, k], m[col, k]);
                (x[col], x[pivot]) = (x[pivot], x[col]);
            }

            for (var row = col + 1; row < n; row++)
            {
                var factor = m[row, col] / m[col, col];
                if (factor == 0) continue;
                for (var k = col; k < n; k++) m[row, k] -= factor * m[col, k];
                x[row] -= factor * x[col];
            }
        }

        for (var row = n - 1; row >= 0; row--)
        {
            var sum = x[row];
            for (var k = row + 1; k < n; k++) sum -= m[row, k] * x[k];
            x[row] = sum / m[row, row];
        }

        return x;
    }

    /// <summary>Inverts a square matrix, or null when it is singular.</summary>
    public static double[,]? Invert(double[,] a)
    {
        var n = a.GetLength(0);
        var inverse = new double[n, n];
        for (var col = 0; col < n; col++)
        {
            var unit = new double[n];
            unit[col] = 1;
            var solved = Solve(a, unit);
            if (solved is null) return null;
            for (var row = 0; row < n; row++) inverse[row, col] = solved[row];
        }

        return inverse;
    }

    /// <summary>
    /// The lower-triangular Cholesky factor L with L·Lᵀ = A, or null when A is
    /// not positive definite.
    /// </summary>
    /// <remarks>
    /// This is how a sampler proposes moves shaped like the distribution it is
    /// exploring: draw z from independent standard normals and L·z has the
    /// covariance A. Without it, a proposal that ignores the correlation
    /// between parameters spends its life being rejected.
    /// </remarks>
    public static double[,]? Cholesky(double[,] a)
    {
        var n = a.GetLength(0);
        var l = new double[n, n];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j <= i; j++)
            {
                var sum = a[i, j];
                for (var k = 0; k < j; k++) sum -= l[i, k] * l[j, k];

                if (i == j)
                {
                    if (sum <= 0) return null;
                    l[i, j] = Math.Sqrt(sum);
                }
                else
                {
                    l[i, j] = sum / l[j, j];
                }
            }
        }

        return l;
    }

    /// <summary>The standard normal cumulative distribution.</summary>
    /// <remarks>
    /// Erf by the Abramowitz &amp; Stegun 7.1.26 rational approximation, which
    /// is accurate to 1.5×10⁻⁷ — four more digits than any probability this
    /// app should be quoting to an athlete.
    /// </remarks>
    public static double NormalCdf(double z)
    {
        var sign = z < 0 ? -1 : 1;
        var x = Math.Abs(z) / Math.Sqrt(2);

        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
        const double a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;

        var t = 1.0 / (1.0 + p * x);
        var erf = 1 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * erf);
    }

    /// <summary>The z for a two-sided interval of the given coverage.</summary>
    public static double NormalQuantile(double probability)
    {
        if (probability is <= 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(probability));

        double low = -10, high = 10;
        for (var i = 0; i < 200; i++)
        {
            var mid = (low + high) / 2;
            if (NormalCdf(mid) < probability) low = mid; else high = mid;
        }

        return (low + high) / 2;
    }
}
