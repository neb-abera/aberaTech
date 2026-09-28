using System.Security.Claims;
using System.Text.Encodings.Web;
using aberaTech.Scheduling.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// Who may call /api/alerts: the owner's cookie everywhere, and a paired
/// phone's token on the routes the phone needs. Every /api/alerts route
/// names one of the two policies, and RouteTableTests holds the split.
/// </summary>
/// <remarks>
/// Both policies authenticate both schemes. A request with a valid token
/// is then a signed-in device, and a route it may not use answers 403
/// rather than 401: the phone learns it is paired and refused, rather than
/// that it should pair again.
/// </remarks>
public static class AlertsAuth
{
    public const string DeviceScheme = "alerts-device";

    /// <summary>Settings, event types, tests, pairing: the owner's cookie alone.</summary>
    public const string OwnerPolicy = "alerts-owner";

    /// <summary>Status, mute, unmute, skip, unskip and ack: the owner's cookie or a paired phone.</summary>
    public const string OwnerOrDevicePolicy = "alerts-owner-or-device";

    /// <summary>The paired phone's id, on a principal only the device scheme issues.</summary>
    public const string DeviceClaim = "aberatech:alerts-device";

    /// <summary>LastSeenAt is written at most this often per phone.</summary>
    public static readonly Duration TouchEvery = Duration.FromMinutes(1);

    public static IServiceCollection AddAlertsDeviceAuth(this IServiceCollection services, AdminOptions admin)
    {
        services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AlertDeviceAuthenticationHandler>(DeviceScheme, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(OwnerPolicy, policy => policy
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, DeviceScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => IsOwner(context.User, admin)))
            .AddPolicy(OwnerOrDevicePolicy, policy => policy
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, DeviceScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => IsOwner(context.User, admin) || IsDevice(context.User)));

        return services;
    }

    /// <summary>A paired phone: an identity the device scheme authenticated, carrying the phone's id.</summary>
    public static bool IsDevice(ClaimsPrincipal user) =>
        user.Identities.Any(identity => identity.IsAuthenticated
                                        && identity.AuthenticationType == DeviceScheme
                                        && identity.HasClaim(claim => claim.Type == DeviceClaim));

    /// <summary>
    /// The owner's cookie, checked as the admin policy checks it: the Google
    /// address on the allowlist. The device scheme issues no address, so a
    /// token alone never passes.
    /// </summary>
    private static bool IsOwner(ClaimsPrincipal user, AdminOptions admin) =>
        user.Identities.Any(identity => identity.IsAuthenticated
                                        && identity.AuthenticationType != DeviceScheme
                                        && admin.Allows(identity.FindFirst(ClaimTypes.Email)?.Value));
}

/// <summary>
/// <c>Authorization: Bearer aat_…</c>. A request without the header is no
/// result, so the cookie decides. A malformed token is refused before it is
/// hashed. A well-formed one is looked up by its SHA-256, so a revoked phone
/// is refused on its next request.
/// </summary>
/// <remarks>
/// A failure carries no token material anywhere: the reason is one of three
/// fixed words, and SecurityEvents logs the route and the address.
/// </remarks>
public sealed class AlertDeviceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IClock clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var token = header[Bearer.Length..].Trim();
        if (!AlertDeviceTokens.IsWellFormed(token)) return AuthenticateResult.Fail("malformed");

        if (Context.RequestServices.GetService<IAlertDeviceStore>() is not { } store) return AuthenticateResult.Fail("off");

        var device = await store.FindAsync(AlertDeviceTokens.Hash(token), Context.RequestAborted);
        if (device is null) return AuthenticateResult.Fail("unknown");

        var now = clock.GetCurrentInstant();
        if (device.LastSeenAt is not { } seen || seen <= now - AlertsAuth.TouchEvery)
        {
            await store.TouchAsync(device.Id, now, now - AlertsAuth.TouchEvery, Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
            [new Claim(AlertsAuth.DeviceClaim, device.Id.ToString())], AlertsAuth.DeviceScheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AlertsAuth.DeviceScheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        // Only to a caller that presented a token: a browser has no use for it.
        if (Request.Headers.Authorization.ToString().StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        }

        return Task.CompletedTask;
    }
}
