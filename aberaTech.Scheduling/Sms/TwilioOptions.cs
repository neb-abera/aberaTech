namespace aberaTech.Scheduling.Sms;

/// <summary>Credentials and numbers for the SMS provider.</summary>
/// <remarks>
/// None of this belongs in appsettings. AccountSid and AuthToken are container
/// app secrets; the auth token in particular is the key that signs delivery
/// receipts, so anybody holding it can forge them.
/// </remarks>
public sealed class TwilioOptions
{
    public const string Section = "Twilio";

    public string AccountSid { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    /// <summary>The sending number, in E.164.</summary>
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>
    /// The absolute, public URL Twilio should POST delivery receipts to.
    /// </summary>
    /// <remarks>
    /// Configured rather than reconstructed from the incoming request. Behind a
    /// proxy the scheme and host come from forwarded headers, which the caller
    /// controls — and since the URL is part of what the signature covers,
    /// trusting those headers would let an attacker choose the string being
    /// verified. A fixed value cannot be influenced from outside.
    /// </remarks>
    public string StatusCallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether SMS can be sent *and* confirmed.
    /// </summary>
    /// <remarks>
    /// The callback URL is required, not optional. Without it Twilio sends no
    /// delivery receipts, so every message would sit in Sent until its receipt
    /// window closed, be retried five times and dead letter — a working sender
    /// that reports total failure. Treating a half-configured provider as not
    /// configured falls back to the logging sender instead, which is wrong in a
    /// way somebody notices immediately rather than wrong in a way that looks
    /// like the carrier is broken.
    /// </remarks>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(FromNumber)
        && !string.IsNullOrWhiteSpace(StatusCallbackUrl);

    /// <summary>Something was filled in, but not enough of it.</summary>
    public bool IsPartiallyConfigured =>
        !IsConfigured
        && (!string.IsNullOrWhiteSpace(AccountSid)
            || !string.IsNullOrWhiteSpace(AuthToken)
            || !string.IsNullOrWhiteSpace(FromNumber)
            || !string.IsNullOrWhiteSpace(StatusCallbackUrl));
}
