using System.Security.Claims;
using System.Text.Json;
using aberaTech.Scheduling.Admin;
using aberaTech.Scheduling.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Calendar;

/// <summary>Connecting and disconnecting the host's Google calendar.</summary>
/// <remarks>
/// Separate from admin sign-in because the two want different things from
/// Google. Signing in wants an identity and nothing more, so it asks for the
/// narrowest scopes and keeps no tokens. Connecting a calendar needs offline
/// access and a refresh token that outlives the session, which is a materially
/// larger grant and should be an explicit, separate act rather than something
/// that happens quietly the first time somebody signs in.
/// </remarks>
public static class CalendarAdminEndpoints
{
    private const string ReadOnlyScope = "https://www.googleapis.com/auth/calendar.readonly";

    public static IEndpointRouteBuilder MapCalendarAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/scheduling/admin/calendar")
            .RequireAuthorization(AdminAuth.PolicyName);

        group.MapGet("/", StatusAsync);
        group.MapPost("/disconnect", DisconnectAsync);

        // The consent round trip. A GET because it is a redirect the browser
        // follows, not an API call.
        group.MapGet("/connect", (HttpContext context) =>
        {
            var properties = new AuthenticationProperties { RedirectUri = "/schedule/admin" };

            // Offline access is what yields a refresh token; without it the
            // grant dies with the browser session and free/busy stops working
            // the moment the host closes the tab. The consent prompt is forced
            // because Google returns a refresh token only on the first consent,
            // so a reconnect after a revoke would otherwise come back without
            // one and appear to succeed while being useless.
            properties.SetParameter("access_type", "offline");
            properties.SetParameter("prompt", "consent");

            return Results.Challenge(properties, ["Google"]);
        });

        return routes;
    }

    private static async Task<IResult> StatusAsync(
        SchedulingDbContext database,
        CancellationToken cancellationToken)
    {
        var credential = await database.HostCalendarCredentials.FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new
        {
            connected = credential is not null,
            email = credential?.ConnectedEmail,
            calendarId = credential?.CalendarId
        });
    }

    private static async Task<IResult> DisconnectAsync(
        SchedulingDbContext database,
        CancellationToken cancellationToken)
    {
        var credentials = await database.HostCalendarCredentials.ToListAsync(cancellationToken);

        database.HostCalendarCredentials.RemoveRange(credentials);
        await database.SaveChangesAsync(cancellationToken);

        // Removing the row is enough for this application to stop reading the
        // calendar, but it does not revoke the grant at Google's end. The page
        // says so and links to the account permissions page, because a stored
        // token being gone and a grant being withdrawn are different facts and
        // conflating them would overstate what this button did.
        return Results.NoContent();
    }

    /// <summary>
    /// Captures a refresh token from a completed Google sign-in, if one came back.
    /// </summary>
    /// <remarks>
    /// Called from the Google handler's OnCreatingTicket, which is the only
    /// point the refresh token exists: it is present exactly once, in the token
    /// response, and is never returned again.
    /// </remarks>
    public static async Task CaptureRefreshTokenAsync(
        SchedulingDbContext database,
        GoogleAccessTokens tokens,
        IClock clock,
        ClaimsPrincipal user,
        string? refreshToken,
        string? grantedScopes,
        CancellationToken cancellationToken)
    {
        // A plain sign-in has no refresh token and no calendar scope, and must
        // not be mistaken for connecting a calendar.
        if (string.IsNullOrEmpty(refreshToken)
            || grantedScopes is null
            || !grantedScopes.Contains(ReadOnlyScope, StringComparison.Ordinal))
        {
            return;
        }

        var existing = await database.HostCalendarCredentials.ToListAsync(cancellationToken);
        database.HostCalendarCredentials.RemoveRange(existing);

        database.HostCalendarCredentials.Add(new HostCalendarCredential
        {
            Id = Guid.NewGuid(),
            ProtectedRefreshToken = tokens.Protect(refreshToken),
            CalendarId = "primary",
            ConnectedEmail = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            ConnectedAt = clock.GetCurrentInstant()
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Reads the granted scopes out of Google's token response.</summary>
    internal static string? ReadScopes(JsonElement tokenResponse) =>
        tokenResponse.TryGetProperty("scope", out var scope) ? scope.GetString() : null;
}
