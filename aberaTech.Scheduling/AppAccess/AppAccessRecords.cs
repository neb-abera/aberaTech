using NodaTime;

namespace aberaTech.Scheduling.AppAccess;

/// <summary>
/// A one-time code from a finished Google sign-in, waiting for the aberaTech
/// iPhone app to trade it for a session. Lives 2 minutes and is used once.
/// </summary>
public class AppSignInCodeRecord
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the code. The code itself only ever travels in the redirect to the app.</summary>
    public byte[] CodeHash { get; set; } = [];

    /// <summary>The app's PKCE challenge: base64url SHA-256 of the verifier only the app holds.</summary>
    public string Challenge { get; set; } = string.Empty;

    /// <summary>Who signed in, by the name the app knows them by: the owner or a household member.</summary>
    public string Person { get; set; } = string.Empty;

    public Instant ExpiresAt { get; set; }
}

/// <summary>A signed-in aberaTech app on one phone. The token is stored as its hash.</summary>
public class AppSessionRecord
{
    public Guid Id { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public string Person { get; set; } = string.Empty;

    /// <summary>The phone's name, as the app reports it.</summary>
    public string Device { get; set; } = string.Empty;

    public Instant CreatedAt { get; set; }

    /// <summary>The last request the token made, written at most once a minute.</summary>
    public Instant? LastSeenAt { get; set; }
}

/// <summary>
/// Finishes a Google sign-in that the aberaTech app started, instead of
/// setting the site's cookie. The server project registers it.
/// </summary>
public interface IAppSignInHandoff
{
    /// <summary>The marker the app's sign-in puts in the authentication properties.</summary>
    const string ChallengeItem = "app.challenge";

    /// <summary>Where to send the browser sheet: the app's scheme with a code, or with an error.</summary>
    Task<string> CompleteAsync(string? email, string challenge, string state, CancellationToken cancellationToken);
}
