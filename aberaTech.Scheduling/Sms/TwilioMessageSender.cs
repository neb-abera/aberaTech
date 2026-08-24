using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using aberaTech.Scheduling.Outbox;

namespace aberaTech.Scheduling.Sms;

/// <summary>
/// Hands a message to Twilio.
/// </summary>
/// <remarks>
/// Raw HTTP rather than the Twilio SDK: the Messages endpoint is one form POST,
/// and this keeps the dependency surface of a public-facing service smaller
/// than pulling in a client library and its transitive graph for a single call.
///
/// Note what this class does *not* do: decide anything about retries. It reports
/// what happened and the dispatcher owns the schedule, so all the retry
/// behaviour lives in one place and is tested without a network.
/// </remarks>
public sealed class TwilioMessageSender(
    HttpClient http,
    TwilioOptions options,
    ILogger<TwilioMessageSender> logger) : IMessageSender
{
    public async Task<SendResult> SendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("To", message.ToPhoneE164),
            new("From", options.FromNumber),
            new("Body", message.Body)
        };

        // Without a status callback there is no receipt, and with no receipt
        // every message would sit in Sent until the reconciler gave up on it.
        if (!string.IsNullOrWhiteSpace(options.StatusCallbackUrl))
        {
            form.Add(new KeyValuePair<string, string>("StatusCallback", options.StatusCallbackUrl));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{options.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(form)
        };

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Twilio deduplicates on this, so a retry of a request it already
        // accepted cannot produce a second text on somebody's phone.
        request.Headers.Add("I-Twilio-Idempotency-Token", message.IdempotencyKey);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorCode = TwilioFailure.ReadErrorCode(body);
            var (kind, reason) = TwilioFailure.Classify(errorCode, (int)response.StatusCode);

            // The status and Twilio's error code, never the number. A failed send
            // is exactly when somebody is tempted to log the recipient to help
            // debugging, and exactly when it should not be in the log.
            logger.LogWarning(
                "Twilio rejected message {MessageId}: {StatusCode}, code {ErrorCode}, {Kind}.",
                message.Id,
                (int)response.StatusCode,
                errorCode,
                kind);

            if (TwilioFailure.IsOptOut(errorCode))
            {
                return SendResult.RejectedAsOptedOut(reason);
            }

            return kind == FailureKind.Permanent
                ? SendResult.RejectedPermanently(reason)
                : SendResult.Rejected(reason);
        }

        var sid = ReadSid(body);

        return sid is null
            ? SendResult.Rejected("Twilio accepted the message but returned no sid.")
            // Accepted, not delivered. The receipt decides that.
            : SendResult.Ok(sid);
    }

    private static string? ReadSid(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A short, bounded excerpt of an error body, safe to store.</summary>
    private static string Summarise(string body) => body.Length <= 200 ? body : body[..200];
}
