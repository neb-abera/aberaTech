using System.Security.Cryptography;
using System.Text;

namespace aberaTech.Scheduling.Sms;

/// <summary>
/// Validates the <c>X-Twilio-Signature</c> header on an incoming request.
/// </summary>
/// <remarks>
/// This is not optional decoration on the delivery-receipt webhook, it is the
/// thing that makes the receipt worth believing. The retry logic treats a
/// receipt as the single source of truth about whether a message reached a
/// handset; an unauthenticated endpoint would let anybody on the internet POST
/// "delivered" for a message that never arrived, which switches off the retry
/// for exactly the messages that needed it. That reproduces the original
/// Waitwhile failure by hand, and does it silently.
///
/// The algorithm is Twilio's: take the full request URL, append each POST
/// parameter's name and value in order sorted by name, HMAC-SHA1 the result
/// with the account's auth token, and base64 the digest.
/// </remarks>
public static class TwilioSignature
{
    /// <summary>Computes the expected signature for a request.</summary>
    public static string Compute(string authToken, string url, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(authToken);
        ArgumentException.ThrowIfNullOrEmpty(url);

        var builder = new StringBuilder(url);

        // Ordinal, not culture-aware: the ordering has to match Twilio's byte
        // wise sort exactly, and a culture-sensitive comparison would disagree
        // with it for some keys under some locales.
        foreach (var parameter in parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(parameter.Key).Append(parameter.Value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Whether <paramref name="provided"/> is the correct signature.
    /// </summary>
    /// <remarks>
    /// Compared with a fixed-time equality. A naive string comparison returns as
    /// soon as two bytes differ, and the time it took is a measurement of how
    /// much of the prefix was right — enough, over many attempts, to construct a
    /// valid signature a byte at a time without ever knowing the token.
    /// </remarks>
    public static bool IsValid(
        string authToken,
        string url,
        IEnumerable<KeyValuePair<string, string>> parameters,
        string? provided)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var expected = Compute(authToken, url, parameters);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}
