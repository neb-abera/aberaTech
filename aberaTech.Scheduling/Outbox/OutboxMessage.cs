using aberaTech.Scheduling.Domain;
using NodaTime;

namespace aberaTech.Scheduling.Outbox;

/// <summary>
/// One outbound message, written in the same transaction as the change that
/// caused it.
/// </summary>
/// <remarks>
/// This is the transactional outbox, and it exists because the alternative —
/// calling the SMS provider inline while handling the request — loses the
/// message whenever the process dies, the provider is briefly unreachable, or
/// the surrounding transaction rolls back after the text has already gone out.
/// Writing the intent to the same database in the same transaction makes the
/// decision to notify exactly as durable as the booking that triggered it.
/// </remarks>
public class OutboxMessage
{
    public Guid Id { get; set; }

    public Guid? QueueEntryId { get; set; }

    /// <summary>
    /// Set for messages about a booked appointment, as QueueEntryId is for
    /// messages about a place in the queue. Exactly one of the two is set.
    /// </summary>
    public Guid? AppointmentId { get; set; }

    public NotificationKind Kind { get; set; }

    public string ToPhoneE164 { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DeliveryState State { get; set; } = DeliveryState.Pending;

    public int Attempts { get; set; }

    public Instant CreatedAt { get; set; }

    /// <summary>Null once the message reaches a terminal state.</summary>
    public Instant? NextAttemptAt { get; set; }

    public Instant? SentAt { get; set; }

    public Instant? DeliveredAt { get; set; }

    /// <summary>The provider's id, used to match delivery receipts back to this row.</summary>
    public string? ProviderMessageId { get; set; }

    public string? LastError { get; set; }

    /// <summary>
    /// Sent with the message so a retry the provider already accepted cannot
    /// produce a second text on somebody's phone.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
