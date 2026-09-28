using aberaTech.Scheduling.Admin;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The owner's /alerts page: the next alerts, the last calendar read, the
/// settings, and Mute, Unmute, Skip, each event's type, Save settings and
/// three test sends. Plain JSON over HTTPS, so it works from a locked-down
/// work computer.
/// </summary>
/// <remarks>
/// Owner only, behind the same policy as /devbox and the queue. The actions
/// share one rate limit. Every action answers with the page's whole state,
/// so the page never shows a mute or a skip the server did not store.
/// </remarks>
public static class AlertsEndpoints
{
    public const string ActionsPolicy = "alerts-actions";

    /// <summary>Presses a minute per address. The e2e suite raises it (compose.yaml).</summary>
    public const int DefaultActionsPerMinute = 10;

    /// <summary>How many upcoming alerts the page lists.</summary>
    public const int Listed = 25;

    public static IEndpointRouteBuilder MapAlertsEndpoints(
        this IEndpointRouteBuilder routes, AlertsOptions options, IReadOnlyList<string> missing)
    {
        var group = routes
            .MapGroup("/api/alerts")
            .RequireAuthorization(AdminAuth.PolicyName)
            .WithTags("Alerts");

        if (missing.Count > 0)
        {
            // Names, never values. The worker is not running and nothing
            // else is mapped.
            group.MapGet("/status", () => Results.Ok(new { configured = false, missing }));
            return routes;
        }

        group.MapGet("/status", async (AlertsStatus status, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
            Results.Ok(await StateAsync(status, store, clock, options, cancellationToken)));

        group.MapPost("/mute", async (
            MuteRequest request, AlertsStatus status, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            var now = clock.GetCurrentInstant();
            Instant? until = request.Until switch
            {
                "hour" => AlertMute.ForAnHour(now),
                "morning" => AlertMute.UntilMorning(now, status.Snapshot().Zone),
                _ => null
            };
            if (until is null) return Results.BadRequest("until must be \"hour\" or \"morning\"");

            await store.SetMutedUntilAsync(until, now, cancellationToken);
            return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapPost("/unmute", async (AlertsStatus status, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            await store.SetMutedUntilAsync(null, clock.GetCurrentInstant(), cancellationToken);
            return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapPost("/skip", async (
            SkipRequest request, AlertsStatus status, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            // Only what is on the list: a skip is for an alert the owner can see.
            var alert = status.Snapshot().Plan.FirstOrDefault(planned => planned.Key == request.Key);
            if (alert is null) return Results.NotFound();

            await store.SkipAsync(alert.Key, alert.StartsAt, clock.GetCurrentInstant(), cancellationToken);
            return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapPost("/unskip", async (
            SkipRequest request, AlertsStatus status, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            await store.UnskipAsync(request.Key!, cancellationToken);
            return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapPut("/settings", async (
            SettingsRequest request,
            CalendarAlertWorker worker,
            AlertsStatus status,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var errors = AlertSettings.Validate(
                request.Priority,
                request.RepeatSeconds,
                request.StopAfterMinutes,
                request.Sound,
                request.DefaultLeadMinutes,
                request.PollMinutes,
                request.LookaheadHours,
                request.IncludeAllDay,
                request.TimeZone,
                request.OwnerEmails,
                request.NotificationPriority,
                request.NotificationSound,
                request.DefaultType);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var settings = new AlertSettings(
                request.Priority!.Value,
                request.RepeatSeconds!.Value,
                request.StopAfterMinutes!.Value,
                request.Sound!,
                request.DefaultLeadMinutes!.Value,
                request.PollMinutes!.Value,
                request.LookaheadHours!.Value,
                request.IncludeAllDay!.Value,
                request.TimeZone!.Trim(),
                [.. request.OwnerEmails!.Select(email => email!.Trim())],
                request.NotificationPriority!.Value,
                request.NotificationSound!,
                request.DefaultType!);
            await store.SaveSettingsAsync(settings, clock.GetCurrentInstant(), cancellationToken);

            // This replica plans with the new values now. The others read
            // the row at the start of their next pass.
            await worker.ReadNowAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // One event's type, kept under its UID so it holds for every
        // occurrence. "default" drops the choice. Only an event on the list:
        // a choice is for an event the owner can see.
        group.MapPut("/event-type", async (
            EventTypeRequest request, AlertsStatus status, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!Valid(request.Key)) errors["key"] = ["Required, at most 200 characters."];
            if (request.Type is not { } type || (type != AlertTypes.Default && !AlertTypes.Choices.Contains(type)))
            {
                errors["type"] = ["\"none\", \"notification\", \"alarm\" or \"default\"."];
            }

            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var alert = status.Snapshot().Plan.FirstOrDefault(planned => planned.Key == request.Key);
            if (alert is null) return Results.NotFound();

            await store.SetEventTypeAsync(
                alert.EventId, request.Type == AlertTypes.Default ? null : request.Type, clock.GetCurrentInstant(), cancellationToken);
            return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // Send test alert: an alarm, with the alarm settings.
        group.MapPost("/test", async (AlertDispatcher dispatcher, CancellationToken cancellationToken) =>
            Answer(await dispatcher.SendTestAsync(AlertTypes.Alarm, cancellationToken))).RequireRateLimiting(ActionsPolicy);

        // Send test notification: one sound, with the notification settings.
        group.MapPost("/test-notification", async (AlertDispatcher dispatcher, CancellationToken cancellationToken) =>
            Answer(await dispatcher.SendTestAsync(AlertTypes.Notification, cancellationToken))).RequireRateLimiting(ActionsPolicy);

        // One listed alert, as its real send would go but titled as a test.
        // Claims nothing and ignores mute and skip: pressing it is the owner
        // asking, and the real alert still goes at its time.
        group.MapPost("/test-event", async (
            SkipRequest request, AlertsStatus status, AlertDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            var alert = status.Snapshot().Plan.FirstOrDefault(planned => planned.Key == request.Key);
            if (alert is null) return Results.NotFound();

            // An event whose type is none sends nothing, so there is nothing to test.
            return await dispatcher.SendEventTestAsync(alert, cancellationToken) is { } result
                ? Answer(result)
                : Results.Text("This event sends nothing.", "text/plain", statusCode: StatusCodes.Status409Conflict);
        }).RequireRateLimiting(ActionsPolicy);

        // Only where Program.cs registered the development calendar:
        // Development with Alerts:Fake set. The browser suite calls it
        // first, so the calendar's events are hours ahead of the test.
        if (routes.ServiceProvider.GetService<FakeAlertServices>() is not null)
        {
            group.MapPost("/fake/reset", async (
                FakeAlertServices fake,
                CalendarAlertWorker worker,
                AlertsStatus status,
                IAlertStore store,
                IClock clock,
                CancellationToken cancellationToken) =>
            {
                fake.Reanchor();
                await worker.ReadNowAsync(cancellationToken);
                return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
            }).RequireRateLimiting(ActionsPolicy);

            // The calendar answers 404 until the next reset, so the browser
            // suite can see the page's failed-read banner.
            group.MapPost("/fake/fail", async (
                FakeAlertServices fake,
                CalendarAlertWorker worker,
                AlertsStatus status,
                IAlertStore store,
                IClock clock,
                CancellationToken cancellationToken) =>
            {
                fake.Fail();
                await worker.ReadNowAsync(cancellationToken);
                return Results.Ok(await StateAsync(status, store, clock, options, cancellationToken));
            }).RequireRateLimiting(ActionsPolicy);

            // The last message the fake Pushover took, so the browser suite
            // can see what Send test alert asked for.
            group.MapGet("/fake/sent", (FakeAlertServices fake) =>
                fake.Sent.Count == 0 ? Results.NotFound() : Results.Ok(fake.Sent[^1]));
        }

        return routes;
    }

    /// <summary>
    /// A deployment with no owner sign-in: the page asks and is told the
    /// feature is off, rather than getting the SPA shell where it expected
    /// JSON. Anonymous because it discloses nothing, not even which names
    /// are missing.
    /// </summary>
    public static IEndpointRouteBuilder MapAlertsUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/alerts/status", () =>
            Results.Ok(new { configured = false, missing = Array.Empty<string>() })).AllowAnonymous();

        return routes;
    }

    private static IResult Answer(PushoverResult result) =>
        result.Ok
            ? Results.Ok(new { sent = true })
            : Results.Text(result.Error, "text/plain", statusCode: StatusCodes.Status502BadGateway);

    private static bool Valid(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= AlertPlanner.MaxKeyLength;

    private static async Task<AlertsState> StateAsync(
        AlertsStatus status, IAlertStore store, IClock clock, AlertsOptions options, CancellationToken cancellationToken)
    {
        var snapshot = status.Snapshot();
        var now = clock.GetCurrentInstant();
        var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
        var mutedUntil = await store.MutedUntilAsync(cancellationToken) is { } until && until > now ? until : (Instant?)null;
        var skipped = await store.SkippedAsync(cancellationToken);
        var chosen = await store.EventTypesAsync(cancellationToken);

        return new AlertsState(
            Configured: true,
            TimeZone: snapshot.Zone.Id,
            PollMinutes: settings.PollMinutes,
            DefaultLeadMinutes: settings.DefaultLeadMinutes,
            Settings: new SettingsView(
                settings.Priority,
                settings.RepeatSeconds,
                settings.StopAfterMinutes,
                settings.Sound,
                settings.DefaultLeadMinutes,
                settings.PollMinutes,
                settings.LookaheadHours,
                settings.IncludeAllDay,
                settings.TimeZone,
                settings.OwnerEmails,
                settings.NotificationPriority,
                settings.NotificationSound,
                settings.DefaultType),
            Bounds: SettingsBounds.Instance,
            MutedUntil: mutedUntil?.ToDateTimeOffset(),
            LastFetchAt: snapshot.LastFetchAt?.ToDateTimeOffset(),
            LastFetchError: snapshot.LastFetchError,
            LastSuccessAt: snapshot.LastSuccessAt?.ToDateTimeOffset(),
            LastSend: snapshot.LastSend is { } send
                ? new SendView(send.At.ToDateTimeOffset(), send.Title, send.Outcome)
                : null,
            Alerts:
            [
                .. snapshot.Plan
                    .Where(alert => alert.StartsAt > now)
                    .Take(Listed)
                    .Select(alert =>
                    {
                        var type = AlertTypes.Resolve(alert, chosen.GetValueOrDefault(alert.EventId), settings);
                        return new AlertView(
                            alert.Key,
                            alert.Title,
                            alert.Location,
                            alert.StartsAt.ToDateTimeOffset(),
                            alert.AlertAt.ToDateTimeOffset(),
                            alert.Source == AlertSource.Reminder ? "reminder" : "default",
                            skipped.Contains(alert.Key),
                            mutedUntil is { } muted && alert.AlertAt < muted,
                            alert.Critical,
                            type.Type,
                            type.From);
                    })
            ]);
    }

    public sealed record MuteRequest(string? Until);

    public sealed record SkipRequest(string? Key);

    /// <summary>A listed alert's key and the type for its event: none, notification, alarm, or default to drop the choice.</summary>
    public sealed record EventTypeRequest(string? Key, string? Type);

    /// <summary>The settings form. Every field is required: the page sends the whole form.</summary>
    public sealed record SettingsRequest(
        int? Priority,
        int? RepeatSeconds,
        int? StopAfterMinutes,
        string? Sound,
        int? DefaultLeadMinutes,
        int? PollMinutes,
        int? LookaheadHours,
        bool? IncludeAllDay,
        string? TimeZone,
        IReadOnlyList<string?>? OwnerEmails,
        int? NotificationPriority,
        string? NotificationSound,
        string? DefaultType);

    /// <summary>
    /// The page's whole state. Lists its fields: the settings in force, and
    /// no secret. TimeZone is the zone in use, the calendar's own when it
    /// names one. Settings.TimeZone is the fallback the owner set.
    /// </summary>
    public sealed record AlertsState(
        bool Configured,
        string TimeZone,
        int PollMinutes,
        int DefaultLeadMinutes,
        SettingsView Settings,
        SettingsBounds Bounds,
        DateTimeOffset? MutedUntil,
        DateTimeOffset? LastFetchAt,
        string? LastFetchError,
        DateTimeOffset? LastSuccessAt,
        SendView? LastSend,
        IReadOnlyList<AlertView> Alerts);

    public sealed record SettingsView(
        int Priority,
        int RepeatSeconds,
        int StopAfterMinutes,
        string Sound,
        int DefaultLeadMinutes,
        int PollMinutes,
        int LookaheadHours,
        bool IncludeAllDay,
        string TimeZone,
        IReadOnlyList<string> OwnerEmails,
        int NotificationPriority,
        string NotificationSound,
        string DefaultType);

    public sealed record Bound(int Min, int Max);

    /// <summary>What the form's inputs accept. The server checks the same numbers on save.</summary>
    public sealed record SettingsBounds(
        Bound RepeatSeconds,
        Bound StopAfterMinutes,
        Bound DefaultLeadMinutes,
        Bound PollMinutes,
        Bound LookaheadHours,
        int MaxOwnerEmails,
        int MaxEmergencySounds,
        IReadOnlyList<string> Sounds)
    {
        public static readonly SettingsBounds Instance = new(
            new Bound(AlertSettings.MinRepeatSeconds, AlertSettings.MaxRepeatSeconds),
            new Bound(AlertSettings.MinStopAfterMinutes, AlertSettings.MaxStopAfterMinutes),
            new Bound(AlertSettings.MinDefaultLeadMinutes, AlertSettings.MaxDefaultLeadMinutes),
            new Bound(AlertSettings.MinPollMinutes, AlertSettings.MaxPollMinutes),
            new Bound(AlertSettings.MinLookaheadHours, AlertSettings.MaxLookaheadHours),
            AlertSettings.MaxOwnerEmails,
            PushoverClient.MaxEmergencySounds,
            PushoverClient.Sounds);
    }

    public sealed record SendView(DateTimeOffset At, string Title, string Outcome);

    public sealed record AlertView(
        string Key,
        string Title,
        string? Location,
        DateTimeOffset StartsAt,
        DateTimeOffset AlertAt,
        string Source,
        bool Skipped,
        bool Muted,
        bool Critical,
        string Type,
        string TypeFrom);
}
