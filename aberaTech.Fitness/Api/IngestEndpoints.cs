using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Strava;
using aberaTech.Fitness.Sync;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>Where each automatic source stands.</summary>
public sealed record SourceStatusDto(bool Configured, bool Connected, string? LastRunAt, string? LastSyncedAt, string? LastOutcome);

public sealed record IngestStatusDto(SourceStatusDto Hevy, SourceStatusDto Strava);

/// <summary>
/// The automatic sources: their status, a manual run, and the Strava consent
/// round trip.
/// </summary>
public static class IngestEndpoints
{
    /// <summary>The purpose string for the OAuth state nonce.</summary>
    private const string StatePurpose = "aberaTech.Fitness.StravaOAuthState";

    /// <summary>A consent round trip older than this is somebody replaying a link.</summary>
    private static readonly Duration StateLifetime = Duration.FromMinutes(10);

    public static RouteGroupBuilder MapIngestEndpoints(this RouteGroupBuilder api, StravaOptions strava, bool hevyConfigured)
    {
        api.MapGet("/ingest", async (FitnessDbContext database, CancellationToken cancellationToken) =>
            Results.Ok(await StatusAsync(database, strava, hevyConfigured, cancellationToken)));

        if (hevyConfigured)
        {
            api.MapPost("/ingest/hevy/sync", async (HevySync sync, CancellationToken cancellationToken) =>
            {
                var outcome = await sync.RunAsync(cancellationToken);
                return outcome.Error is null
                    ? Results.Ok(new { fetched = outcome.Fetched, added = outcome.Added })
                    : Results.Text(outcome.Error, "text/plain", statusCode: StatusCodes.Status502BadGateway);
            });
        }

        if (!strava.IsConfigured) return api;

        // The consent round trip. A GET because it is a redirect the browser
        // follows. The state is a protected timestamp: the callback refuses a
        // state it did not mint or minted too long ago.
        api.MapGet("/ingest/strava/connect", (HttpContext context, IDataProtectionProvider protection, IClock clock) =>
        {
            var state = protection.CreateProtector(StatePurpose)
                .Protect(clock.GetCurrentInstant().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            return Results.Redirect(StravaClient.AuthorizeRedirect(strava.ClientId, CallbackUri(context), state));
        });

        api.MapGet("/ingest/strava/callback", async (
            HttpContext context,
            string? code,
            string? state,
            string? error,
            string? scope,
            StravaClient client,
            StravaSync sync,
            FitnessDbContext database,
            IDataProtectionProvider protection,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (error is not null || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Redirect("/fitness?tab=data&strava=refused");
            }

            long minted;
            try
            {
                minted = long.Parse(protection.CreateProtector(StatePurpose).Unprotect(state), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return Results.Redirect("/fitness?tab=data&strava=refused");
            }

            var now = clock.GetCurrentInstant();
            if (now - Instant.FromUnixTimeSeconds(minted) > StateLifetime)
            {
                return Results.Redirect("/fitness?tab=data&strava=expired");
            }

            // Strava lets the athlete untick the scope on the consent page; a
            // grant without it would connect and then read nothing.
            if (scope is not null && !scope.Contains(StravaClient.Scope, StringComparison.Ordinal))
            {
                return Results.Redirect("/fitness?tab=data&strava=scope");
            }

            var tokens = await client.ExchangeCodeAsync(code, cancellationToken);
            if (tokens is null)
            {
                return Results.Redirect("/fitness?tab=data&strava=failed");
            }

            var connection = await database.StravaConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);
            if (connection is null)
            {
                connection = new StravaConnection { Id = 1, ProtectedRefreshToken = "" };
                database.StravaConnections.Add(connection);
            }

            connection.AthleteId = tokens.AthleteId;
            connection.ProtectedRefreshToken = sync.Protect(tokens.RefreshToken);
            connection.ConnectedAt = now;
            connection.LastSyncedAt = null;
            connection.LastError = null;

            // The worker would pick it up within the hour; connecting is the
            // one moment the athlete is watching, so run now.
            var state0 = await database.SyncStates.SingleOrDefaultAsync(s => s.Source == SyncSchedule.StravaSource, cancellationToken);
            if (state0 is not null) state0.LastRunAt = null;
            await database.SaveChangesAsync(cancellationToken);

            await sync.RunAsync(cancellationToken);

            return Results.Redirect("/fitness?tab=data&strava=connected");
        });

        api.MapPost("/ingest/strava/sync", async (StravaSync sync, CancellationToken cancellationToken) =>
        {
            var outcome = await sync.RunAsync(cancellationToken);
            return outcome.Error is null
                ? Results.Ok(new { fetched = outcome.Fetched, added = outcome.Added })
                : Results.Text(outcome.Error, "text/plain", statusCode: StatusCodes.Status502BadGateway);
        });

        api.MapPost("/ingest/strava/disconnect", async (
            StravaClient client,
            FitnessDbContext database,
            IDataProtectionProvider protection,
            CancellationToken cancellationToken) =>
        {
            var connection = await database.StravaConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);
            if (connection is null) return Results.NoContent();

            try
            {
                var refresh = protection.CreateProtector(StravaSync.ProtectionPurpose).Unprotect(connection.ProtectedRefreshToken);
                var tokens = await client.RefreshAsync(refresh, cancellationToken);
                if (tokens is not null) await client.RevokeAsync(tokens.AccessToken, cancellationToken);
            }
            catch (Exception)
            {
                // The grant may already be dead on Strava's side; the row goes
                // either way, and the activities already imported stay.
            }

            database.StravaConnections.Remove(connection);
            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return api;
    }

    internal static async Task<IngestStatusDto> StatusAsync(
        FitnessDbContext database, StravaOptions strava, bool hevyConfigured, CancellationToken cancellationToken)
    {
        var states = await database.SyncStates.ToDictionaryAsync(s => s.Source, cancellationToken);
        var connection = await database.StravaConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);

        var hevyState = states.GetValueOrDefault(SyncSchedule.HevySource);
        var stravaState = states.GetValueOrDefault(SyncSchedule.StravaSource);

        return new IngestStatusDto(
            new SourceStatusDto(hevyConfigured, hevyConfigured, Iso(hevyState?.LastRunAt), Iso(hevyState?.LastRunAt), hevyState?.LastOutcome),
            new SourceStatusDto(
                strava.IsConfigured,
                connection is not null,
                Iso(stravaState?.LastRunAt),
                Iso(connection?.LastSyncedAt),
                connection?.LastError ?? stravaState?.LastOutcome));
    }

    /// <summary>The callback, on whatever host this request arrived at — registered on the Strava app as-is.</summary>
    internal static string CallbackUri(HttpContext context) =>
        $"{context.Request.Scheme}://{context.Request.Host}/api/fitness/ingest/strava/callback";

    private static string? Iso(Instant? instant) => instant?.ToString("uuuu-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
