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

/// <summary>One paired phone, as the page lists it. Never the hash or the push token.</summary>
/// <param name="Push">The phone has registered a push token.</param>
public sealed record AlertDevice(Guid Id, string Name, Instant CreatedAt, Instant? LastSeenAt, bool Push = false);

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

    /// <summary>Stores the phone's push token and Apple environment, replacing any before. Null clears both.</summary>
    /// <remarks>
    /// A new token counts as holding the current plan: the phone registers
    /// after it reads the plan, so it is pushed the next change.
    /// </remarks>
    Task SetPushAsync(Guid id, string? token, string? environment, CancellationToken cancellationToken);

    /// <summary>Clears the phone's push token when it is still this one. Apple said it is dead.</summary>
    Task ClearPushIfAsync(Guid id, string token, CancellationToken cancellationToken);

    /// <summary>The plan version every replica agrees on. 0 before the first change.</summary>
    Task<long> PlanVersionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Bumps the plan version and stores the alarms' fingerprint with it.
    /// Without <paramref name="force"/>, only when the fingerprint differs
    /// from the stored one. A null fingerprint keeps the stored one. True when
    /// the version moved.
    /// </summary>
    Task<bool> BumpPlanAsync(byte[]? fingerprint, bool force, Instant now, CancellationToken cancellationToken);

    /// <summary>Every phone with a push token.</summary>
    Task<IReadOnlyList<PushTarget>> PushTargetsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Claims the push of <paramref name="version"/> to one phone: true for
    /// one caller, and only while the phone has a token, is behind, and was
    /// last pushed at or before <paramref name="windowStart"/>.
    /// </summary>
    Task<bool> TryClaimPushAsync(Guid id, long version, Instant now, Instant windowStart, CancellationToken cancellationToken);
}

public sealed class DatabaseAlertDeviceStore(SchedulingDbContext database) : IAlertDeviceStore
{
    /// <summary>Held for the count and the insert, so two pairings at once cannot make a sixth phone.</summary>
    private const long PairingLock = 0x61617470616972; // "aatpair"

    public async Task<IReadOnlyList<AlertDevice>> ListAsync(CancellationToken cancellationToken) =>
        await database.AlertDevices.AsNoTracking()
            .OrderBy(device => device.CreatedAt)
            .Select(device => new AlertDevice(device.Id, device.Name, device.CreatedAt, device.LastSeenAt, device.ApnsToken != null))
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
            .Select(device => new AlertDevice(device.Id, device.Name, device.CreatedAt, device.LastSeenAt, device.ApnsToken != null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task TouchAsync(Guid id, Instant now, Instant unlessAfter, CancellationToken cancellationToken) =>
        await database.AlertDevices
            .Where(device => device.Id == id && (device.LastSeenAt == null || device.LastSeenAt <= unlessAfter))
            .ExecuteUpdateAsync(set => set.SetProperty(device => device.LastSeenAt, now), cancellationToken);

    public async Task SetPushAsync(Guid id, string? token, string? environment, CancellationToken cancellationToken) =>
        await database.Database.ExecuteSqlAsync(
            $"""
             UPDATE "AlertDevices" SET "ApnsToken" = {token}, "ApnsEnvironment" = {environment},
                 "PushedVersion" = COALESCE((SELECT "Version" FROM "AlertPushStates" WHERE "Id" = {AlertPushStateRecord.SingleId}), 0)
             WHERE "Id" = {id}
             """,
            cancellationToken);

    public async Task ClearPushIfAsync(Guid id, string token, CancellationToken cancellationToken) =>
        await database.AlertDevices
            .Where(device => device.Id == id && device.ApnsToken == token)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(device => device.ApnsToken, (string?)null)
                    .SetProperty(device => device.ApnsEnvironment, (string?)null),
                cancellationToken);

    public async Task<long> PlanVersionAsync(CancellationToken cancellationToken) =>
        await database.AlertPushStates.AsNoTracking()
            .Where(state => state.Id == AlertPushStateRecord.SingleId)
            .Select(state => state.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> BumpPlanAsync(byte[]? fingerprint, bool force, Instant now, CancellationToken cancellationToken)
    {
        // One statement: two replicas that read the same change bump once,
        // because the second finds the fingerprint already stored.
        var changed = await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "AlertPushStates" ("Id", "Version", "AlarmsFingerprint", "UpdatedAt")
             VALUES ({AlertPushStateRecord.SingleId}, 1, {fingerprint}, {now})
             ON CONFLICT ("Id") DO UPDATE SET
                 "Version" = "AlertPushStates"."Version" + 1,
                 "AlarmsFingerprint" = COALESCE(EXCLUDED."AlarmsFingerprint", "AlertPushStates"."AlarmsFingerprint"),
                 "UpdatedAt" = EXCLUDED."UpdatedAt"
             WHERE {force} OR "AlertPushStates"."AlarmsFingerprint" IS DISTINCT FROM EXCLUDED."AlarmsFingerprint"
             """,
            cancellationToken);
        return changed == 1;
    }

    public async Task<IReadOnlyList<PushTarget>> PushTargetsAsync(CancellationToken cancellationToken) =>
        await database.AlertDevices.AsNoTracking()
            .Where(device => device.ApnsToken != null && device.ApnsEnvironment != null)
            .OrderBy(device => device.CreatedAt)
            .Select(device => new PushTarget(
                device.Id, device.ApnsToken!, device.ApnsEnvironment!, device.PushedVersion, device.PushedAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryClaimPushAsync(
        Guid id, long version, Instant now, Instant windowStart, CancellationToken cancellationToken) =>
        // The row decides, like the delivery claims: of two replicas, one
        // UPDATE matches and the other finds the phone already pushed.
        await database.AlertDevices
            .Where(device => device.Id == id
                             && device.ApnsToken != null
                             && device.PushedVersion < version
                             && (device.PushedAt == null || device.PushedAt <= windowStart))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(device => device.PushedVersion, version)
                    .SetProperty(device => device.PushedAt, now),
                cancellationToken) == 1;
}
