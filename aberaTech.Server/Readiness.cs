namespace aberaTech.Server;

/// <summary>One dependency's answer to the readiness probe.</summary>
/// <param name="Name">Which dependency.</param>
/// <param name="Configured">Whether this deployment switched it on at all.</param>
/// <param name="Ok">False only when something configured did not answer.</param>
/// <param name="Detail">A short classification, never a provider message.</param>
public sealed record ReadinessCheck(string Name, bool Configured, bool Ok, string Detail);

/// <summary>The readiness verdict and the checks behind it.</summary>
public sealed record ReadinessReport(bool Ready, IReadOnlyList<ReadinessCheck> Checks)
{
    /// <summary>503 when something configured did not answer, 200 otherwise.</summary>
    /// <remarks>
    /// Not serialised: the HTTP status already carries it, and a body that
    /// repeats it invites the two to disagree.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public int StatusCode => Ready ? 200 : 503;
}

/// <summary>
/// Turns a set of dependency checks into a verdict a deploy can act on.
/// </summary>
/// <remarks>
/// The rule is one line and it is the whole point of the endpoint, so it lives
/// where it can be tested rather than inside a lambda in Program.cs: a
/// subsystem this deployment never switched on is not a failure, and a
/// subsystem it did switch on and cannot reach is.
///
/// The distinction matters because both features here fail closed. A
/// deployment with no databases is a legitimate deployment of this app, and a
/// probe that called it unready would block every release of a site that is
/// working exactly as designed.
/// </remarks>
public static class Readiness
{
    public static ReadinessCheck NotConfigured(string name) => new(name, false, true, "not configured");

    public static ReadinessCheck Reachable(string name) => new(name, true, true, "reachable");

    public static ReadinessCheck Unreachable(string name, string detail) => new(name, true, false, detail);

    public static ReadinessReport From(IReadOnlyList<ReadinessCheck> checks) =>
        new(checks.All(check => check.Ok), checks);
}
