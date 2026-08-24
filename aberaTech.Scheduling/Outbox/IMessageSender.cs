namespace aberaTech.Scheduling.Outbox;

/// <summary>
/// The result of handing one message to a provider.
/// </summary>
/// <param name="Accepted">
/// Whether the provider took the message. Deliberately not called "sent": an
/// accepted message has reached a carrier queue and nothing more. Delivery is
/// decided later, by a receipt.
/// </param>
/// <param name="DeliveryConfirmed">
/// Whether delivery is already settled, so no receipt will follow. False for a
/// real carrier, where acceptance and delivery are separate events minutes
/// apart. True only for a sender that is itself the final destination.
/// </param>
/// <param name="Permanent">
/// Whether retrying is pointless. An opted-out number or a landline does not
/// become deliverable by waiting.
/// </param>
public readonly record struct SendResult(
    bool Accepted,
    string? ProviderMessageId,
    string? Error,
    bool DeliveryConfirmed = false,
    bool Permanent = false,
    /// <summary>The recipient asked not to be messaged, rather than being unreachable.</summary>
    bool OptedOut = false)
{
    /// <summary>Accepted by a provider. Delivery is not yet known.</summary>
    public static SendResult Ok(string providerMessageId) => new(true, providerMessageId, null);

    /// <summary>
    /// Delivered, with no receipt to wait for.
    /// </summary>
    /// <remarks>
    /// Only for senders with no receipt channel at all. Using this for a carrier
    /// would reintroduce the exact bug this project exists to fix: treating "the
    /// API returned success" as "it reached a handset".
    /// </remarks>
    public static SendResult Confirmed(string providerMessageId) => new(true, providerMessageId, null, true);

    public static SendResult Rejected(string error) => new(false, null, error);

    /// <summary>Refused for a reason that will not change. Do not retry.</summary>
    public static SendResult RejectedPermanently(string error) => new(false, null, error, false, true);

    /// <summary>Refused because the recipient opted out. Never message them again.</summary>
    public static SendResult RejectedAsOptedOut(string error) => new(false, null, error, false, true, true);
}

/// <summary>
/// Hands a message to whatever actually delivers it.
/// </summary>
/// <remarks>
/// An interface rather than a direct Twilio call so the dispatcher's retry and
/// reconciliation behaviour can be exercised without credentials, a network, or
/// a carrier — and so the site runs end to end in development before any A2P
/// registration exists.
/// </remarks>
public interface IMessageSender
{
    Task<SendResult> SendAsync(OutboxMessage message, CancellationToken cancellationToken);
}
