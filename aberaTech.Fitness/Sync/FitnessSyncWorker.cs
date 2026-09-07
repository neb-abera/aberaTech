using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

/// <summary>
/// Brings the log in on its own: Hevy daily, Strava hourly, each only when
/// it is configured or connected.
/// </summary>
/// <remarks>
/// The same plain hosted service the outbox uses, for the same reason: exact
/// control over cadence and failure without a scheduler's surface to defend.
/// One tick a few minutes after boot, then every ten minutes, asking
/// <see cref="SyncSchedule"/> what is due. A source that fails is recorded and
/// waited on, not hammered.
/// </remarks>
public sealed class FitnessSyncWorker(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<FitnessSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan FirstTick = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Fitness sync worker started; first tick in {Delay}.", FirstTick);

        try
        {
            await Task.Delay(FirstTick, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Fitness sync tick failed. Continuing.");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        var now = clock.GetCurrentInstant();

        var states = await database.SyncStates.ToDictionaryAsync(s => s.Source, cancellationToken);

        if (scope.ServiceProvider.GetService<HevySync>() is { } hevy
            && SyncSchedule.IsDue(SyncSchedule.HevySource, states.GetValueOrDefault(SyncSchedule.HevySource)?.LastRunAt, now))
        {
            var outcome = await hevy.RunAsync(cancellationToken);
            logger.LogInformation("Hevy sync: {Fetched} fetched, {Added} new{Error}.",
                outcome.Fetched, outcome.Added, outcome.Error is null ? "" : $" ({outcome.Error})");
        }

        if (scope.ServiceProvider.GetService<StravaSync>() is { } strava
            && await database.StravaConnections.AnyAsync(cancellationToken)
            && SyncSchedule.IsDue(SyncSchedule.StravaSource, states.GetValueOrDefault(SyncSchedule.StravaSource)?.LastRunAt, now))
        {
            var outcome = await strava.RunAsync(cancellationToken);
            logger.LogInformation("Strava sync: {Fetched} seen, {Added} new{Error}.",
                outcome.Fetched, outcome.Added, outcome.Error is null ? "" : $" ({outcome.Error})");
        }
    }
}
