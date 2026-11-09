using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
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
    /// Messages per tick. Bounded so one backlog cannot monopolize the provider
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
            // Oldest first, and ordered explicitly: a Take without an OrderBy
            // leaves it to the planner which rows come back, so a message could
            // in principle sit unreconciled while newer ones are picked instead.
            .OrderBy(message => message.SentAt)
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
        var invites = scope.ServiceProvider.GetRequiredService<ICalendarInvites>();
        var options = scope.ServiceProvider.GetRequiredService<SchedulingOptions>();

        var now = clock.GetCurrentInstant();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // FOR UPDATE SKIP LOCKED is the point of the raw SQL: EF cannot express
        // it, and without it two replicas either send the same message twice or
        // serialize behind each other.
        var claimed = await database.Outbox
            .FromSql(
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

            Appointment? appointment = null;
            SendResult result;
            try
            {
                if (message.Kind.IsCalendar())
                {
                    appointment = await database.Appointments
                        .FirstOrDefaultAsync(candidate => candidate.Id == message.AppointmentId, cancellationToken);

                    result = await SendCalendarAsync(message, appointment, invites, cancellationToken);
                }
                else
                {
                    result = await sender.SendAsync(message, cancellationToken);
                }
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

                if (message.Kind == NotificationKind.CalendarInvite && appointment is not null)
                {
                    appointment.GoogleEventId = result.ProviderMessageId;
                }
            }
            else if (result.Permanent)
            {
                // An opt-out is worth remembering rather than rediscovering.
                // Without this every future booking queues messages for a number
                // the carrier will refuse, each failing and dead lettering, and
                // each spending part of a daily allowance.
                if (result.OptedOut)
                {
                    await RecordOptOutAsync(database, message.ToPhoneE164, result.Error, now, cancellationToken);
                }

                // No further attempts. Waiting cannot make an opted-out number
                // or a landline deliverable, and four more tries would spend the
                // daily allowance, delay the messages that could have gone, and
                // bury the real reason under identical failures.
                message.State = DeliveryState.DeadLettered;
                message.NextAttemptAt = null;
                message.SentAt = null;
                message.LastError = result.Error;

                logger.LogError(
                    "Message {MessageId} of kind {Kind} will not be retried: {Error}",
                    message.Id,
                    message.Kind,
                    result.Error);

                TellHostIfInviteFailed(database, message, appointment, options, now);
            }
            else
            {
                Fail(message, result.Error ?? "The provider rejected the message.", now);

                if (message.State == DeliveryState.DeadLettered)
                {
                    TellHostIfInviteFailed(database, message, appointment, options, now);
                }
            }
        }

        if (claimed.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>The reason a calendar job is skipped rather than sent.</summary>
    internal const string CanceledFirst = "The booking was canceled before the invite went out.";

    /// <summary>Hands a calendar job to Google, or says why it cannot go.</summary>
    private static Task<SendResult> SendCalendarAsync(
        OutboxMessage message,
        Appointment? appointment,
        ICalendarInvites invites,
        CancellationToken cancellationToken)
    {
        if (appointment is null)
        {
            return Task.FromResult(SendResult.RejectedPermanently("The booking no longer exists."));
        }

        if (message.Kind == NotificationKind.CalendarInvite && appointment.Cancelled)
        {
            // An invite for a meeting that is not happening is worse than none.
            return Task.FromResult(SendResult.RejectedPermanently(CanceledFirst));
        }

        return message.Kind == NotificationKind.CalendarInvite
            ? invites.InviteAsync(appointment, cancellationToken)
            : invites.WithdrawAsync(appointment, cancellationToken);
    }

    /// <summary>
    /// Texts the host when a booking's invite is out of attempts.
    /// </summary>
    /// <remarks>
    /// The visitor was told an invite is on its way, so the host has to know
    /// it is not. Queued as an ordinary text in the same save, so it gets the
    /// texts' own retries. Nothing is sent for a booking canceled first, or
    /// when no host number is configured.
    /// </remarks>
    private static void TellHostIfInviteFailed(
        SchedulingDbContext database,
        OutboxMessage message,
        Appointment? appointment,
        SchedulingOptions options,
        Instant now)
    {
        if (message.Kind != NotificationKind.CalendarInvite
            || appointment is null
            || appointment.Cancelled
            || !PhoneNumber.TryParse(options.HostPhoneE164, out var hostPhone))
        {
            return;
        }

        database.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Kind = NotificationKind.HostInviteFailed,
            ToPhoneE164 = hostPhone.Value.E164,
            Body = MessageComposer.Compose(
                NotificationKind.HostInviteFailed,
                options.HostName,
                appointment.StartsAt,
                options.HostZone,
                options.BrandName),
            State = DeliveryState.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
            IdempotencyKey = $"{appointment.Id}:{NotificationKind.HostInviteFailed}"
        });
    }

    /// <summary>Remembers that a number asked not to be messaged.</summary>
    private static async Task RecordOptOutAsync(
        SchedulingDbContext database,
        string phoneE164,
        string? reason,
        Instant now,
        CancellationToken cancellationToken)
    {
        if (await database.SmsOptOuts.AnyAsync(optOut => optOut.PhoneE164 == phoneE164, cancellationToken))
        {
            return;
        }

        database.SmsOptOuts.Add(new SmsOptOut
        {
            Id = Guid.NewGuid(),
            PhoneE164 = phoneE164,
            OptedOutAt = now,
            Reason = reason ?? "The provider reported an opt-out."
        });
    }

    /// <summary>
    /// Records a failed attempt and decides whether there is another one.
    /// </summary>
    private void Fail(OutboxMessage message, string error, Instant now)
    {
        message.LastError = error;
        message.SentAt = null;

        var next = DeliveryPolicy.NextAttemptAt(message.Attempts, now, message.Kind);

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
