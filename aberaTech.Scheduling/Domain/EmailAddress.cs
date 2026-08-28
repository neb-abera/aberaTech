namespace aberaTech.Scheduling.Domain;

/// <summary>An email address that looked plausible enough to send an invite to.</summary>
/// <remarks>
/// Deliberately not RFC 5322. The full grammar admits addresses no mail system
/// accepts and rejects none of the typos people actually make; the checks here
/// are the shape a real address always has — one @, something before it,
/// a dot somewhere after it, no spaces — and Google's delivery failure covers
/// the rest. A stricter parser would only manufacture reasons to refuse a
/// booking over a courtesy field.
/// </remarks>
public readonly record struct EmailAddress(string Value)
{
    /// <summary>The practical ceiling from RFC 5321, and the column width.</summary>
    public const int MaxLength = 254;

    public static bool TryParse(string? input, out EmailAddress? email)
    {
        email = null;

        var trimmed = (input ?? string.Empty).Trim();

        if (trimmed.Length is 0 or > MaxLength || trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = trimmed.IndexOf('@');

        // Exactly one @, with a local part before it and a domain after it.
        if (at < 1 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return false;
        }

        // The domain needs a dot that is neither its first character nor its
        // last: "a@.b" and "a@b." are typos, not addresses.
        var dot = trimmed.IndexOf('.', at + 2);

        if (dot == -1 || dot == trimmed.Length - 1)
        {
            return false;
        }

        email = new EmailAddress(trimmed);
        return true;
    }
}
