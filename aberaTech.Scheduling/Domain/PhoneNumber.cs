using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace aberaTech.Scheduling.Domain;

/// <summary>
/// A North American phone number, normalised to E.164.
/// </summary>
/// <remarks>
/// Deliberately narrow. A public form that causes an SMS to be sent is a way
/// for a stranger to spend somebody else's money, and the expensive version of
/// that is an international or premium-rate destination. Restricting to +1
/// removes the profitable case outright, which is worth more than any rate
/// limit, and it costs nothing real: the people this queue is for are soldiers
/// with US numbers.
///
/// Normalising at the edge also means the rate limiter and the outbox see one
/// canonical form. "(913) 499-9497", "913-499-9497" and "+19134999497" must not
/// be three different identities to a per-number cap, or the cap is decorative.
/// </remarks>
public readonly record struct PhoneNumber
{
    private static readonly Regex Digits = new(@"[^\d]", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private PhoneNumber(string e164) => E164 = e164;

    public string E164 { get; }

    /// <summary>The last four digits, for showing somebody which number they gave without printing it.</summary>
    public string Last4 => E164[^4..];

    public static bool TryParse(string? input, [NotNullWhen(true)] out PhoneNumber? number)
    {
        number = null;

        if (string.IsNullOrWhiteSpace(input) || input.Length > 32)
        {
            return false;
        }

        var digits = Digits.Replace(input, string.Empty);

        // Accept the two ways a US number is normally typed, and nothing else.
        digits = digits.Length switch
        {
            10 => "1" + digits,
            11 when digits[0] == '1' => digits,
            _ => string.Empty
        };

        if (digits.Length != 11)
        {
            return false;
        }

        // NANP structure: neither the area code nor the exchange may begin with
        // 0 or 1. This rejects most of what a bot types into a form without
        // needing a lookup.
        if (digits[1] is '0' or '1' || digits[4] is '0' or '1')
        {
            return false;
        }

        number = new PhoneNumber("+" + digits);
        return true;
    }

    public override string ToString() => E164;
}
