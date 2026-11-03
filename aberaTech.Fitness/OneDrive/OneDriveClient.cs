using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace aberaTech.Fitness.OneDrive;

/// <summary>
/// The OneDrive account the goals workbook is read from. Optional: without a
/// client id and secret the connection is never mapped and the Goals tab says
/// so.
/// </summary>
/// <remarks>
/// An app registration in Entra ID for personal Microsoft accounts, with the
/// delegated <c>Files.Read</c> scope and the callback
/// <c>https://abera.tech/api/fitness/onedrive/callback</c>. The secret is a
/// container app secret, never appsettings.
/// </remarks>
public sealed class OneDriveOptions
{
    public const string Section = "OneDrive";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The workbook's path from the drive's root.</summary>
    public string WorkbookPath { get; set; } = "Documents/physical fitness/physical fitness goals.xlsx";

    /// <summary>
    /// A workbook on disk to read instead of OneDrive: the e2e suite's fixture,
    /// or a developer's copy under `make up`. Never set in production.
    /// </summary>
    public string LocalWorkbookPath { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>What a code exchange or a refresh hands back.</summary>
/// <param name="AccessToken">Good for about an hour.</param>
/// <param name="RefreshToken">Rotated on every refresh for personal accounts; the newest one is kept.</param>
/// <param name="ExpiresInSeconds">The access token's lifetime.</param>
public sealed record OneDriveTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>A file's identity, enough to tell whether it changed.</summary>
public sealed record DriveFile(string ETag, DateTimeOffset? LastModified, long Size);

/// <summary>Why a read failed, in words the page can show.</summary>
public sealed class OneDriveException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// Microsoft identity and Graph, as far as reading one file goes: the
/// authorization-code round trip with PKCE, refresh, the file's metadata and
/// its bytes. Raw HTTP, as the Strava and Hevy calls were: four requests do
/// not need the Graph SDK and its dependency graph.
/// </summary>
/// <remarks>
/// Graph's Excel API does not serve personal OneDrive ("Working with Excel in
/// Microsoft Graph", Microsoft Learn), so the file itself is downloaded and
/// parsed here. The consumers authority is the one personal accounts sign in
/// at.
/// </remarks>
public sealed class OneDriveClient(HttpClient http, OneDriveOptions options)
{
    public const string Authority = "https://login.microsoftonline.com/consumers/oauth2/v2.0/";
    public const string Graph = "https://graph.microsoft.com/v1.0/";
    public const string Scope = "Files.Read offline_access";

    /// <summary>The largest file the client will download.</summary>
    public const long MaxDownloadBytes = 5 * 1024 * 1024;

    /// <summary>Where to send the browser to grant access.</summary>
    public static string AuthorizeRedirect(string clientId, string redirectUri, string state, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["response_mode"] = "query",
            ["scope"] = Scope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        return Authority + "authorize?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    /// <summary>A fresh PKCE verifier: 32 random bytes, base64url.</summary>
    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge for a verifier.</summary>
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public Task<OneDriveTokens> ExchangeCodeAsync(string code, string redirectUri, string verifier, CancellationToken cancellationToken) =>
        TokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier,
            ["scope"] = Scope
        }, cancellationToken);

    public Task<OneDriveTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        TokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = Scope
        }, cancellationToken);

    /// <summary>The file's ETag, last change and size.</summary>
    public async Task<DriveFile> MetadataAsync(string accessToken, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Graph}me/drive/root:/{EscapePath(path)}?$select=eTag,lastModifiedDateTime,size");
        request.Headers.Authorization = new("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureAsync(response, path, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var etag = root.TryGetProperty("eTag", out var e) ? e.GetString() ?? "" : "";
        DateTimeOffset? modified = root.TryGetProperty("lastModifiedDateTime", out var m) && m.TryGetDateTimeOffset(out var when) ? when : null;
        var size = root.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
        return new DriveFile(etag, modified, size);
    }

    /// <summary>The file's bytes. Graph answers with a redirect to a pre-signed address, which the handler follows without the bearer token.</summary>
    public async Task<byte[]> DownloadAsync(string accessToken, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Graph}me/drive/root:/{EscapePath(path)}:/content");
        request.Headers.Authorization = new("Bearer", accessToken);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureAsync(response, path, cancellationToken);

        if (response.Content.Headers.ContentLength > MaxDownloadBytes)
        {
            throw new OneDriveException("The workbook is larger than a goals workbook can be.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxDownloadBytes) throw new OneDriveException("The workbook is larger than a goals workbook can be.");
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    internal static string EscapePath(string path) =>
        string.Join("/", path.Trim('/').Split('/').Select(Uri.EscapeDataString));

    private async Task<OneDriveTokens> TokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Authority + "token")
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant is the one that needs the owner: the grant was
            // revoked, or unused long enough to lapse.
            var refused = body.Contains("invalid_grant", StringComparison.Ordinal);
            throw new OneDriveException(
                refused ? "Microsoft refused the stored sign-in. Connect OneDrive again." : $"Microsoft sign-in answered {(int)response.StatusCode}.",
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var access) || !root.TryGetProperty("refresh_token", out var refresh))
        {
            throw new OneDriveException("Microsoft sign-in answered without a token.");
        }

        var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds) ? seconds : 3600;
        return new OneDriveTokens(access.GetString() ?? "", refresh.GetString() ?? "", expiresIn);
    }

    private static async Task EnsureAsync(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        _ = await response.Content.ReadAsStringAsync(cancellationToken);
        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new OneDriveException($"OneDrive has no file at {path}.", response.StatusCode),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new OneDriveException("OneDrive refused the key. Connect OneDrive again.", response.StatusCode),
            HttpStatusCode.Locked => new OneDriveException("The workbook is locked for editing. It is read again on the next load.", response.StatusCode),
            _ => new OneDriveException($"OneDrive answered {(int)response.StatusCode}.", response.StatusCode)
        };
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
