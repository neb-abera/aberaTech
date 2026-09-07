using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A wall-clock budget that is reported always and enforced only on request.
/// </summary>
/// <remarks>
/// The tests that use this exist to catch one thing: a return to the quadratic
/// likelihood, where the trajectory was integrated once per observation instead
/// of once per proposal and a fit took thirty-seven seconds. A clock cannot
/// tell that apart from a loaded runner — under <c>make check</c>, with three
/// image builds and a browser competing for the machine, a healthy fit has
/// taken longer than the old bound — so the assertion is off by default and
/// the test is a build-and-run smoke gate, as harnesses are meant to be. Set
/// <see cref="Variable"/> to any value to profile on a quiet machine, where
/// the number means something:
/// <code>ABERA_ENFORCE_TIMING=1 make servertest</code>
/// The measured time is printed either way.
/// </remarks>
internal static class TimingBudget
{
    public const string Variable = "ABERA_ENFORCE_TIMING";

    public static bool Enforced =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));

    /// <summary>Time <paramref name="work"/>, report it, and assert the budget only when enforced.</summary>
    public static T Measure<T>(ITestOutputHelper output, string label, TimeSpan budget, Func<T> work)
    {
        var clock = Stopwatch.StartNew();
        var result = work();
        clock.Stop();

        var verdict = Enforced ? "enforced" : $"reported only; set {Variable} to enforce";
        output.WriteLine($"{label} in {clock.ElapsedMilliseconds} ms (budget {budget.TotalMilliseconds:0} ms, {verdict})");

        if (Enforced)
        {
            Assert.True(clock.Elapsed < budget, $"{label} took {clock.ElapsedMilliseconds} ms, over the {budget.TotalMilliseconds:0} ms budget");
        }

        return result;
    }
}
