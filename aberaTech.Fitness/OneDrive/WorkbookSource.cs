using aberaTech.Fitness.Data;
using aberaTech.Fitness.Workbook;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.OneDrive;

/// <summary>What one read of the goals workbook gave.</summary>
/// <param name="Sheet">The essential rows, from this read or the last good one.</param>
/// <param name="Origin">Where the workbook was read from, for the page.</param>
/// <param name="ReadAt">When the rows in <paramref name="Sheet"/> were read.</param>
/// <param name="Modified">When the workbook was last saved, as OneDrive reports it.</param>
/// <param name="Error">Why this read failed; the sheet, if any, is the last good one.</param>
/// <param name="Configured">Whether a source is set up at all.</param>
/// <param name="Connected">Whether OneDrive has been granted.</param>
public sealed record WorkbookRead(
    GoalSheet? Sheet,
    string Origin,
    Instant? ReadAt,
    DateTimeOffset? Modified,
    string? Error,
    bool Configured,
    bool Connected);

public interface IWorkbookSource
{
    Task<WorkbookRead> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The last good read and the access token, shared by every request. Not the
/// workbook's store: it lives in OneDrive and is read again whenever its ETag
/// changes. This is the copy shown, with its time, while OneDrive cannot answer.
/// </summary>
public sealed class WorkbookCache
{
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal string? AccessToken;
    internal Instant AccessTokenExpires;
    internal string? ETag;
    internal GoalSheet? Sheet;
    internal Instant? ReadAt;
    internal DateTimeOffset? Modified;

    internal void Forget()
    {
        AccessToken = null;
        ETag = null;
        Sheet = null;
        ReadAt = null;
        Modified = null;
    }
}

/// <summary>Reads the goals workbook from the owner's OneDrive, or from a file in development and tests.</summary>
public sealed class WorkbookSource(
    OneDriveOptions options,
    WorkbookCache cache,
    IServiceProvider services,
    IClock clock) : IWorkbookSource
{
    public const string ProtectorPurpose = "aberaTech.Fitness.OneDriveRefreshToken";

    /// <summary>An access token this close to expiry is refreshed first.</summary>
    private static readonly Duration TokenMargin = Duration.FromMinutes(5);

    public async Task<WorkbookRead> ReadAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.LocalWorkbookPath)) return ReadLocal(options.LocalWorkbookPath);
        if (!options.IsConfigured) return new WorkbookRead(null, "OneDrive", null, null, null, Configured: false, Connected: false);

        await cache.Gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadOneDriveAsync(cancellationToken);
        }
        finally
        {
            cache.Gate.Release();
        }
    }

    private WorkbookRead ReadLocal(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var sheet = GoalWorkbook.Parse(stream);
            return new WorkbookRead(sheet, "file", clock.GetCurrentInstant(), File.GetLastWriteTimeUtc(path), null, true, true);
        }
        catch (Exception e) when (e is GoalWorkbookException or IOException or UnauthorizedAccessException)
        {
            return new WorkbookRead(null, "file", null, null, e.Message, true, true);
        }
    }

    private async Task<WorkbookRead> ReadOneDriveAsync(CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FitnessDbContext>();
        var connection = await database.OneDriveConnections.SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);
        if (connection is null)
        {
            cache.Forget();
            return new WorkbookRead(null, "OneDrive", null, null, null, Configured: true, Connected: false);
        }

        var client = services.GetRequiredService<OneDriveClient>();
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(ProtectorPurpose);

        try
        {
            var now = clock.GetCurrentInstant();
            if (cache.AccessToken is null || now + TokenMargin >= cache.AccessTokenExpires)
            {
                var tokens = await client.RefreshAsync(protector.Unprotect(connection.ProtectedRefreshToken), cancellationToken);
                cache.AccessToken = tokens.AccessToken;
                cache.AccessTokenExpires = now + Duration.FromSeconds(tokens.ExpiresInSeconds);

                // Personal accounts rotate the refresh token; the newest one is
                // the one that will work next time.
                connection.ProtectedRefreshToken = protector.Protect(tokens.RefreshToken);
                await database.SaveChangesAsync(cancellationToken);
            }

            var file = await client.MetadataAsync(cache.AccessToken, options.WorkbookPath, cancellationToken);
            if (cache.Sheet is null || !string.Equals(file.ETag, cache.ETag, StringComparison.Ordinal))
            {
                var bytes = await client.DownloadAsync(cache.AccessToken, options.WorkbookPath, cancellationToken);
                using var stream = new MemoryStream(bytes, writable: false);
                cache.Sheet = GoalWorkbook.Parse(stream);
                cache.ETag = file.ETag;
                cache.Modified = file.LastModified;
            }

            cache.ReadAt = clock.GetCurrentInstant();
            return new WorkbookRead(cache.Sheet, "OneDrive", cache.ReadAt, cache.Modified, null, true, true);
        }
        catch (Exception e) when (e is OneDriveException or GoalWorkbookException or HttpRequestException or TaskCanceledException
                                      or System.Security.Cryptography.CryptographicException)
        {
            if (e is OneDriveException { Status: System.Net.HttpStatusCode.Unauthorized }) cache.AccessToken = null;

            var reason = e switch
            {
                TaskCanceledException when !cancellationToken.IsCancellationRequested => "OneDrive did not answer in time.",
                HttpRequestException => "OneDrive could not be reached.",
                System.Security.Cryptography.CryptographicException => "The stored sign-in could not be unsealed. Connect OneDrive again.",
                _ => e.Message
            };

            if (cancellationToken.IsCancellationRequested) throw;
            return new WorkbookRead(cache.Sheet, "OneDrive", cache.ReadAt, cache.Modified, reason, true, true);
        }
    }
}
