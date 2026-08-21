using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Outbox;

/// <summary>
/// Drains the outbox: sends what is due, retries what failed, and gives up
/// loudly rather than quietly.
/// </summary>
/// <remarks>
/// A plain hosted service over a Postgres queue rather than Hangfire or Quartz.
/// The scheduling library in those products buys cron expressions and a
/// dashboard; neither is wanted here, and the dashboard is a further
/// authenticated surface to defend. What is wanted is exact control over when a
/// message is retried and when it stops being retried, which is the whole
/// reason this project exists, so it is written out rather than configured.
///
/// Claiming uses SELECT ... FOR UPDATE SKIP LOCKED, the standard Postgres queue
/// pattern: each worker takes rows nobody else holds and never blocks behind
/// another worker's batch. That keeps a second replica correct for free, rather
/// than correct only because there happens to be one of them.
/// </remarks>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    /// <summary>
    /// How often to look for work. Fast enough that a "you are up next" message
    /// is not stale by the time it lands, slow enough to be invisible against a
    /// burstable database.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Messages per tick. Bounded so one backlog cannot monopolise the provider
    /// or hold a long transaction open across hundreds of HTTP calls.
    /// </summary>
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox dispatcher started, polling every {Interval}.", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAcceptedAsync(stoppingToken);
                await DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Never let one bad tick kill the loop: a dispatcher that exits
                // on an unexpected error is indistinguishable, from the outside,
                // from the silent non-delivery this whole design is meant to
                // prevent.
                logger.LogError(exception, "Outbox tick failed. Continuing.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Outbox dispatcher stopping.");
    }

    /// <summary>
    /// Moves messages the provider accepted but never confirmed back into the
    /// retry path.
    /// </summary>
    /// <remarks>
    /// This is the specific gap that makes a message vanish: the provider
    /// returns success, the receipt never arrives because a carrier filtered it,
    /// and a system that treats acceptance as delivery has already forgotten
    /// about it. Here, silence past the receipt window is a failure.
    /// </remarks>
    private async Task ReconcileAcceptedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        var now = clock.GetCurrentInstant();
        var cutoff = now - DeliveryPolicy.ReceiptWindow;

        var overdue = await database.Outbox
            .Where(message => message.State == DeliveryState.Sent && message.SentAt != null && message.SentAt <= cutoff)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in overdue)
        {
            Fail(message, $"No delivery receipt within {DeliveryPolicy.ReceiptWindow}.", now);

            logger.LogWarning(
                "Message {MessageId} was accepted but never confirmed; attempt {Attempts} of {Max}.",
                message.Id,
                message.Attempts,
                DeliveryPolicy.MaxAttempts);
        }

        if (overdue.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DispatchDueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        var now = clock.GetCurrentInstant();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // FOR UPDATE SKIP LOCKED is the point of the raw SQL: EF cannot express
        // it, and without it two replicas either send the same message twice or
        // serialise behind each other.
        var claimed = await database.Outbox
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "Outbox"
                 WHERE "State" IN ({(int)DeliveryState.Pending}, {(int)DeliveryState.Failed})
                   AND "NextAttemptAt" IS NOT NULL
                   AND "NextAttemptAt" <= {now}
                 ORDER BY "NextAttemptAt"
                 LIMIT {BatchSize}
                 FOR UPDATE SKIP LOCKED
                 """)
            .ToListAsync(cancellationToken);

        foreach (var message in claimed)
        {
            message.Attempts++;

            SendResult result;
            try
            {
                result = await sender.SendAsync(message, cancellationToken);
            }
            catch (Exception exception)
            {
                // A throwing provider is just another failure. Swallowing it
                // here keeps the rest of the batch moving.
                result = SendResult.Rejected(exception.Message);
            }

            if (result.Accepted)
            {
                // Sent means "handed over, awaiting a receipt", and the
                // reconciler will chase it. Delivered is terminal and only a
                // sender with no receipt channel may claim it directly.
                message.State = result.DeliveryConfirmed ? DeliveryState.Delivered : DeliveryState.Sent;
                message.SentAt = now;
                message.DeliveredAt = result.DeliveryConfirmed ? now : null;
                message.ProviderMessageId = result.ProviderMessageId;
                message.NextAttemptAt = null;
                message.LastError = null;
            }
            else
            {
                Fail(message, result.Error ?? "The provider rejected the message.", now);
            }
        }

        if (claimed.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Records a failed attempt and decides whether there is another one.
    /// </summary>
    private void Fail(OutboxMessage message, string error, Instant now)
    {
        message.LastError = error;
        message.SentAt = null;

        var next = DeliveryPolicy.NextAttemptAt(message.Attempts, now);

        if (next is null)
        {
            message.State = DeliveryState.DeadLettered;
            message.NextAttemptAt = null;

            // The loudest thing this service does, on purpose. A message that
            // has exhausted its retries is exactly the case that used to
            // disappear without trace, and the only useful response is for a
            // human to find out.
            logger.LogError(
                "Message {MessageId} of kind {Kind} dead lettered after {Attempts} attempts. Last error: {Error}",
                message.Id,
                message.Kind,
                message.Attempts,
                error);
            return;
        }

        message.State = DeliveryState.Failed;
        message.NextAttemptAt = next;
    }
}
