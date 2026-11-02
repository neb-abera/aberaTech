using System.Net;
using System.Text.Json;
using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Fitness.Sync;

/// <summary>What a failed pull says on the page, and the record of it.</summary>
public static class SyncFailure
{
    /// <summary>Every exception except the host stopping is a failed run. An HttpClient timeout is a cancellation too.</summary>
    public static bool Counts(Exception exception, CancellationToken stopping) =>
        !(exception is OperationCanceledException && stopping.IsCancellationRequested);

    /// <summary>The failure in the words the page shows.</summary>
    public static string Describe(string label, Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            $"{label} refused the API key; replace it.",
        HttpRequestException => $"{label} did not answer; will try again.",
        OperationCanceledException => $"{label} timed out; will try again.",
        JsonException or NotSupportedException => $"{label} sent something unreadable; will try again.",
        _ => $"{label} sync failed; will try again."
    };

    /// <summary>
    /// Saves the failed run, dropping whatever the run had pending first: a
    /// failed save leaves its rows tracked, and they would fail this one too.
    /// </summary>
    public static async Task RecordAsync(
        FitnessDbContext database, string source, Instant ranAt, string outcome, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();

        var state = await database.SyncStates.SingleOrDefaultAsync(s => s.Source == source, cancellationToken);
        if (state is null)
        {
            state = new SyncState { Source = source };
            database.SyncStates.Add(state);
        }

        state.LastRunAt = ranAt;
        state.LastOutcome = outcome;
        await database.SaveChangesAsync(cancellationToken);
    }
}
