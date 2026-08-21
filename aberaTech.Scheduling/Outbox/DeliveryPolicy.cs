using NodaTime;

namespace aberaTech.Scheduling.Outbox;

/// <summary>Where a queued message has got to.</summary>
/// <remarks>
/// <see cref="Sent"/> is not success. The provider accepting a message over
/// HTTP says only that it has been handed to a carrier, and the gap between
/// that and a handset is where messages disappear: an unregistered 10DLC
/// campaign, a filtered number, a disconnected line. Only a delivery receipt
/// moves a message to <see cref="Delivered"/>, and anything still sitting in
/// <see cref="Sent"/> when its receipt window closes is treated as a failure
/// and retried.
/// </remarks>
public enum DeliveryState
{
    Pending = 0,
    Sent = 1,
    Delivered = 2,
    Failed = 3,

    /// <summary>Out of attempts. Needs a human, and says so.</summary>
    DeadLettered = 4
}

/// <summary>
/// When to try again, how often, and when to stop and shout.
/// </summary>
/// <remarks>
/// Pure arithmetic, kept apart from the dispatcher so the schedule can be
/// asserted directly. The behaviour it encodes is the one the paid tools did
/// not have: a message that fails is retried on a widening interval, a message
/// that is accepted but never confirmed is retried too, and a message that
/// exhausts its attempts is parked in a state that a human is told about rather
/// than dropped.
/// </remarks>
public static class DeliveryPolicy
{
    /// <summary>
    /// Attempts before a message is dead lettered. Five attempts across the
    /// backoff below spans a bit over half an hour, which is the useful life of
    /// a message about an appointment that is minutes away. Retrying beyond
    /// that delivers an alert about a slot that has already come and gone.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// How long to wait for a delivery receipt before assuming the message is
    /// lost. Carriers normally confirm in seconds; five minutes is generous
    /// enough that a slow receipt is not mistaken for a failure.
    /// </summary>
    public static readonly Duration ReceiptWindow = Duration.FromMinutes(5);

    /// <summary>
    /// Exponential backoff with a fixed base, jittered by the caller.
    /// </summary>
    /// <remarks>
    /// 30s, 1m, 2m, 4m, 8m. Exponential rather than fixed because the common
    /// causes of a failed send — a provider blip, a rate limit, a transient
    /// network fault — clear on their own given a little room, and hammering
    /// them makes a rate limit worse rather than better.
    /// </remarks>
    public static Duration BackoffFor(int attempt)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), "Attempts are counted from one.");
        }

        var seconds = 30L * (1L << Math.Min(attempt - 1, 10));
        return Duration.FromSeconds(seconds);
    }

    /// <summary>When a message that has just failed its nth attempt should next be tried.</summary>
    public static Instant? NextAttemptAt(int attemptsMade, Instant now) =>
        attemptsMade >= MaxAttempts ? null : now + BackoffFor(attemptsMade);

    /// <summary>
    /// Whether a message the provider accepted has waited long enough without a
    /// receipt to be treated as lost.
    /// </summary>
    public static bool ReceiptOverdue(Instant sentAt, Instant now) => now - sentAt >= ReceiptWindow;

    public static bool ShouldDeadLetter(int attemptsMade) => attemptsMade >= MaxAttempts;
}
