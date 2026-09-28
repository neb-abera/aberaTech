using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The phone's credential: "aat_" and 32 random bytes in base64url, 47
/// characters. A look-up secret in NIST SP 800-63B terms: 256 bits from the
/// system's random number generator, shown once, stored as its SHA-256,
/// revocable, and verified behind a failure limit.
/// </summary>
/// <remarks>
/// A plain SHA-256 is enough for a 256-bit random secret. A slow hash is for
/// what a person chooses: nobody can guess through 2^256 values, fast or slow.
/// The hash is the lookup key, so no comparison of the token itself happens.
/// </remarks>
public static class AlertDeviceTokens
{
    public const string Prefix = "aat_";

    public const int RandomBytes = 32;

    /// <summary>"aat_" and 43 base64url characters, the unpadded encoding of 32 bytes.</summary>
    public const int Length = 47;

    /// <summary>At most this many phones. A sixth is refused until one is revoked.</summary>
    public const int MaxDevices = 5;

    public const int MaxNameLength = 60;

    public static string New() => Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes));

    /// <summary>The shape alone: the prefix, the length, and base64url characters. Checked before anything is hashed.</summary>
    public static bool IsWellFormed(string? token) =>
        token is { Length: Length }
        && token.StartsWith(Prefix, StringComparison.Ordinal)
        && token.AsSpan(Prefix.Length).ContainsAnyExcept(Base64UrlCharacters) is false;

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));

    /// <summary>
    /// The link a phone opens to pair. The token rides in the fragment,
    /// which a browser never sends to a server. The server is named only
    /// when it is not abera.tech, for a development server.
    /// </summary>
    public static string PairUrl(string token, string origin, string host) =>
        string.Equals(host, "abera.tech", StringComparison.OrdinalIgnoreCase)
            ? $"aberaalarms://pair#token={token}"
            : $"aberaalarms://pair#token={token}&server={Uri.EscapeDataString(origin)}";

    /// <summary>A name the list can show: 1 to 60 characters after trimming, no control characters. Null when it is not.</summary>
    public static string? CleanName(string? name)
    {
        var text = name?.Trim();
        return text is { Length: > 0 and <= MaxNameLength } && !text.Any(char.IsControl) ? text : null;
    }

    private static readonly System.Buffers.SearchValues<char> Base64UrlCharacters =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");
}

/// <summary>One paired phone, as the page lists it. Never the hash.</summary>
public sealed record AlertDevice(Guid Id, string Name, Instant CreatedAt, Instant? LastSeenAt);

/// <summary>The paired phones. Owner-only on the page, read by the device scheme on every request with a token.</summary>
public interface IAlertDeviceStore
{
    Task<IReadOnlyList<AlertDevice>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Null, and nothing stored, when <see cref="AlertDeviceTokens.MaxDevices"/> are already paired.</summary>
    Task<AlertDevice?> CreateAsync(Guid id, string name, byte[] tokenHash, Instant now, CancellationToken cancellationToken);

    /// <summary>False when there was no such phone.</summary>
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken);

    Task<AlertDevice?> FindAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>Records a request from the phone, unless one was recorded after <paramref name="unlessAfter"/>.</summary>
    Task TouchAsync(Guid id, Instant now, Instant unlessAfter, CancellationToken cancellationToken);
}

public sealed class DatabaseAlertDeviceStore(SchedulingDbContext database) : IAlertDeviceStore
{
    /// <summary>Held for the count and the insert, so two pairings at once cannot make a sixth phone.</summary>
    private const long PairingLock = 0x61617470616972; // "aatpair"

    public async Task<IReadOnlyList<AlertDevice>> ListAsync(CancellationToken cancellationToken) =>
        await database.AlertDevices.AsNoTracking()
            .OrderBy(device => device.CreatedAt)
            .Select(device => new AlertDevice(device.Id, device.Name, device.CreatedAt, device.LastSeenAt))
            .ToListAsync(cancellationToken);

    public async Task<AlertDevice?> CreateAsync(
        Guid id, string name, byte[] tokenHash, Instant now, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // An advisory lock needs no privilege beyond connecting, so the
        // runtime role can take it.
        await database.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({PairingLock})", cancellationToken);

        if (await database.AlertDevices.CountAsync(cancellationToken) >= AlertDeviceTokens.MaxDevices) return null;

        await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertDevices" ("Id", "Name", "TokenHash", "CreatedAt", "LastSeenAt")
             VALUES ({id}, {name}, {tokenHash}, {now}, NULL)
             """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AlertDevice(id, name, now, null);
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken) =>
        await database.AlertDevices.Where(device => device.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public Task<AlertDevice?> FindAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        database.AlertDevices.AsNoTracking()
            .Where(device => device.TokenHash == tokenHash)
            .Select(device => new AlertDevice(device.Id, device.Name, device.CreatedAt, device.LastSeenAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task TouchAsync(Guid id, Instant now, Instant unlessAfter, CancellationToken cancellationToken) =>
        await database.AlertDevices
            .Where(device => device.Id == id && (device.LastSeenAt == null || device.LastSeenAt <= unlessAfter))
            .ExecuteUpdateAsync(set => set.SetProperty(device => device.LastSeenAt, now), cancellationToken);
}
