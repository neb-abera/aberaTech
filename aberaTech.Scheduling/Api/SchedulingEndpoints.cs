using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace aberaTech.Scheduling.Api;

/// <summary>Everything the public booking page needs, in one round trip.</summary>
/// <param name="Mode">"queue" when a session is open, otherwise "slots".</param>
public sealed record ScheduleState(
    string Mode,
    string HostName,
    string ViewerZoneId,
    IReadOnlyList<SlotView> Slots,
    QueueView? Queue);

public sealed record SlotView(string StartsAt, string EndsAt, int Minutes);

/// <summary>
/// The public view of a queue. Deliberately anonymous.
/// </summary>
/// <remarks>
/// Position and length only. The people in this queue are soldiers being seen
/// for counselling, and who is waiting to speak to their commander is nobody
/// else's business — so the public projection never carries a name, a phone
/// number or anything else that identifies who is in front of you.
/// </remarks>
public sealed record QueueView(string Name, int Waiting, string? NextStartsAt);

/// <summary>What one visitor can see about their own place in the line.</summary>
public sealed record MyPlace(
    Guid Id,
    int Position,
    int Ahead,
    string State,
    string? ProjectedStart,
    int? MinutesAway);

public sealed record JoinRequest(string? Name, string? Phone, string? ZoneId);

public static class SchedulingEndpoints
{
    /// <summary>
    /// The rate limiting policy applied to everything that can cause an SMS or
    /// write a row. Named so the intent is visible at the call site.
    /// </summary>
    public const string PublicWritePolicy = "scheduling-public-write";

    /// <summary>
    /// The state endpoint alone, for a deployment with no database configured.
    /// </summary>
    /// <remarks>
    /// Without this the route does not exist, so the request falls through to
    /// the SPA fallback and the page receives index.html where it expected
    /// JSON — which surfaces to a visitor as a parse error rather than as an
    /// explanation. Mapping the endpoint and answering "unavailable" lets the
    /// page say something true and offer another way to get in touch.
    /// </remarks>
    public static IEndpointRouteBuilder MapSchedulingUnavailable(
        this IEndpointRouteBuilder routes,
        SchedulingOptions options)
    {
        routes.MapGet("/api/scheduling/state", (string? zone) => Results.Ok(new ScheduleState(
            "unavailable",
            options.HostName,
            ResolveZone(zone, options).Id,
            [],
            null)));

        return routes;
    }

    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/scheduling").WithTags("Scheduling");

        group.MapGet("/state", GetStateAsync);
        group.MapGet("/queue/{entryId:guid}", GetMyPlaceAsync);

        group
            .MapPost("/queue", JoinQueueAsync)
            .RequireRateLimiting(PublicWritePolicy);

        group
            .MapDelete("/queue/{entryId:guid}", LeaveQueueAsync)
            .RequireRateLimiting(PublicWritePolicy);

        return routes;
    }

    private static async Task<IResult> GetStateAsync(
        SchedulingDbContext database,
        SchedulingOptions options,
        IClock clock,
        CancellationToken cancellationToken,
        string? zone = null)
    {
        var viewerZone = ResolveZone(zone, options);
        var now = clock.GetCurrentInstant();

        var session = await database.QueueSessions
            .Include(candidate => candidate.Entries)
            .FirstOrDefaultAsync(candidate => candidate.Open, cancellationToken);

        if (session is not null)
        {
            var projection = QueueProjection.Project(
                session.Entries.Select(entry => entry.ToDomain()),
                now);

            return Results.Ok(new ScheduleState(
                "queue",
                options.HostName,
                viewerZone.Id,
                [],
                new QueueView(
                    session.Name,
                    projection.Count,
                    projection.Count > 0 ? Format(projection[0].ProjectedStart) : null)));
        }

        var rules = await database.AvailabilityRules
            .Where(rule => rule.Active)
            .ToListAsync(cancellationToken);

        var today = now.InZone(options.HostZone).Date;

        var busy = await database.Appointments
            .Where(appointment => !appointment.Cancelled && appointment.EndsAt >= now)
            .Select(appointment => new { appointment.StartsAt, appointment.EndsAt })
            .ToListAsync(cancellationToken);

        var slots = SlotPlanner.Plan(
            rules.Select(rule => rule.ToDomain()),
            today,
            today.PlusDays(options.HorizonDays),
            Duration.FromMinutes(options.DefaultAppointmentMinutes),
            busy.Select(window => new Interval(window.StartsAt, window.EndsAt)).ToList(),
            now + Duration.FromMinutes(options.BookingLeadMinutes));

        return Results.Ok(new ScheduleState(
            "slots",
            options.HostName,
            viewerZone.Id,
            slots
                .Select(slot => new SlotView(
                    Format(slot.Start),
                    Format(slot.End),
                    (int)slot.Length.TotalMinutes))
                .ToList(),
            null));
    }

    private static async Task<IResult> JoinQueueAsync(
        JoinRequest request,
        SchedulingDbContext database,
        QueueNotifier notifier,
        SchedulingOptions options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryParse(request.Phone, out var phone))
        {
            // Deliberately unspecific about why. The distinction between "not a
            // number" and "not a US number" is useful to somebody probing for a
            // way to reach an international destination and useless to a soldier
            // who mistyped.
            return Results.BadRequest(new { error = "Enter a US mobile number." });
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120)
        {
            return Results.BadRequest(new { error = "Enter your name." });
        }

        var session = await database.QueueSessions
            .Include(candidate => candidate.Entries)
            .FirstOrDefaultAsync(candidate => candidate.Open, cancellationToken);

        if (session is null)
        {
            return Results.Conflict(new { error = "The queue is not open right now." });
        }

        var now = clock.GetCurrentInstant();

        // Rejoining is idempotent: pressing join twice, or reopening the link on
        // a second device, should find the same place in the line rather than
        // taking a second one.
        var existing = session.Entries.FirstOrDefault(entry =>
            entry.PhoneE164 == phone.Value.E164 && entry.State == QueueEntryState.Waiting);

        if (existing is not null)
        {
            return Results.Ok(new { id = existing.Id });
        }

        var entry = new QueueEntryRecord
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Position = session.Entries.Count == 0 ? 1 : session.Entries.Max(item => item.Position) + 1,
            DisplayName = name,
            PhoneE164 = phone.Value.E164,
            ZoneId = ResolveZone(request.ZoneId, options).Id,
            Expected = session.DefaultDuration,
            State = QueueEntryState.Waiting,
            JoinedAt = now
        };

        session.Entries.Add(entry);
        database.QueueEntries.Add(entry);

        // The notification rows and the queue entry land in one commit. Either
        // this person is in the queue and we have undertaken to text them, or
        // neither happened.
        notifier.Reconcile(session);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { id = entry.Id });
    }

    private static async Task<IResult> GetMyPlaceAsync(
        Guid entryId,
        SchedulingDbContext database,
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

        var now = clock.GetCurrentInstant();
        var projection = QueueProjection.Project(
            entry.Session.Entries.Select(record => record.ToDomain()),
            now);

        var mine = projection.FirstOrDefault(item => item.Id == entryId);

        if (mine is null)
        {
            return Results.Ok(new MyPlace(entry.Id, entry.Position, 0, entry.State.ToString(), null, null));
        }

        var ahead = projection.Count(item => item.Position < mine.Position);

        return Results.Ok(new MyPlace(
            entry.Id,
            mine.Position,
            ahead,
            entry.State.ToString(),
            Format(mine.ProjectedStart),
            (int)mine.WaitFrom(now).TotalMinutes));
    }

    private static async Task<IResult> LeaveQueueAsync(
        Guid entryId,
        SchedulingDbContext database,
        QueueNotifier notifier,
        CancellationToken cancellationToken)
    {
        var entry = await database.QueueEntries
            .Include(record => record.Session)
            .ThenInclude(session => session!.Entries)
            .FirstOrDefaultAsync(record => record.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.State = QueueEntryState.Cancelled;

        // Leaving moves everybody behind them up, which is a change they may
        // well want to hear about.
        if (entry.Session is not null)
        {
            notifier.Reconcile(entry.Session);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Resolves a browser-supplied zone, falling back to the host's.
    /// </summary>
    /// <remarks>
    /// The zone arrives from the client as an untrusted string. Looking it up in
    /// the tzdb rather than trusting it is both a correctness measure and a
    /// small input-validation one: only names the database knows get through.
    /// </remarks>
    private static DateTimeZone ResolveZone(string? zoneId, SchedulingOptions options) =>
        (zoneId is null ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(zoneId)) ?? options.HostZone;

    /// <summary>
    /// Instants cross the wire as ISO-8601 UTC, always.
    /// </summary>
    /// <remarks>
    /// The browser converts to the viewer's zone for display. Sending a
    /// preformatted local time instead would bake the server's idea of the
    /// visitor's zone into the payload, and be wrong for anybody travelling.
    /// </remarks>
    private static string Format(Instant instant) => instant.ToString("uuuu-MM-ddTHH:mm:ss'Z'", null);
}
