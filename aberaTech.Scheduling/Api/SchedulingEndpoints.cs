using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using Npgsql;
using aberaTech.Scheduling.Outbox;

namespace aberaTech.Scheduling.Api;

/// <summary>Everything the public booking page needs, in one round trip.</summary>
/// <param name="Mode">"queue" when a session is open, otherwise "slots".</param>
public sealed record ScheduleState(
    string Mode,
    string HostName,
    string ViewerZoneId,
    IReadOnlyList<SlotView> Slots,
    QueueView? Queue,
    int Days = 0,
    bool MoreDays = false);

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

public sealed record BookRequest(string? StartsAt, string? Name, string? Phone, string? ZoneId);

public sealed record BookingConfirmation(Guid Id, string StartsAt, string EndsAt);

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
            .MapPost("/book", BookSlotAsync)
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
        string? zone = null,
        int? days = null)
    {
        var viewerZone = ResolveZone(zone, options);

        // A three week horizon at quarter hour granularity is roughly five
        // hundred slots and seventy kilobytes, which is a slow first paint on a
        // phone and far more than anybody scrolls. Serve a week by default and
        // let the page ask for more.
        var window = Math.Clamp(days ?? options.DefaultWindowDays, 1, options.HorizonDays);
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
            today.PlusDays(window),
            Duration.FromMinutes(options.DefaultAppointmentMinutes),
            busy.Select(period => new Interval(period.StartsAt, period.EndsAt)).ToList(),
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
            null,
            window,
            window < options.HorizonDays));
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


    /// <summary>Books one slot.</summary>
    /// <remarks>
    /// The availability check here is a courtesy, not the guarantee. Between
    /// reading the free slots and inserting a row another visitor can book the
    /// same time, and no amount of checking first closes that window. The
    /// guarantee is the exclusion constraint in the database, which is why the
    /// insert is wrapped rather than the read: 23P01 comes back as a plain "that
    /// time was just taken", which is the truth.
    /// </remarks>
    private static async Task<IResult> BookSlotAsync(
        BookRequest request,
        SchedulingDbContext database,
        SchedulingOptions options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryParse(request.Phone, out var phone))
        {
            return Results.BadRequest(new { error = "Enter a US mobile number." });
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120)
        {
            return Results.BadRequest(new { error = "Enter your name." });
        }

        if (!InstantPattern.ExtendedIso.Parse(request.StartsAt ?? string.Empty).TryGetValue(default, out var startsAt))
        {
            return Results.BadRequest(new { error = "That is not a valid time." });
        }

        var now = clock.GetCurrentInstant();
        var zone = ResolveZone(request.ZoneId, options);
        var length = Duration.FromMinutes(options.DefaultAppointmentMinutes);
        var endsAt = startsAt + length;

        // The slot must be one we actually offered. Without this the endpoint
        // would accept any instant at all, including outside working hours and
        // in the past, simply because nothing overlapped it.
        var rules = await database.AvailabilityRules.Where(rule => rule.Active).ToListAsync(cancellationToken);
        var today = now.InZone(options.HostZone).Date;
        var busy = await database.Appointments
            .Where(appointment => !appointment.Cancelled && appointment.EndsAt >= now)
            .Select(appointment => new { appointment.StartsAt, appointment.EndsAt })
            .ToListAsync(cancellationToken);

        var offered = SlotPlanner.Plan(
            rules.Select(rule => rule.ToDomain()),
            today,
            today.PlusDays(options.HorizonDays),
            length,
            busy.Select(window => new Interval(window.StartsAt, window.EndsAt)).ToList(),
            now + Duration.FromMinutes(options.BookingLeadMinutes));

        if (!offered.Any(slot => slot.Start == startsAt))
        {
            return Results.Conflict(new { error = "That time is no longer available." });
        }

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            BookedZoneId = zone.Id,
            DisplayName = name,
            PhoneE164 = phone.Value.E164,
            CreatedAt = now,
            Cancelled = false
        };

        database.Appointments.Add(appointment);

        // Confirmation now, reminder later. The reminder is not a scheduled job:
        // it is an ordinary outbox row whose NextAttemptAt is in the future, so
        // it inherits the same retry, receipt and dead letter handling as
        // everything else rather than needing a second delivery path.
        database.Outbox.Add(NewMessage(appointment, NotificationKind.Booked, options, zone, now, now));
        database.Outbox.Add(NewMessage(
            appointment,
            NotificationKind.Reminder,
            options,
            zone,
            now,
            startsAt - Duration.FromMinutes(options.ReminderLeadMinutes)));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOverlap(exception))
        {
            // Somebody else committed the same time between the read above and
            // this insert. The database caught it; nothing is double booked.
            return Results.Conflict(new { error = "That time was just taken. Please pick another." });
        }

        return Results.Ok(new BookingConfirmation(appointment.Id, Format(startsAt), Format(endsAt)));
    }

    private static OutboxMessage NewMessage(
        Appointment appointment,
        NotificationKind kind,
        SchedulingOptions options,
        DateTimeZone zone,
        Instant now,
        Instant dueAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Kind = kind,
            ToPhoneE164 = appointment.PhoneE164,
            Body = MessageComposer.Compose(kind, options.HostName, appointment.StartsAt, zone),
            State = DeliveryState.Pending,
            Attempts = 0,
            CreatedAt = now,
            NextAttemptAt = dueAt,
            IdempotencyKey = $"{appointment.Id}:{kind}"
        };

    /// <summary>Whether this failure is the overlap constraint rather than anything else.</summary>
    /// <remarks>
    /// 23P01 is exclusion_violation. Matched on the SQLSTATE rather than the
    /// message text, which is localised and rewordable between releases.
    /// </remarks>
    private static bool IsOverlap(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: "23P01" };

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
