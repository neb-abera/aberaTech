using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

/// <summary>
/// Brings the log in on its own: Hevy daily, intervals.icu hourly, each only when
/// it is configured or connected.
/// </summary>
/// <remarks>
/// The same plain hosted service the outbox uses, for the same reason: exact
/// control over cadence and failure without a scheduler's surface to defend.
/// One tick a few minutes after boot, then every ten minutes, asking
/// <see cref="SyncSchedule"/> what is due. A source that fails is recorded and
/// retried on <see cref="SyncSchedule"/>'s terms, not hammered.
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

    /// <summary>One pass over both sources. Each runs on its own scope, so one failing cannot stop the other.</summary>
    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, SyncState> states;
        using (var scope = scopeFactory.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
            states = await database.SyncStates.AsNoTracking().ToDictionaryAsync(s => s.Source, cancellationToken);
        }

        var now = clock.GetCurrentInstant();
        bool Due(string source)
        {
            var state = states.GetValueOrDefault(source);
            return SyncSchedule.IsDue(source, state?.LastRunAt, state?.LastSuccessAt, now);
        }

        if (Due(SyncSchedule.HevySource))
        {
            await IsolatedAsync(SyncSchedule.HevySource, async services =>
            {
                if (services.GetService<HevySync>() is not { } hevy) return;
                var outcome = await hevy.RunAsync(cancellationToken);
                logger.LogInformation("Hevy sync: {Fetched} fetched, {Added} new{Error}.",
                    outcome.Fetched, outcome.Added, outcome.Error is null ? "" : $" ({outcome.Error})");
            }, cancellationToken);
        }

        if (Due(SyncSchedule.IntervalsIcuSource))
        {
            await IsolatedAsync(SyncSchedule.IntervalsIcuSource, async services =>
            {
                if (services.GetService<IntervalsIcuSync>() is not { } icu) return;
                var outcome = await icu.RunAsync(cancellationToken);
                logger.LogInformation("intervals.icu sync: {Fetched} seen, {Added} new{Error}.",
                    outcome.Fetched, outcome.Added, outcome.Error is null ? "" : $" ({outcome.Error})");
            }, cancellationToken);
        }
    }

    private async Task IsolatedAsync(string source, Func<IServiceProvider, Task> run, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await run(scope.ServiceProvider);
        }
        catch (Exception exception) when (SyncFailure.Counts(exception, cancellationToken))
        {
            logger.LogError(exception, "{Source} sync could not record its run. The other source still runs.", source);
        }
    }
}
