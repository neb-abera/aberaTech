namespace aberaTech.Fitness.IntervalsIcu;

/// <summary>
/// The intervals.icu account this deployment reads. Optional: without a key
/// the bridge is never mapped and the page says so.
/// </summary>
/// <remarks>
/// intervals.icu is the bridge to the watch. Garmin has no personal API and
/// Strava's now costs a subscription, but Garmin Connect pushes every
/// activity to intervals.icu through Garmin's own partner integration, and
/// intervals.icu's personal API is free: one key from Settings → Developer,
/// sent as basic auth. It also keeps the original FIT file, so what arrives
/// here is exactly what a manual upload would have been — laps, sub-sport
/// and all. The key is a container app secret, never appsettings.
/// </remarks>
public sealed class IntervalsIcuOptions
{
    public const string Section = "IntervalsIcu";

    /// <summary>The personal API key from intervals.icu Settings → Developer.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The athlete the key belongs to; "0" means the key's own athlete.</summary>
    public string AthleteId { get; set; } = "0";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
