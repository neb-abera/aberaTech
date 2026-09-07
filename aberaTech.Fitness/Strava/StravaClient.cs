using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace aberaTech.Fitness.Strava;

/// <summary>What a code exchange or a refresh hands back.</summary>
/// <param name="AccessToken">Good for six hours.</param>
/// <param name="RefreshToken">Good until revoked; may be rotated on refresh.</param>
/// <param name="ExpiresAtUnix">When the access token dies, as a Unix timestamp.</param>
/// <param name="AthleteId">The athlete the grant belongs to, on exchange.</param>
public sealed record StravaTokens(string AccessToken, string RefreshToken, long ExpiresAtUnix, long? AthleteId);

/// <summary>One activity as Strava summarises it.</summary>
public sealed record StravaActivity(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("sport_type")] string? SportType,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("start_date")] string? StartDate,
    [property: JsonPropertyName("distance")] double? DistanceMeters,
    [property: JsonPropertyName("moving_time")] double? MovingTimeSeconds,
    [property: JsonPropertyName("elapsed_time")] double? ElapsedTimeSeconds,
    [property: JsonPropertyName("average_heartrate")] double? AverageHeartRate,
    [property: JsonPropertyName("max_heartrate")] double? MaxHeartRate,
    [property: JsonPropertyName("trainer")] bool? Trainer,
    [property: JsonPropertyName("manual")] bool? Manual);

/// <summary>One lap of an activity, as Strava recorded it.</summary>
public sealed record StravaLap(
    [property: JsonPropertyName("lap_index")] int LapIndex,
    [property: JsonPropertyName("distance")] double? DistanceMeters,
    [property: JsonPropertyName("moving_time")] double? MovingTimeSeconds,
    [property: JsonPropertyName("elapsed_time")] double? ElapsedTimeSeconds,
    [property: JsonPropertyName("average_heartrate")] double? AverageHeartRate);

/// <summary>
/// Strava's API, as far as this library uses it: the OAuth round trip and two
/// reads. Raw HTTP, for the same reason the Google and Twilio calls are: a
/// handful of requests do not need a client library and its dependency graph.
/// </summary>
/// <remarks>
/// Rate limits are the constraint that shapes the sync: 100 read requests per
/// fifteen minutes and 1,000 a day. Listing activities since the last sync is
/// one request; laps are one more per new activity. An hourly sync of a
/// one-athlete log spends a few dozen a day.
/// </remarks>
public sealed class StravaClient(HttpClient http, StravaOptions options)
{
    public const string ApiBase = "https://www.strava.com/api/v3/";
    public const string AuthorizeUrl = "https://www.strava.com/oauth/authorize";
    public const string Scope = "activity:read_all";

    /// <summary>Activities per page; Strava's maximum is 200.</summary>
    public const int PageSize = 100;

    /// <summary>Where to send the browser to grant access.</summary>
    public static string AuthorizeRedirect(string clientId, string redirectUri, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["approval_prompt"] = "auto",
            ["scope"] = Scope,
            ["state"] = state
        };

        return AuthorizeUrl + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<StravaTokens?> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await TokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code"
        }, cancellationToken);
    }

    public async Task<StravaTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return await TokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }, cancellationToken);
    }

    /// <summary>Tell Strava the grant is over, so the athlete's list of apps stays honest.</summary>
    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.strava.com/oauth/deauthorize")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = token })
        };
        using var response = await http.SendAsync(request, cancellationToken);
        // A failed revoke is not worth failing the disconnect over: the stored
        // token is gone either way, and the athlete can revoke from Strava.
    }

    /// <summary>Activities started after <paramref name="afterUnix"/>, newest last, one page.</summary>
    public async Task<IReadOnlyList<StravaActivity>> ActivitiesAsync(
        string accessToken, long afterUnix, int page, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ApiBase}athlete/activities?after={afterUnix}&page={page}&per_page={PageSize}");
        request.Headers.Authorization = new("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<StravaActivity>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<StravaLap>> LapsAsync(string accessToken, long activityId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}activities/{activityId}/laps");
        request.Headers.Authorization = new("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<StravaLap>>(cancellationToken) ?? [];
    }

    private async Task<StravaTokens?> TokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}oauth/token")
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;

        if (!root.TryGetProperty("access_token", out var access) || !root.TryGetProperty("refresh_token", out var refresh))
        {
            return null;
        }

        var expiresAt = root.TryGetProperty("expires_at", out var exp) && exp.ValueKind == JsonValueKind.Number
            ? exp.GetInt64()
            : DateTimeOffset.UtcNow.AddHours(5).ToUnixTimeSeconds();

        long? athleteId = root.TryGetProperty("athlete", out var athlete)
                          && athlete.ValueKind == JsonValueKind.Object
                          && athlete.TryGetProperty("id", out var id)
                          && id.ValueKind == JsonValueKind.Number
            ? id.GetInt64()
            : null;

        return new StravaTokens(access.GetString() ?? "", refresh.GetString() ?? "", expiresAt, athleteId);
    }

    internal static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
