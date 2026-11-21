using aberaTech.Scheduling.AppAccess;
using Microsoft.AspNetCore.Authentication;
using NodaTime;

namespace aberaTech.Server.App;

/// <summary>The app's sign-in: Google in a browser sheet, then a one-time code traded for a token.</summary>
/// <remarks>
/// <list type="number">
/// <item>The app opens <c>GET /api/app/sign-in?state=…&amp;challenge=…</c> in an
/// ASWebAuthenticationSession. The challenge is PKCE S256 of a verifier
/// only the app holds.</item>
/// <item>The server sends it to Google, with the site's own OAuth client.</item>
/// <item>Google's answer comes back to /signin-google. The handoff
/// (<see cref="AppSignInHandoff"/>) checks the address against the owner and
/// the household, stores a code good for 2 minutes, and sends the sheet to
/// <c>aberatech://signin?code=…&amp;state=…</c>. No cookie is set.</item>
/// <item>The app posts the code and the verifier to <c>/api/app/session</c>
/// and gets its token. Another app that registered the same scheme and
/// caught the redirect has the code but not the verifier.</item>
/// </list>
/// </remarks>
public static class AppSignIn
{
    public static readonly Duration CodeLifetime = Duration.FromMinutes(2);

    public const int MaxDeviceLength = 60;

    public const string RateLimitPolicy = "app-sign-in";

    public sealed record SessionRequest(string? Code, string? Verifier, string? Device);

    public sealed record SessionResponse(string Token, string Name, string Role);

    public sealed record MeResponse(string Name, string Role);

    public static IEndpointRouteBuilder MapAppSignInEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/app/sign-in", (string? state, string? challenge) =>
            {
                if (!AppTokens.IsPkceValue(challenge) || string.IsNullOrEmpty(state) || state.Length > 128)
                {
                    return Results.BadRequest();
                }

                var properties = new AuthenticationProperties { RedirectUri = "/" };
                properties.Items[IAppSignInHandoff.ChallengeItem] = challenge;
                properties.Items["app.state"] = state;
                return Results.Challenge(properties, ["Google"]);
            })
            .RequireRateLimiting(RateLimitPolicy)
            .AllowAnonymous();

        routes.MapPost("/api/app/session", async (
                SessionRequest request, IAppAccessStore store, IClock clock, AppOptions app, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrEmpty(request.Code) || request.Code.Length > 64 || !AppTokens.IsPkceValue(request.Verifier))
                {
                    return Results.BadRequest();
                }

                var now = clock.GetCurrentInstant();
                var redeemed = await store.RedeemCodeAsync(AppTokens.Hash(request.Code), now, cancellationToken);
                if (redeemed is not { } code
                    || !string.Equals(code.Challenge, AppTokens.Challenge(request.Verifier!), StringComparison.Ordinal)
                    || AppPeople.ByName(code.Person, app) is not { } person)
                {
                    return Results.Unauthorized();
                }

                var device = Clean(request.Device) ?? "iPhone";
                var token = AppTokens.New();
                await store.AddSessionAsync(Guid.NewGuid(), AppTokens.Hash(token), person.Name, device, now, cancellationToken);
                return Results.Ok(new SessionResponse(token, person.Name, person.Role.ToString().ToLowerInvariant()));
            })
            .RequireRateLimiting(RateLimitPolicy)
            .AllowAnonymous();

        routes.MapGet("/api/app/me", (HttpContext context) =>
                AppAuth.Person(context.User) is { } person
                    ? Results.Ok(new MeResponse(person.Name, person.Role.ToString().ToLowerInvariant()))
                    : Results.Unauthorized())
            .RequireAuthorization(AppAuth.SignedInPolicy);

        routes.MapDelete("/api/app/session", async (HttpContext context, IAppAccessStore store, CancellationToken cancellationToken) =>
            {
                if (AppAuth.Session(context.User) is { } id) await store.RevokeAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(AppAuth.SignedInPolicy);

        return routes;
    }

    private static string? Clean(string? device)
    {
        var text = device?.Trim();
        return text is { Length: > 0 and <= MaxDeviceLength } && !text.Any(char.IsControl) ? text : null;
    }
}
