using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Outbox;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Sms;

/// <summary>
/// Receives Twilio's delivery receipts and settles the outbox row they refer to.
/// </summary>
public static class SmsReceiptEndpoint
{
    public const string Path = "/api/scheduling/sms-status";

    public static IEndpointRouteBuilder MapSmsReceipts(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(Path, HandleAsync);
        return routes;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        SchedulingDbContext database,
        TwilioOptions options,
        IClock clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("SmsReceipts");

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var parameters = form.Select(field => new KeyValuePair<string, string>(field.Key, field.Value.ToString()));

        // Verified against the configured public URL, not the one reconstructed
        // from this request's headers, which the caller controls.
        var valid = TwilioSignature.IsValid(
            options.AuthToken,
            options.StatusCallbackUrl,
            parameters,
            context.Request.Headers["X-Twilio-Signature"].ToString());

        if (!valid)
        {
            // 403 and nothing else. An unsigned or wrongly signed receipt is
            // either a misconfiguration or somebody trying to mark a message
            // delivered that never arrived, and neither deserves a hint about
            // which part failed.
            logger.LogWarning("Rejected an SMS receipt with an invalid signature.");
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var sid = form["MessageSid"].ToString();
        var status = form["MessageStatus"].ToString();

        if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(status))
        {
            return Results.BadRequest();
        }

        var message = await database.Outbox
            .FirstOrDefaultAsync(candidate => candidate.ProviderMessageId == sid, cancellationToken);

        if (message is null)
        {
            // Twilio retries receipts, and one for a message we have purged is
            // not an error. 200 stops it retrying something we cannot act on.
            return Results.Ok();
        }

        var now = clock.GetCurrentInstant();

        switch (status)
        {
            case "delivered":
                message.State = DeliveryState.Delivered;
                message.DeliveredAt = now;
                message.NextAttemptAt = null;
                message.LastError = null;
                break;

            case "undelivered":
            case "failed":
                // The case the whole design exists for. A carrier refusing the
                // message is a failure that has to re-enter the retry path
                // rather than sit in Sent looking successful.
                var error = form["ErrorCode"].ToString();
                message.LastError = string.IsNullOrEmpty(error)
                    ? $"Twilio reported {status}."
                    : $"Twilio reported {status} with error {error}.";
                message.SentAt = null;

                var next = DeliveryPolicy.NextAttemptAt(message.Attempts, now);
                if (next is null)
                {
                    message.State = DeliveryState.DeadLettered;
                    message.NextAttemptAt = null;
                    logger.LogError(
                        "Message {MessageId} dead lettered after Twilio reported {Status}.",
                        message.Id,
                        status);
                }
                else
                {
                    message.State = DeliveryState.Failed;
                    message.NextAttemptAt = next;
                }

                break;

            default:
                // queued, sending, sent, accepted: still in flight. Nothing to
                // settle, and the reconciler still owns the timeout.
                return Results.Ok();
        }

        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }
}
