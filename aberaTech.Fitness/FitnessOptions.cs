namespace aberaTech.Fitness;

/// <summary>Who may see the fitness data, and the optional paid integrations.</summary>
public sealed class FitnessOptions
{
    public const string Section = "Fitness";

    /// <summary>
    /// The Google accounts allowed to read and write fitness data.
    /// </summary>
    /// <remarks>
    /// Same reasoning as the scheduling admin allowlist: authentication proves
    /// somebody has a Google account; this proves it is the athlete. Heart
    /// rate, bodyweight and training history are health data — nobody else's
    /// business — so with no allowlist the endpoints are never mapped at all.
    /// </remarks>
    public string[] AllowedEmails { get; set; } = [];

    /// <summary>
    /// Hevy API key, from hevy.com/settings?developer. Requires Hevy Pro
    /// (~$24/year) — optional on purpose: the CSV upload path costs nothing.
    /// A container app secret, never appsettings.
    /// </summary>
    public string HevyApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Skip sign-in entirely, so `make up` shows the real console with no
    /// Google project and no allowlist.
    /// </summary>
    /// <remarks>
    /// Honored only when the host environment is Development — the gate below
    /// enforces that, not this flag — so setting it in a deployed
    /// configuration does nothing. It exists because the local database is
    /// empty and loopback-only: there is no health data for the bypass to
    /// expose that the developer did not just upload to their own machine.
    /// </remarks>
    public bool DevelopmentOwner { get; set; }

    public bool IsConfigured => AllowedEmails.Length > 0;

    public bool HasHevyApi => !string.IsNullOrWhiteSpace(HevyApiKey);

    public bool Allows(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && AllowedEmails.Any(allowed => string.Equals(allowed, email, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The one decision table for whether the fitness surface exists and whether
/// it requires sign-in. Pure, so the fail-closed contract is unit-testable
/// without booting anything.
/// </summary>
public static class FitnessGate
{
    /// <summary>Sign-in is required unless this is a Development host with the explicit bypass set.</summary>
    public static bool RequiresOwnerSignIn(bool isDevelopment, FitnessOptions options) =>
        !(isDevelopment && options.DevelopmentOwner);

    /// <summary>
    /// The endpoints exist when there is a database and either a real
    /// allowlist backed by configured sign-in, or the Development bypass.
    /// Anything else fails closed.
    /// </summary>
    public static bool IsEnabled(
        bool isDevelopment,
        FitnessOptions options,
        bool adminAuthConfigured,
        string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        if (!RequiresOwnerSignIn(isDevelopment, options)) return true;

        return options.IsConfigured && adminAuthConfigured;
    }
}
