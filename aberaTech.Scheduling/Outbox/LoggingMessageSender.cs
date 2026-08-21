namespace aberaTech.Scheduling.Outbox;

/// <summary>
/// Records what would have been sent. The default until an SMS provider is
/// configured.
/// </summary>
/// <remarks>
/// Note what is not logged: the recipient's number. These are phone numbers of
/// soldiers, and a log line is the easiest place in a system for personal data
/// to end up somewhere it was never meant to go — aggregated, shipped to a log
/// service, retained past any policy anyone wrote down. The message id is
/// enough to follow one message through the system, and the number can be
/// looked up deliberately by somebody who needs it.
/// </remarks>
public sealed class LoggingMessageSender(ILogger<LoggingMessageSender> logger) : IMessageSender
{
    public Task<SendResult> SendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Would send {Kind} message {MessageId} ({Length} characters). No SMS provider is configured.",
            message.Kind,
            message.Id,
            message.Body.Length);

        // Confirmed, not merely accepted. This sender is the final destination,
        // so no delivery receipt is ever coming; reporting it as accepted would
        // leave every development message to time out its receipt window, get
        // retried five times and dead letter, filling the log with errors about
        // a provider that does not exist.
        //
        // A real carrier must never use this. There, acceptance and delivery are
        // separate events and the gap between them is the whole problem.
        return Task.FromResult(SendResult.Confirmed($"local-{message.IdempotencyKey}"));
    }
}
