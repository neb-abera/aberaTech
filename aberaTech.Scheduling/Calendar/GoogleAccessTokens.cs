using System.Text.Json;
using aberaTech.Scheduling.Admin;
using Microsoft.AspNetCore.DataProtection;
using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>
/// Turns the stored refresh token into a usable access token, and keeps it
/// until it expires.
/// </summary>
/// <remarks>
/// Raw HTTP against Google's token endpoint rather than the Google SDK, for the
/// same reason the Twilio sender is: this is one form POST, and a public facing
/// service does not need a client library and its transitive graph to make it.
/// </remarks>
public sealed class GoogleAccessTokens(
    HttpClient http,
    AdminOptions admin,
    IDataProtectionProvider protection,
    IClock clock,
    ILogger<GoogleAccessTokens> logger)
{
    /// <summary>
    /// The purpose string ties protected values to this use. A token protected
    /// for the calendar cannot be unprotected by anything created for another
    /// purpose, so one compromised protector does not unlock the rest.
    /// </summary>
    public const string ProtectionPurpose = "aberaTech.Scheduling.GoogleRefreshToken";

    private string? cachedToken;
    private Instant expiresAt = Instant.MinValue;

    public string Protect(string refreshToken) =>
        protection.CreateProtector(ProtectionPurpose).Protect(refreshToken);

    public async Task<string?> GetAccessTokenAsync(string protectedRefreshToken, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();

        if (cachedToken is not null && now < expiresAt)
        {
            return cachedToken;
        }

        string refreshToken;
        try
        {
            refreshToken = protection.CreateProtector(ProtectionPurpose).Unprotect(protectedRefreshToken);
        }
        catch (Exception exception)
        {
            // Usually means the data protection keys were lost, so the stored
            // token can never be read again and the host has to reconnect.
            logger.LogError(exception, "The stored Google refresh token could not be unprotected.");
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = admin.GoogleClientId,
                ["client_secret"] = admin.GoogleClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            })
        };

        using var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The status only. The body of a token response is exactly the kind
            // of thing that should never reach a log.
            logger.LogWarning("Google refused to refresh the access token ({StatusCode}).", (int)response.StatusCode);
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (!document.RootElement.TryGetProperty("access_token", out var token))
        {
            return null;
        }

        var lifetime = document.RootElement.TryGetProperty("expires_in", out var expiresIn)
            ? expiresIn.GetInt32()
            : 3600;

        cachedToken = token.GetString();

        // Renew a minute early rather than at the boundary, so a request that
        // starts just before expiry does not arrive just after it.
        expiresAt = now + Duration.FromSeconds(Math.Max(lifetime - 60, 30));

        return cachedToken;
    }
}
