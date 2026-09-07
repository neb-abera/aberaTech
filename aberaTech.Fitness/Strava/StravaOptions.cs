namespace aberaTech.Fitness.Strava;

/// <summary>
/// The Strava API application this deployment speaks as. Optional: without it
/// the Strava bridge is never mapped and the page says so.
/// </summary>
/// <remarks>
/// Strava is the bridge to the watch. Garmin has no personal API, but Garmin
/// Connect pushes every activity to Strava the moment it syncs, and Strava's
/// API is free for personal use — so connecting Strava once ends the ritual of
/// requesting an export and dropping the archive on the page. Both values are
/// container app secrets, never appsettings.
/// </remarks>
public sealed class StravaOptions
{
    public const string Section = "Strava";

    /// <summary>The application's numeric client id, from strava.com/settings/api.</summary>
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
