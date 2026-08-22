namespace aberaTech.Scheduling.Admin;

/// <summary>Who may run the queue, and the Google credentials that prove it.</summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>The OAuth client id from the Google Cloud console.</summary>
    public string GoogleClientId { get; set; } = string.Empty;

    /// <summary>The OAuth client secret. A container app secret, never appsettings.</summary>
    public string GoogleClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The Google accounts allowed to administer the queue.
    /// </summary>
    /// <remarks>
    /// An allowlist, not a role claim. Anybody in the world can complete a
    /// Google sign-in, so authentication alone proves only that somebody has
    /// *a* Google account; without this check the admin surface would be open
    /// to all of them. Authorisation is the allowlist, and it is the only thing
    /// standing between a stranger and a list of soldiers' phone numbers.
    /// </remarks>
    public string[] AllowedEmails { get; set; } = [];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GoogleClientId)
        && !string.IsNullOrWhiteSpace(GoogleClientSecret)
        && AllowedEmails.Length > 0;

    public bool Allows(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && AllowedEmails.Any(allowed => string.Equals(allowed, email, StringComparison.OrdinalIgnoreCase));
}
