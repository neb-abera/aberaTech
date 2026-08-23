namespace aberaTech.Scheduling.Sms;

/// <summary>Whether a failed send is worth trying again.</summary>
public enum FailureKind
{
    /// <summary>Something temporary. A later attempt may well work.</summary>
    Transient,

    /// <summary>Nothing about waiting will change this. Stop.</summary>
    Permanent
}

/// <summary>
/// Sorts provider failures into the ones worth retrying and the ones that are
/// simply the answer.
/// </summary>
/// <remarks>
/// Retrying everything five times is nearly as dishonest as retrying nothing.
/// A number that has opted out, a landline, a number that does not exist —
/// none of those become deliverable because you waited thirty seconds and asked
/// again. Retrying them wastes the daily carrier allowance, delays the messages
/// that could have gone, and buries the real reason under four identical
/// failures.
///
/// The costly one is the opt-out. Somebody who replied STOP has told the
/// carrier not to deliver to them, and continuing to attempt it is both futile
/// and the kind of thing that gets a sending number reviewed.
/// </remarks>
public static class TwilioFailure
{
    /// <summary>
    /// Twilio error codes that will never succeed on a retry.
    /// </summary>
    /// <remarks>
    /// Deliberately a short list of the ones whose meaning is unambiguous.
    /// Anything not named here is treated as transient, because retrying
    /// something retryable costs a little and giving up on something
    /// deliverable costs the message.
    /// </remarks>
    private static readonly Dictionary<int, string> Permanent = new()
    {
        [21211] = "That is not a valid phone number.",
        [21214] = "That number cannot receive messages.",
        [21606] = "The sending number cannot send to that destination.",
        [21610] = "That number has replied STOP and will not be messaged again.",
        [21612] = "That route cannot be used to reach the number.",
        [21614] = "That number is not a mobile.",
        [30003] = "The handset is unreachable or switched off for good.",
        [30004] = "That number has blocked messages.",
        [30005] = "That number does not exist.",
        [30006] = "That is a landline or otherwise unreachable number."
    };

    /// <summary>Classifies a failure from the provider's error code, if there is one.</summary>
    public static (FailureKind Kind, string Reason) Classify(int? errorCode, int? httpStatus)
    {
        if (errorCode is { } code && Permanent.TryGetValue(code, out var reason))
        {
            return (FailureKind.Permanent, reason);
        }

        // 4xx without a code we recognise is still most likely our fault rather
        // than a blip, but it is not worth guessing which: rate limiting is a
        // 429 and genuinely worth retrying, and a malformed request will fail
        // identically every time and dead letter soon enough anyway.
        return httpStatus switch
        {
            429 => (FailureKind.Transient, "The provider is rate limiting."),
            >= 500 => (FailureKind.Transient, "The provider had a server error."),
            _ when errorCode is { } other => (FailureKind.Transient, $"The provider returned error {other}."),
            _ => (FailureKind.Transient, "The provider rejected the message.")
        };
    }

    /// <summary>Pulls the error code out of a Twilio error body, if it is there.</summary>
    public static int? ReadErrorCode(string body)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
