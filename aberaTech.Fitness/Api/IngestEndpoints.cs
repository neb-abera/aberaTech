using System.Globalization;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.IntervalsIcu;
using aberaTech.Fitness.Sync;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>Where each automatic source stands.</summary>
public sealed record SourceStatusDto(bool Configured, bool Connected, string? LastRunAt, string? LastSyncedAt, string? LastOutcome);

public sealed record IngestStatusDto(SourceStatusDto Hevy, SourceStatusDto IntervalsIcu);

/// <summary>The automatic sources: their status and a manual run of each.</summary>
public static class IngestEndpoints
{
    public static RouteGroupBuilder MapIngestEndpoints(this RouteGroupBuilder api, IntervalsIcuOptions intervalsIcu, bool hevyConfigured)
    {
        api.MapGet("/ingest", async (FitnessDbContext database, CancellationToken cancellationToken) =>
            Results.Ok(await StatusAsync(database, intervalsIcu.IsConfigured, hevyConfigured, cancellationToken)));

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

        if (intervalsIcu.IsConfigured)
        {
            api.MapPost("/ingest/intervals-icu/sync", async (IntervalsIcuSync sync, CancellationToken cancellationToken) =>
            {
                var outcome = await sync.RunAsync(cancellationToken);
                return outcome.Error is null
                    ? Results.Ok(new { fetched = outcome.Fetched, added = outcome.Added })
                    : Results.Text(outcome.Error, "text/plain", statusCode: StatusCodes.Status502BadGateway);
            });
        }

        return api;
    }

    internal static async Task<IngestStatusDto> StatusAsync(
        FitnessDbContext database, bool intervalsIcuConfigured, bool hevyConfigured, CancellationToken cancellationToken)
    {
        var states = await database.SyncStates.ToDictionaryAsync(s => s.Source, cancellationToken);
        var hevy = states.GetValueOrDefault(SyncSchedule.HevySource);
        var icu = states.GetValueOrDefault(SyncSchedule.IntervalsIcuSource);

        return new IngestStatusDto(
            new SourceStatusDto(hevyConfigured, hevyConfigured, Iso(hevy?.LastRunAt), Iso(hevy?.LastRunAt), hevy?.LastOutcome),
            new SourceStatusDto(intervalsIcuConfigured, intervalsIcuConfigured, Iso(icu?.LastRunAt), Iso(icu?.LastRunAt), icu?.LastOutcome));
    }

    private static string? Iso(Instant? instant) => instant?.ToString("uuuu-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
