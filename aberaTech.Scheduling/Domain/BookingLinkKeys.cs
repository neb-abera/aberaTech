using System.Security.Cryptography;
using System.Text;

namespace aberaTech.Scheduling.Domain;

/// <summary>
/// The secret in a booking link, and the hash the database keeps of it.
/// </summary>
/// <remarks>
/// The key is 128 random bits, written as 22 URL-safe base64 characters. Only
/// its SHA-256 is stored, so a copy of the database holds no working link.
/// A plain hash is enough: the key is random, so there is no dictionary to
/// run against it, and a slow hash would only slow every page load.
/// </remarks>
public static class BookingLinkKeys
{
    /// <summary>The longest key accepted from a request. Longer input is refused unhashed.</summary>
    public const int MaxLength = 64;

    /// <summary>A new key, for a new link or a link whose old key is being replaced.</summary>
    public static string NewKey() => Base64Url(RandomNumberGenerator.GetBytes(16));

    /// <summary>The stored form of a key: lowercase hex SHA-256.</summary>
    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>
    /// The hash to look up for a key a visitor sent, or null when the input
    /// cannot be a key.
    /// </summary>
    public static string? HashOrNull(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxLength)
        {
            return null;
        }

        foreach (var character in key)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                return null;
            }
        }

        return Hash(key);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
