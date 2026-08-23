using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Admin;

/// <summary>One person in the queue, as the host sees them.</summary>
/// <remarks>
/// This is the only projection that carries a name and a number, which is why
/// it exists separately from the public one rather than being the same record
/// with fields blanked out. Two shapes cannot leak into each other by accident;
/// one shape with a flag eventually will.
/// </remarks>
public sealed record AdminEntry(
    Guid Id,
    int Position,
    string DisplayName,
    string PhoneE164,
    string State,
    string? ProjectedStart,
    int ExpectedMinutes);

public sealed record AdminQueue(Guid? SessionId, string? Name, bool Open, IReadOnlyList<AdminEntry> Entries);

public sealed record OpenSessionRequest(string? Name, int? DefaultMinutes, int? HoursOpen);

public sealed record DurationRequest(int? Minutes);

public static class AdminEndpoints
{
    /// <summary>
    /// The shortest and longest a conversation may be booked for.
    /// </summary>
    /// <remarks>
    /// Bounds rather than free text, because the value feeds projections for
    /// everybody behind: a mistyped 500 would push the rest of the afternoon
    /// past closing and tell them all they will not be seen.
    /// </remarks>
    private const int MinimumMinutes = 5;

    private const int MaximumMinutes = 120;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/scheduling/admin")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Scheduling admin");

        group.MapGet("/queue", GetQueueAsync);
        group.MapPost("/session", OpenSessionAsync);
        group.MapPost("/session/close", CloseSessionAsync);
        group.MapPost("/queue/{entryId:guid}/start", (Guid entryId, SchedulingDbContext db, QueueNotifier n, IClock c, CancellationToken t) =>
            AdvanceAsync(entryId, QueueEntryState.Serving, db, n, c, t));
        group.MapPost("/queue/{entryId:guid}/done", (Guid entryId, SchedulingDbContext db, QueueNotifier n, IClock c, CancellationToken t) =>
            AdvanceAsync(entryId, QueueEntryState.Done, db, n, c, t));
        group.MapPost("/queue/{entryId:guid}/no-show", (Guid entryId, SchedulingDbContext db, QueueNotifier n, IClock c, CancellationToken t) =>
            AdvanceAsync(entryId, QueueEntryState.NoShow, db, n, c, t));

        group.MapPost("/queue/{entryId:guid}/duration", SetDurationAsync);

        return routes;
    }

    private static async Task<IResult> GetQueueAsync(
        SchedulingDbContext database,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var session = await database.QueueSessions
            .Include(candidate => candidate.Entries)
            .OrderByDescending(candidate => candidate.OpensAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Results.Ok(new AdminQueue(null, null, false, []));
        }

        var projection = QueueProjection
            .Project(session.Entries.Select(entry => entry.ToDomain()), clock.GetCurrentInstant())
            .ToDictionary(entry => entry.Id);

        var entries = session.Entries
            .OrderBy(entry => entry.Position)
            .Select(entry => new AdminEntry(
                entry.Id,
                entry.Position,
                entry.DisplayName,
                entry.PhoneE164,
                entry.State.ToString(),
                projection.TryGetValue(entry.Id, out var projected)
                    ? projected.ProjectedStart.ToString("uuuu-MM-ddTHH:mm:ss'Z'", null)
                    : null,
                (int)entry.Expected.TotalMinutes))
            .ToList();

        return Results.Ok(new AdminQueue(session.Id, session.Name, session.Open, entries));
    }

    private static async Task<IResult> OpenSessionAsync(
        OpenSessionRequest request,
        SchedulingDbContext database,
        SchedulingOptions options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120)
        {
            return Results.BadRequest(new { error = "Give the session a name." });
        }

        // One open session at a time. The public page picks "the open session"
        // to show, so two would make which queue a visitor joined depend on row
        // order, which is no answer at all.
        var already = await database.QueueSessions.AnyAsync(session => session.Open, cancellationToken);
        if (already)
        {
            return Results.Conflict(new { error = "A queue is already open. Close it first." });
        }

        var now = clock.GetCurrentInstant();
        var session = new QueueSession
        {
            Id = Guid.NewGuid(),
            Name = name,
            OpensAt = now,
            ClosesAt = now + Duration.FromHours(Math.Clamp(request.HoursOpen ?? 8, 1, 24)),
            DefaultDuration = Duration.FromMinutes(
                Math.Clamp(request.DefaultMinutes ?? options.DefaultAppointmentMinutes, MinimumMinutes, MaximumMinutes)),
            Open = true
        };

        database.QueueSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { id = session.Id });
    }

    private static async Task<IResult> CloseSessionAsync(
        SchedulingDbContext database,
        CancellationToken cancellationToken)
    {
        var session = await database.QueueSessions.FirstOrDefaultAsync(candidate => candidate.Open, cancellationToken);

        if (session is null)
        {
            return Results.Conflict(new { error = "No queue is open." });
        }

        session.Open = false;
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Changes how long one person is expected to need.
    /// </summary>
    /// <remarks>
    /// The estimate every queue tool gets wrong by assuming one number fits
    /// everybody. A first counselling session and a two minute signature are not
    /// the same conversation, and treating them as fifteen minutes each makes
    /// every projection behind them wrong in the same direction.
    ///
    /// Changing it moves everybody behind, so this reconciles notifications like
    /// any other change to the line: somebody whose turn slips by more than the
    /// tolerance is told, and somebody whose turn barely moves is not.
    /// </remarks>
    private static async Task<IResult> SetDurationAsync(
        Guid entryId,
        DurationRequest request,
        SchedulingDbContext database,
        QueueNotifier notifier,
        CancellationToken cancellationToken)
    {
        if (request.Minutes is not { } minutes || minutes is < MinimumMinutes or > MaximumMinutes)
        {
            return Results.BadRequest(new
            {
                error = $"Give a length between {MinimumMinutes} and {MaximumMinutes} minutes."
            });
        }

        var entry = await database.QueueEntries
            .Include(record => record.Session)
            .ThenInclude(session => session!.Entries)
            .FirstOrDefaultAsync(record => record.Id == entryId, cancellationToken);

        if (entry?.Session is null)
        {
            return Results.NotFound();
        }

        entry.Expected = Duration.FromMinutes(minutes);

        notifier.Reconcile(entry.Session);
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Moves one entry to a new state and lets everybody behind it know.
    /// </summary>
    /// <remarks>
    /// All three host actions are the same operation: the line moved. Starting
    /// an appointment, finishing one, and marking a no-show each change who is
    /// at the front and what everybody else should now expect, so each of them
    /// reconciles notifications in the same transaction.
    /// </remarks>
    private static async Task<IResult> AdvanceAsync(
        Guid entryId,
        QueueEntryState state,
        SchedulingDbContext database,
        QueueNotifier notifier,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var entry = await database.QueueEntries
            .Include(record => record.Session)
            .ThenInclude(session => session!.Entries)
            .FirstOrDefaultAsync(record => record.Id == entryId, cancellationToken);

        if (entry?.Session is null)
        {
            return Results.NotFound();
        }

        entry.State = state;

        if (state == QueueEntryState.Serving)
        {
            // The real start time, which the projection needs so an appointment
            // that overruns stops pushing everybody's estimate further out on
            // every recalculation.
            entry.StartedAt = clock.GetCurrentInstant();
        }

        notifier.Reconcile(entry.Session);
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
