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

    public bool IsConfigured => AllowedEmails.Length > 0;

    public bool HasHevyApi => !string.IsNullOrWhiteSpace(HevyApiKey);

    public bool Allows(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && AllowedEmails.Any(allowed => string.Equals(allowed, email, StringComparison.OrdinalIgnoreCase));
}
