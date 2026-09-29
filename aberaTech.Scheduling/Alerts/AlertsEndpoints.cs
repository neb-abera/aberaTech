using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The owner's /alerts page: the next alerts, the last calendar read, the
/// settings, and Mute, Unmute, Skip, Acknowledge, each event's type, Save
/// settings, three test sends and the paired phones. Plain JSON over HTTPS,
/// so it works from a locked-down work computer.
/// </summary>
/// <remarks>
/// The owner's Google sign-in reaches every route. A paired phone's token
/// reaches the eight a phone needs (<see cref="AlertsAuth.OwnerOrDevicePolicy"/>):
/// the status, mute, unmute, skip, unskip, ack, an event's type and a new
/// event. The actions share one rate
/// limit. Every action answers with the page's whole state, so the page and
/// the phone never show a mute or a skip the server did not store.
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
        // The owner's cookie alone.
        var group = routes
            .MapGroup("/api/alerts")
            .RequireAuthorization(AlertsAuth.OwnerPolicy)
            .WithTags("Alerts");

        // The owner's cookie or a paired phone's token.
        var shared = routes
            .MapGroup("/api/alerts")
            .RequireAuthorization(AlertsAuth.OwnerOrDevicePolicy)
            .WithTags("Alerts");

        if (missing.Count > 0)
        {
            // Names, never values. The worker is not running and nothing
            // else is mapped.
            shared.MapGet("/status", () => Results.Ok(new { configured = false, missing }));
            return routes;
        }

        shared.MapGet("/status", async (AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
            Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken)));

        shared.MapPost("/mute", async (
            MuteRequest request, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
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
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPost("/unmute", async (AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            await store.SetMutedUntilAsync(null, clock.GetCurrentInstant(), cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPost("/skip", async (
            SkipRequest request, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            // Only what is on the list: a skip is for an alert the owner can see.
            var alert = await FindAsync(request.Key!, status, store, clock, options, cancellationToken);
            if (alert is null) return Results.NotFound();

            await store.SkipAsync(alert.Key, alert.StartsAt, clock.GetCurrentInstant(), cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPost("/unskip", async (
            SkipRequest request, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            await store.UnskipAsync(request.Key!, cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapPut("/settings", async (
            SettingsRequest request,
            CalendarAlertWorker worker,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            // A sound saved earlier stays valid while Pushover cannot be
            // reached and only the built-ins are listed.
            var allowed = (await sounds.CurrentAsync(cancellationToken)).Select(sound => sound.Name).ToHashSet();
            var saved = await AlertSettings.CurrentAsync(store, options, cancellationToken);
            allowed.Add(saved.Sound);
            allowed.Add(saved.NotificationSound);

            var errors = AlertSettings.Validate(
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
                request.DefaultType,
                request.BackupDelaySeconds,
                allowed);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var settings = new AlertSettings(
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
                request.DefaultType!,
                request.BackupDelaySeconds!.Value);
            await store.SaveSettingsAsync(settings, clock.GetCurrentInstant(), cancellationToken);

            // This replica plans with the new values now. The others read
            // the row at the start of their next pass.
            await worker.ReadNowAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // One event's type, kept under its UID so it holds for every
        // occurrence. "default" drops the choice. Only an event on the list:
        // a choice is for an event the owner can see. The choice is then
        // written to Google Calendar: #critical in the description for an
        // alarm, removed for anything else. The choice stands whatever
        // Google says, and calendarWrite says why Google was not changed.
        shared.MapPut("/event-type", async (
            EventTypeRequest request,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            GoogleAlertEvents google,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!Valid(request.Key)) errors["key"] = ["Required, at most 200 characters."];
            if (request.Type is not { } type || (type != AlertTypes.Default && !AlertTypes.Choices.Contains(type)))
            {
                errors["type"] = ["\"none\", \"notification\", \"alarm\" or \"default\"."];
            }

            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var alert = await FindAsync(request.Key!, status, store, clock, options, cancellationToken);
            if (alert is null) return Results.NotFound();

            await store.SetEventTypeAsync(
                alert.EventId, request.Type == AlertTypes.Default ? null : request.Type, clock.GetCurrentInstant(), cancellationToken);
            var written = await google.SetMarkAsync(alert.EventId, request.Type == AlertTypes.Alarm, cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken, written));
        }).RequireRateLimiting(ActionsPolicy);

        // Acknowledge: the phone's alarm or the browser's ring was answered.
        // An occurrence on the list, or one that was sent and has since
        // dropped off it, so a phone that answers late still counts. The
        // first acknowledgement stands, and it cancels Pushover's repeats.
        shared.MapPost("/ack", async (
            AckRequest request,
            AlertsStatus status,
            PushoverSounds sounds,
            AlertDispatcher dispatcher,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!Valid(request.Key)) errors["key"] = ["Required, at most 200 characters."];
            if (request.Via is not ("phone" or "browser")) errors["via"] = ["\"phone\" or \"browser\"."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var planned = await FindAsync(request.Key!, status, store, clock, options, cancellationToken);
            var startsAt = planned?.StartsAt ?? (await store.DeliveryAsync(request.Key!, cancellationToken))?.StartsAt;
            if (startsAt is null) return Results.NotFound();

            if (await store.AcknowledgeAsync(request.Key!, startsAt.Value, request.Via!, clock.GetCurrentInstant(), cancellationToken))
            {
                await dispatcher.CancelRepeatsAsync(request.Key!, cancellationToken);
            }

            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // A new event on the calendar the feed reads, with one popup reminder
        // at the lead and #critical for an alarm. Kept here until the feed
        // carries it, so it is listed and alerts at once (CreatedEvents).
        shared.MapPost("/events", async (
            NewEventRequest request,
            CalendarAlertWorker worker,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            GoogleAlertEvents google,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var now = clock.GetCurrentInstant();
            var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
            var (errors, valid) = NewEventRequest.Validate(request, now, settings.DefaultLeadMinutes);
            if (valid is null) return Results.ValidationProblem(errors);

            var outcome = await google.CreateAsync(valid, cancellationToken);
            if (outcome.Event is not { } created)
            {
                return Results.Problem(
                    detail: outcome.Problem,
                    statusCode: outcome.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status502BadGateway);
            }

            await store.AddCreatedEventAsync(created, now, cancellationToken);
            await store.SetEventTypeAsync(AlertPlanner.EventIdFor(created.EventId), valid.Type, now, cancellationToken);

            // This replica plans it on its next pass, now rather than at the
            // end of the wait.
            worker.Wake();
            return Results.Created((string?)null, await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // The paired phones. The token is in the answer to the pairing and
        // nowhere else, ever: the list names the phones, and the server
        // keeps only each token's hash.
        group.MapGet("/devices", async (IAlertDeviceStore devices, CancellationToken cancellationToken) =>
            Results.Ok((await devices.ListAsync(cancellationToken)).Select(DeviceView.From)));

        group.MapPost("/devices", async (
            DeviceRequest request, HttpContext context, IAlertDeviceStore devices, IClock clock, CancellationToken cancellationToken) =>
        {
            if (AlertDeviceTokens.CleanName(request.Name) is not { } name)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = [$"1 to {AlertDeviceTokens.MaxNameLength} characters."]
                });
            }

            var token = AlertDeviceTokens.New();
            var device = await devices.CreateAsync(
                Guid.NewGuid(), name, AlertDeviceTokens.Hash(token), clock.GetCurrentInstant(), cancellationToken);
            if (device is null)
            {
                return Results.Text(
                    $"At most {AlertDeviceTokens.MaxDevices} phones. Revoke one first.",
                    "text/plain",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var origin = $"{context.Request.Scheme}://{context.Request.Host}";
            return Results.Created(
                $"/api/alerts/devices/{device.Id}",
                new PairedDevice(
                    device.Id,
                    device.Name,
                    device.CreatedAt.ToDateTimeOffset(),
                    token,
                    AlertDeviceTokens.PairUrl(token, origin, context.Request.Host.Host)));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapDelete("/devices/{id:guid}", async (Guid id, IAlertDeviceStore devices, CancellationToken cancellationToken) =>
            await devices.RevokeAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .RequireRateLimiting(ActionsPolicy);

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
            SkipRequest request,
            AlertsStatus status,
            IAlertStore store,
            IClock clock,
            AlertDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            var alert = await FindAsync(request.Key!, status, store, clock, options, cancellationToken);
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
                PushoverSounds sounds,
                IAlertStore store,
                IClock clock,
                CancellationToken cancellationToken) =>
            {
                fake.Reanchor();
                await worker.ReadNowAsync(cancellationToken);
                return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
            }).RequireRateLimiting(ActionsPolicy);

            // The calendar answers 404 until the next reset, so the browser
            // suite can see the page's failed-read banner.
            group.MapPost("/fake/fail", async (
                FakeAlertServices fake,
                CalendarAlertWorker worker,
                AlertsStatus status,
                PushoverSounds sounds,
                IAlertStore store,
                IClock clock,
                CancellationToken cancellationToken) =>
            {
                fake.Fail();
                await worker.ReadNowAsync(cancellationToken);
                return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
            }).RequireRateLimiting(ActionsPolicy);

            // An alarm due now, until the next reset, so the browser suite
            // can ring the page and acknowledge it.
            group.MapPost("/fake/due", async (
                FakeAlertServices fake,
                CalendarAlertWorker worker,
                AlertsStatus status,
                PushoverSounds sounds,
                IAlertStore store,
                IClock clock,
                CancellationToken cancellationToken) =>
            {
                fake.AddDue();
                await worker.ReadNowAsync(cancellationToken);
                return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
            }).RequireRateLimiting(ActionsPolicy);

            // The writes the fake Google Calendar took, so the browser suite
            // can see what a type change and a new event asked for.
            group.MapGet("/fake/google", (FakeAlertServices fake) => Results.Ok(fake.Google.Writes));

            // The receipts whose repeats an acknowledgement cancelled.
            group.MapGet("/fake/cancelled", (FakeAlertServices fake) => Results.Ok(fake.Cancelled));

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

    /// <summary>One planned occurrence by key: from the feed, or created here and not in the feed yet.</summary>
    private static async Task<PlannedAlert?> FindAsync(
        string key, AlertsStatus status, IAlertStore store, IClock clock, AlertsOptions options, CancellationToken cancellationToken)
    {
        var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
        var plan = await AlertsPlan.CurrentAsync(status.Snapshot(), store, settings, clock.GetCurrentInstant(), cancellationToken);
        return plan.FirstOrDefault(planned => planned.Key == key);
    }

    private static async Task<AlertsState> StateAsync(
        AlertsStatus status,
        IAlertStore store,
        PushoverSounds sounds,
        IClock clock,
        AlertsOptions options,
        CancellationToken cancellationToken,
        string? calendarWrite = null)
    {
        var snapshot = status.Snapshot();
        var now = clock.GetCurrentInstant();
        var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
        var plan = await AlertsPlan.CurrentAsync(snapshot, store, settings, now, cancellationToken);
        var mutedUntil = await store.MutedUntilAsync(cancellationToken) is { } until && until > now ? until : (Instant?)null;
        var skipped = await store.SkippedAsync(cancellationToken);
        var chosen = await store.EventTypesAsync(cancellationToken);
        var acknowledged = await store.AcknowledgementsAsync(cancellationToken);

        return new AlertsState(
            Configured: true,
            TimeZone: snapshot.Zone.Id,
            PollMinutes: settings.PollMinutes,
            DefaultLeadMinutes: settings.DefaultLeadMinutes,
            Settings: new SettingsView(
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
                settings.DefaultType,
                settings.BackupDelaySeconds),
            Bounds: SettingsBounds.For(await sounds.CurrentAsync(cancellationToken)),
            MutedUntil: mutedUntil?.ToDateTimeOffset(),
            LastFetchAt: snapshot.LastFetchAt?.ToDateTimeOffset(),
            LastFetchError: snapshot.LastFetchError,
            LastSuccessAt: snapshot.LastSuccessAt?.ToDateTimeOffset(),
            LastSend: snapshot.LastSend is { } send
                ? new SendView(send.At.ToDateTimeOffset(), send.Title, send.Outcome)
                : null,
            Alerts:
            [
                .. plan
                    .Where(alert => alert.StartsAt > now)
                    .Take(Listed)
                    .Select(alert =>
                    {
                        var type = AlertTypes.Resolve(alert, chosen.GetValueOrDefault(alert.EventId), settings);
                        var acknowledgement = acknowledged.GetValueOrDefault(alert.Key);
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
                            type.From,
                            acknowledgement is not null,
                            acknowledgement?.At.ToDateTimeOffset(),
                            acknowledgement?.Via);
                    })
            ],
            CalendarWrite: calendarWrite);
    }

    public sealed record MuteRequest(string? Until);

    public sealed record SkipRequest(string? Key);

    /// <summary>A listed or sent alert's key, and "phone" or "browser".</summary>
    public sealed record AckRequest(string? Key, string? Via);

    public sealed record DeviceRequest(string? Name);

    /// <summary>A paired phone as the list shows it. Never the token or its hash.</summary>
    public sealed record DeviceView(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt)
    {
        public static DeviceView From(AlertDevice device) =>
            new(device.Id, device.Name, device.CreatedAt.ToDateTimeOffset(), device.LastSeenAt?.ToDateTimeOffset());
    }

    /// <summary>The answer to a pairing, the one time the token is shown.</summary>
    public sealed record PairedDevice(Guid Id, string Name, DateTimeOffset CreatedAt, string Token, string PairUrl);

    /// <summary>
    /// A new event: a title, a start with its offset, a duration, an
    /// optional location, the type, and an optional lead. Validate names
    /// each refused field as the body spells it.
    /// </summary>
    public sealed record NewEventRequest(
        string? Title,
        string? StartsAt,
        int? DurationMinutes,
        string? Location,
        string? Type,
        int? LeadMinutes)
    {
        public static (Dictionary<string, string[]> Errors, NewAlertEvent? Valid) Validate(
            NewEventRequest request, Instant now, int defaultLeadMinutes)
        {
            var errors = new Dictionary<string, string[]>();
            var title = request.Title?.Trim() ?? "";
            if (title.Length is 0 or > CreatedEvents.MaxTitleLength || title.Any(char.IsControl))
            {
                errors["title"] = [$"1 to {CreatedEvents.MaxTitleLength} characters."];
            }

            Instant? start = null;
            if (request.StartsAt is { } text
                && NodaTime.Text.OffsetDateTimePattern.ExtendedIso.Parse(text.Trim()) is { Success: true } parsed)
            {
                start = parsed.Value.ToInstant();
            }

            if (start is not { } startsAt || startsAt <= now || startsAt > now + CreatedEvents.MaxAhead)
            {
                errors["startsAt"] = ["An ISO 8601 time with its offset, in the future and at most 366 days ahead."];
            }

            if (request.DurationMinutes is not (>= CreatedEvents.MinDurationMinutes and <= CreatedEvents.MaxDurationMinutes))
            {
                errors["durationMinutes"] = [$"{CreatedEvents.MinDurationMinutes} to {CreatedEvents.MaxDurationMinutes}."];
            }

            var location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
            if (location is not null && (location.Length > CreatedEvents.MaxLocationLength || location.Any(char.IsControl)))
            {
                errors["location"] = [$"At most {CreatedEvents.MaxLocationLength} characters."];
            }

            if (request.Type is not { } type || !AlertTypes.Choices.Contains(type))
            {
                errors["type"] = ["\"none\", \"notification\" or \"alarm\"."];
            }

            if (request.LeadMinutes is { } lead && lead is < CreatedEvents.MinLeadMinutes or > CreatedEvents.MaxLeadMinutes)
            {
                errors["leadMinutes"] = [$"{CreatedEvents.MinLeadMinutes} to {CreatedEvents.MaxLeadMinutes}."];
            }

            if (errors.Count > 0) return (errors, null);

            var begins = start!.Value;
            return (errors, new NewAlertEvent(
                title,
                location,
                begins,
                begins + Duration.FromMinutes(request.DurationMinutes!.Value),
                request.LeadMinutes ?? defaultLeadMinutes,
                request.Type!));
        }
    }

    /// <summary>A listed alert's key and the type for its event: none, notification, alarm, or default to drop the choice.</summary>
    public sealed record EventTypeRequest(string? Key, string? Type);

    /// <summary>
    /// The settings form. Every field is required: the page sends the whole
    /// form. An alarm always repeats until acknowledged, so a "priority"
    /// field in the body is ignored.
    /// </summary>
    public sealed record SettingsRequest(
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
        string? DefaultType,
        int? BackupDelaySeconds);

    /// <summary>
    /// The page's whole state. Lists its fields: the settings in force, and
    /// no secret. TimeZone is the zone in use, the calendar's own when it
    /// names one. Settings.TimeZone is the fallback the owner set.
    /// CalendarWrite is set only in the answer to a type change or a new
    /// event whose write to Google did not happen, and says why.
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
        IReadOnlyList<AlertView> Alerts,
        string? CalendarWrite = null);

    public sealed record SettingsView(
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
        string DefaultType,
        int BackupDelaySeconds);

    public sealed record Bound(int Min, int Max);

    /// <summary>
    /// What the form's inputs accept. The server checks the same numbers on
    /// save. Sounds lists the account's own uploads first, then Pushover's
    /// built-ins, and never the app token.
    /// </summary>
    public sealed record SettingsBounds(
        Bound RepeatSeconds,
        Bound StopAfterMinutes,
        Bound DefaultLeadMinutes,
        Bound PollMinutes,
        Bound LookaheadHours,
        Bound BackupDelaySeconds,
        int MaxOwnerEmails,
        int MaxEmergencySounds,
        IReadOnlyList<PushoverSound> Sounds)
    {
        public static SettingsBounds For(IReadOnlyList<PushoverSound> sounds) => new(
            new Bound(AlertSettings.MinRepeatSeconds, AlertSettings.MaxRepeatSeconds),
            new Bound(AlertSettings.MinStopAfterMinutes, AlertSettings.MaxStopAfterMinutes),
            new Bound(AlertSettings.MinDefaultLeadMinutes, AlertSettings.MaxDefaultLeadMinutes),
            new Bound(AlertSettings.MinPollMinutes, AlertSettings.MaxPollMinutes),
            new Bound(AlertSettings.MinLookaheadHours, AlertSettings.MaxLookaheadHours),
            new Bound(AlertSettings.MinBackupDelaySeconds, AlertSettings.MaxBackupDelaySeconds),
            AlertSettings.MaxOwnerEmails,
            PushoverClient.MaxEmergencySounds,
            sounds);
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
        string TypeFrom,
        bool Acknowledged,
        DateTimeOffset? AcknowledgedAt,
        string? AcknowledgedVia);
}
