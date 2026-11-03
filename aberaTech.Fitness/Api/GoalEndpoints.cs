using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.OneDrive;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>The Goals tab's routes: the goals themselves, and the OneDrive grant they are read with.</summary>
public static class GoalEndpoints
{
    /// <summary>The purpose string for the OAuth state, which carries the PKCE verifier sealed.</summary>
    internal const string StatePurpose = "aberaTech.Fitness.OneDriveOAuthState";

    /// <summary>A consent round trip older than this is somebody replaying a link.</summary>
    internal static readonly Duration StateLifetime = Duration.FromMinutes(10);

    internal const string CallbackPath = "/api/fitness/onedrive/callback";

    public static RouteGroupBuilder MapGoalEndpoints(this RouteGroupBuilder api, OneDriveOptions oneDrive)
    {
        // Not output-cached: the goals live in OneDrive, and a write there
        // evicts nothing here. The ETag check keeps a repeat read to one
        // metadata request.
        api.MapGet("/goals", async (
            FitnessDbContext database,
            IWorkbookSource source,
            IClock clock,
            double? weeklyHours,
            double? compliance,
            CancellationToken cancellationToken) =>
        {
            if (weeklyHours is < 0 or > 40) return Results.Text("weeklyHours 0-40.", "text/plain", statusCode: StatusCodes.Status400BadRequest);
            if (compliance is < 0 or > 1) return Results.Text("compliance 0-1.", "text/plain", statusCode: StatusCodes.Status400BadRequest);

            return Results.Ok(await GoalReports.BuildAsync(
                database, source, weeklyHours, compliance ?? 1.0, clock.GetCurrentInstant(), cancellationToken));
        });

        if (!oneDrive.IsConfigured) return api;

        // The grant: owner-only like every route here, so a stranger can
        // neither start a connection nor finish one with their own code.
        api.MapGet("/onedrive/connect", (HttpContext context, IDataProtectionProvider protection, IClock clock) =>
        {
            var verifier = OneDriveClient.NewVerifier();
            var minted = clock.GetCurrentInstant().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var state = protection.CreateProtector(StatePurpose).Protect($"{minted}|{verifier}");

            return Results.Redirect(OneDriveClient.AuthorizeRedirect(
                oneDrive.ClientId, CallbackUri(context), state, OneDriveClient.Challenge(verifier)));
        });

        api.MapGet("/onedrive/callback", async (
            HttpContext context,
            string? code,
            string? state,
            string? error,
            OneDriveClient client,
            FitnessDbContext database,
            WorkbookCache cache,
            IDataProtectionProvider protection,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (error is not null || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Redirect("/fitness?onedrive=refused");
            }

            string verifier;
            try
            {
                var parts = protection.CreateProtector(StatePurpose).Unprotect(state).Split('|', 2);
                var minted = Instant.FromUnixTimeSeconds(long.Parse(parts[0], CultureInfo.InvariantCulture));
                if (clock.GetCurrentInstant() - minted > StateLifetime) return Results.Redirect("/fitness?onedrive=expired");
                verifier = parts[1];
            }
            catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException or IndexOutOfRangeException or OverflowException)
            {
                return Results.Redirect("/fitness?onedrive=refused");
            }

            OneDriveTokens tokens;
            try
            {
                tokens = await client.ExchangeCodeAsync(code, CallbackUri(context), verifier, cancellationToken);
            }
            catch (OneDriveException)
            {
                return Results.Redirect("/fitness?onedrive=failed");
            }

            var connection = await database.OneDriveConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);
            if (connection is null)
            {
                connection = new OneDriveConnection { Id = 1, ProtectedRefreshToken = "" };
                database.OneDriveConnections.Add(connection);
            }

            connection.ProtectedRefreshToken = protection.CreateProtector(WorkbookSource.ProtectorPurpose).Protect(tokens.RefreshToken);
            connection.ConnectedAt = clock.GetCurrentInstant();
            await database.SaveChangesAsync(cancellationToken);

            await cache.Gate.WaitAsync(cancellationToken);
            try
            {
                cache.Forget();
                cache.AccessToken = tokens.AccessToken;
                cache.AccessTokenExpires = clock.GetCurrentInstant() + Duration.FromSeconds(tokens.ExpiresInSeconds);
            }
            finally
            {
                cache.Gate.Release();
            }

            return Results.Redirect("/fitness?onedrive=connected");
        })
        // Microsoft's redirect back is a cross-site navigation, and the owner
        // cookie is SameSite=Strict, so the browser leaves it off. Owner-only
        // here answered 401 with an empty body, which iPhone Safari offered as
        // a file named "callback". The state carries the proof instead: only
        // the owner-only connect route mints it, Data Protection seals it, it
        // expires, and it holds the PKCE verifier the code is bound to.
        .AllowAnonymous();

        api.MapPost("/onedrive/disconnect", async (FitnessDbContext database, WorkbookCache cache, CancellationToken cancellationToken) =>
        {
            await database.OneDriveConnections.Where(c => c.Id == 1).ExecuteDeleteAsync(cancellationToken);

            await cache.Gate.WaitAsync(cancellationToken);
            try
            {
                cache.Forget();
            }
            finally
            {
                cache.Gate.Release();
            }

            return Results.NoContent();
        });

        return api;
    }

    /// <summary>The callback, on whatever host this request arrived at; registered on the app as-is.</summary>
    internal static string CallbackUri(HttpContext context) =>
        $"{context.Request.Scheme}://{context.Request.Host}{CallbackPath}";
}
