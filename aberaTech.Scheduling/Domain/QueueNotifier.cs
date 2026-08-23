using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Outbox;
using NodaTime;

namespace aberaTech.Scheduling.Domain;

/// <summary>
/// Recomputes the queue and queues whatever messages that change has earned.
/// </summary>
/// <remarks>
/// Called after every change to a queue — a join, a cancellation, an
/// appointment starting or finishing — because in a queue those are all the
/// same event: the line moved, and some subset of the people in it now believe
/// something that is no longer true.
///
/// Nothing here talks to a provider. It writes rows to the outbox, and the
/// caller commits them in the same transaction as the change itself. That is
/// what makes "we decided to tell them" exactly as durable as "it happened",
/// and it is why a crash between the two is not a thing that can occur.
/// </remarks>
public sealed class QueueNotifier(SchedulingDbContext database, IClock clock, SchedulingOptions options)
{
    /// <summary>
    /// Brings notifications for one session up to date.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for saving. Returning without saving is
    /// deliberate: the point is that these rows land in the same commit as
    /// whatever caused them.
    /// </remarks>
    public void Reconcile(QueueSession session)
    {
        var now = clock.GetCurrentInstant();
        var projection = QueueProjection.Project(session.Entries.Select(entry => entry.ToDomain()), now);

        if (projection.Count == 0)
        {
            return;
        }

        var frontId = projection[0].Id;
        var byId = session.Entries.ToDictionary(entry => entry.Id);

        foreach (var projected in projection)
        {
            if (!byId.TryGetValue(projected.Id, out var record))
            {
                continue;
            }

            // Somebody who declined texts still holds their place and still sees
            // it move on the page; they simply are not messaged about it.
            if (!record.SmsConsent || string.IsNullOrEmpty(record.PhoneE164))
            {
                continue;
            }

            var due = NotificationPolicy.Decide(
                record.ToNotificationState(),
                projected.ProjectedStart,
                now,
                isFront: projected.Id == frontId);

            foreach (var kind in due)
            {
                Enqueue(record, kind, projected.ProjectedStart, now);
            }
        }
    }

    private void Enqueue(
        QueueEntryRecord record,
        NotificationKind kind,
        Instant projectedStart,
        Instant now)
    {
        var hostZone = options.HostZone;
        // The visitor's own zone, falling back to the host's only if the browser
        // sent something unusable. Never the server's: a container runs in UTC,
        // and a message rendered in UTC is precisely the bug this project exists
        // to avoid.
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(record.ZoneId) ?? hostZone;

        database.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            QueueEntryId = record.Id,
            Kind = kind,
            ToPhoneE164 = record.PhoneE164,
            Body = MessageComposer.Compose(kind, options.HostName, projectedStart, zone),
            State = DeliveryState.Pending,
            Attempts = 0,
            CreatedAt = now,

            // Due immediately. The dispatcher owns every decision about when a
            // retry happens; the producer only says "this should go out".
            NextAttemptAt = now,

            // Scoped to the entry and the kind, so the same person cannot be
            // told the same kind of thing twice however many times reconcile
            // runs. The unique index on this column is what enforces it.
            IdempotencyKey = IdempotencyKeyFor(record.Id, kind, projectedStart)
        });

        // Recording what they were told, so the next reconcile compares against
        // the message rather than against the previous calculation.
        record.LastAnnouncedStart = projectedStart;
        record.ImminentSent |= kind == NotificationKind.Imminent;
        record.TurnSent |= kind == NotificationKind.YourTurn;
    }

    /// <summary>
    /// A key that is stable for one logical notification and different for the
    /// next one.
    /// </summary>
    /// <remarks>
    /// The milestone kinds are keyed on the entry alone: there is only ever one
    /// "you are up now" for one person, and a second is a bug however much the
    /// queue churns. A time change is keyed on the time as well, because a
    /// genuinely new estimate is genuinely a new thing to say.
    /// </remarks>
    internal static string IdempotencyKeyFor(Guid entryId, NotificationKind kind, Instant projectedStart) =>
        kind switch
        {
            NotificationKind.TimeChanged => $"{entryId}:{kind}:{projectedStart.ToUnixTimeSeconds()}",
            _ => $"{entryId}:{kind}"
        };
}
