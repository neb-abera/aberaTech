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

        // A stable synthetic id, so a replay in development behaves like the
        // real thing: the same message never gets two provider ids.
        return Task.FromResult(SendResult.Ok($"local-{message.IdempotencyKey}"));
    }
}
