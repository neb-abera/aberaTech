using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using aberaTech.Scheduling.Admin;
using aberaTech.Scheduling.AppAccess;
using aberaTech.Scheduling.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace aberaTech.Server.App;

/// <summary>The app's bearer tokens and one-time codes.</summary>
public static class AppTokens
{
    public const string Prefix = "abt_";

    /// <summary>"abt_" and 43 base64url characters, the unpadded encoding of 32 bytes.</summary>
    public const int Length = 47;

    public static string New() => Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string NewCode() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static bool IsWellFormed(string? token) =>
        token is { Length: Length }
        && token.StartsWith(Prefix, StringComparison.Ordinal)
        && token.AsSpan(Prefix.Length).ContainsAnyExcept(Base64UrlCharacters) is false;

    public static byte[] Hash(string value) => SHA256.HashData(Encoding.ASCII.GetBytes(value));

    /// <summary>PKCE S256: base64url SHA-256 of the verifier.</summary>
    public static string Challenge(string verifier) => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>A verifier or challenge as RFC 7636 allows: 43 to 128 unreserved characters.</summary>
    public static bool IsPkceValue(string? value) =>
        value is { Length: >= 43 and <= 128 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~');

    private static readonly System.Buffers.SearchValues<char> Base64UrlCharacters =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");
}

/// <summary>A stored session, as the token handler needs it.</summary>
public sealed record AppSession(Guid Id, string Person, string Device, Instant CreatedAt, Instant? LastSeenAt);

/// <summary>The codes and sessions. The database in production, memory in tests.</summary>
public interface IAppAccessStore
{
    Task AddCodeAsync(byte[] codeHash, string challenge, string person, Instant expiresAt, CancellationToken cancellationToken);

    /// <summary>Removes the code and returns its challenge and person; null when it is unknown or expired.</summary>
    Task<(string Challenge, string Person)?> RedeemCodeAsync(byte[] codeHash, Instant now, CancellationToken cancellationToken);

    Task AddSessionAsync(Guid id, byte[] tokenHash, string person, string device, Instant now, CancellationToken cancellationToken);

    Task<AppSession?> FindSessionAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task TouchAsync(Guid id, Instant now, CancellationToken cancellationToken);

    Task RevokeAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class DatabaseAppAccessStore(SchedulingDbContext database) : IAppAccessStore
{
    public async Task AddCodeAsync(byte[] codeHash, string challenge, string person, Instant expiresAt, CancellationToken cancellationToken)
    {
        // Expired codes go as new ones arrive: there are a handful a month.
        await database.AppSignInCodes.Where(code => code.ExpiresAt < expiresAt - AppSignIn.CodeLifetime)
            .ExecuteDeleteAsync(cancellationToken);
        database.AppSignInCodes.Add(new AppSignInCodeRecord
        {
            Id = Guid.NewGuid(), CodeHash = codeHash, Challenge = challenge, Person = person, ExpiresAt = expiresAt
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<(string Challenge, string Person)?> RedeemCodeAsync(byte[] codeHash, Instant now, CancellationToken cancellationToken)
    {
        var code = await database.AppSignInCodes.SingleOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);
        if (code is null) return null;
        // Used once, whatever happens next: a second exchange of the same code finds nothing.
        var deleted = await database.AppSignInCodes.Where(c => c.Id == code.Id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 1 && code.ExpiresAt > now ? (code.Challenge, code.Person) : null;
    }

    public async Task AddSessionAsync(Guid id, byte[] tokenHash, string person, string device, Instant now, CancellationToken cancellationToken)
    {
        database.AppSessions.Add(new AppSessionRecord
        {
            Id = id, TokenHash = tokenHash, Person = person, Device = device, CreatedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppSession?> FindSessionAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        await database.AppSessions.AsNoTracking().Where(s => s.TokenHash == tokenHash)
            .Select(s => new AppSession(s.Id, s.Person, s.Device, s.CreatedAt, s.LastSeenAt))
            .SingleOrDefaultAsync(cancellationToken);

    public Task TouchAsync(Guid id, Instant now, CancellationToken cancellationToken) =>
        database.AppSessions.Where(s => s.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSeenAt, now), cancellationToken);

    public Task RevokeAsync(Guid id, CancellationToken cancellationToken) =>
        database.AppSessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
}

/// <summary>For a deployment or a test with no scheduling database.</summary>
public sealed class MemoryAppAccessStore : IAppAccessStore
{
    private readonly ConcurrentDictionary<string, (string Challenge, string Person, Instant ExpiresAt)> _codes = new();
    private readonly ConcurrentDictionary<string, AppSession> _sessions = new();

    private static string Key(byte[] hash) => Convert.ToHexString(hash);

    public int Sessions => _sessions.Count;

    public Task AddCodeAsync(byte[] codeHash, string challenge, string person, Instant expiresAt, CancellationToken cancellationToken)
    {
        _codes[Key(codeHash)] = (challenge, person, expiresAt);
        return Task.CompletedTask;
    }

    public Task<(string Challenge, string Person)?> RedeemCodeAsync(byte[] codeHash, Instant now, CancellationToken cancellationToken) =>
        Task.FromResult<(string, string)?>(
            _codes.TryRemove(Key(codeHash), out var code) && code.ExpiresAt > now ? (code.Challenge, code.Person) : null);

    public Task AddSessionAsync(Guid id, byte[] tokenHash, string person, string device, Instant now, CancellationToken cancellationToken)
    {
        _sessions[Key(tokenHash)] = new AppSession(id, person, device, now, null);
        return Task.CompletedTask;
    }

    public Task<AppSession?> FindSessionAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(Key(tokenHash), out var session) ? session : null);

    public Task TouchAsync(Guid id, Instant now, CancellationToken cancellationToken)
    {
        foreach (var (key, session) in _sessions)
        {
            if (session.Id == id) _sessions[key] = session with { LastSeenAt = now };
        }

        return Task.CompletedTask;
    }

    public Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        foreach (var (key, session) in _sessions)
        {
            if (session.Id == id) _sessions.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Who may call /api/app: a signed-in app, and the owner alone on his own routes.</summary>
public static class AppAuth
{
    public const string Scheme = "app-token";

    /// <summary>The owner or a household member, signed in to the app.</summary>
    public const string SignedInPolicy = "app-signed-in";

    /// <summary>The owner alone: his weigh-ins.</summary>
    public const string OwnerPolicy = "app-owner";

    public const string PersonClaim = "aberatech:app-person";
    public const string RoleClaim = "aberatech:app-role";
    public const string SessionClaim = "aberatech:app-session";

    public static readonly Duration TouchEvery = Duration.FromMinutes(1);

    public static IServiceCollection AddAppAuth(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AppTokenAuthenticationHandler>(Scheme, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(SignedInPolicy, policy => policy
                .AddAuthenticationSchemes(Scheme)
                .RequireAuthenticatedUser()
                .RequireClaim(PersonClaim))
            .AddPolicy(OwnerPolicy, policy => policy
                .AddAuthenticationSchemes(Scheme)
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaim, nameof(AppRole.Owner)));
        return services;
    }

    public static AppPerson? Person(ClaimsPrincipal user)
    {
        var name = user.FindFirstValue(PersonClaim);
        var role = user.FindFirstValue(RoleClaim);
        return name is null || !Enum.TryParse<AppRole>(role, out var parsed) ? null : new AppPerson(name, parsed);
    }

    public static Guid? Session(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionClaim), out var id) ? id : null;
}

/// <summary>
/// <c>Authorization: Bearer abt_…</c>, looked up by its SHA-256. A session
/// whose person is no longer in the configuration is refused, so taking
/// an address out of App:Household signs that person out everywhere.
/// </summary>
public sealed class AppTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IClock clock,
    AppOptions app) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var token = header[Bearer.Length..].Trim();
        if (!AppTokens.IsWellFormed(token)) return AuthenticateResult.Fail("malformed");
        if (Context.RequestServices.GetService<IAppAccessStore>() is not { } store) return AuthenticateResult.Fail("off");

        var session = await store.FindSessionAsync(AppTokens.Hash(token), Context.RequestAborted);
        if (session is null) return AuthenticateResult.Fail("unknown");
        if (AppPeople.ByName(session.Person, app) is not { } person) return AuthenticateResult.Fail("removed");

        var now = clock.GetCurrentInstant();
        if (session.LastSeenAt is not { } seen || seen <= now - AppAuth.TouchEvery)
        {
            await store.TouchAsync(session.Id, now, Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(AppAuth.PersonClaim, person.Name),
            new Claim(AppAuth.RoleClaim, person.Role.ToString()),
            new Claim(AppAuth.SessionClaim, session.Id.ToString())
        ], AppAuth.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AppAuth.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        if (Request.Headers.Authorization.ToString().StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        }

        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}

/// <summary>The Google side of the app's sign-in: a code for a known person, an error for anyone else.</summary>
public sealed class AppSignInHandoff(IAppAccessStore store, IClock clock, AdminOptions admin, AppOptions app) : IAppSignInHandoff
{
    public async Task<string> CompleteAsync(string? email, string challenge, string state, CancellationToken cancellationToken)
    {
        var target = $"{app.Scheme}://signin?state={Uri.EscapeDataString(state)}";
        if (AppPeople.Resolve(email, admin.AllowedEmails, app) is not { } person) return target + "&error=not-allowed";

        var code = AppTokens.NewCode();
        await store.AddCodeAsync(AppTokens.Hash(code), challenge, person.Name, clock.GetCurrentInstant() + AppSignIn.CodeLifetime, cancellationToken);
        return target + "&code=" + Uri.EscapeDataString(code);
    }
}
