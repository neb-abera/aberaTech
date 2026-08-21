namespace aberaTech.Scheduling.Outbox;

/// <summary>
/// The result of handing one message to a provider.
/// </summary>
/// <param name="Accepted">
/// Whether the provider took the message. Deliberately not called "sent": an
/// accepted message has reached a carrier queue and nothing more. Delivery is
/// decided later, by a receipt.
/// </param>
public readonly record struct SendResult(bool Accepted, string? ProviderMessageId, string? Error)
{
    public static SendResult Ok(string providerMessageId) => new(true, providerMessageId, null);

    public static SendResult Rejected(string error) => new(false, null, error);
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
