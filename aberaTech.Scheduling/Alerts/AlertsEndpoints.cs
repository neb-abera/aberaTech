using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The owner's /alerts page: the next alerts, the last calendar read, the
/// settings, and Mute, Unmute, Skip, Acknowledge, each event's type, a new
/// event, an event's edit and deletion, Save settings, the phone's alarm
/// sound and snooze, three test sends, the routine alarms, the countdowns
/// and the paired phones. Plain JSON over HTTPS,
/// so it works from a locked-down work computer.
/// </summary>
/// <remarks>
/// The owner's Google sign-in reaches every route. A paired phone's token
/// reaches the seventeen a phone needs (<see cref="AlertsAuth.OwnerOrDevicePolicy"/>):
/// the status, mute, unmute, skip, unskip, ack, an event's type, a new
/// event, an event's edit and deletion, a routine alarm's create, update
/// and delete, a countdown's create, update and delete, and the phone
/// settings. The phone's own push registration takes its token alone
/// (<see cref="AlertsAuth.DevicePolicy"/>). The actions share one rate
/// limit. Every change a phone holds bumps the plan version, and
/// <see cref="AlertPushWorker"/> pushes the phones. Every action answers with the page's whole state, so the page and
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

        // A paired phone's token alone.
        var device = routes
            .MapGroup("/api/alerts")
            .RequireAuthorization(AlertsAuth.DevicePolicy)
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
            MuteRequest request, AlertPushWorker pushes, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
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
            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPost("/unmute", async (
            AlertPushWorker pushes, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            await store.SetMutedUntilAsync(null, clock.GetCurrentInstant(), cancellationToken);
            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPost("/skip", async (
            SkipRequest request, AlertPushWorker pushes, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            // Only what is on the list: a skip is for an alert the owner can see.
            var alert = await FindAsync(request.Key!, status, store, clock, options, cancellationToken);
            if (alert is null) return Results.NotFound();

            await store.SkipAsync(alert.Key, alert.StartsAt, clock.GetCurrentInstant(), cancellationToken);
            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPost("/unskip", async (
            SkipRequest request, AlertPushWorker pushes, AlertsStatus status, PushoverSounds sounds, IAlertStore store, IClock clock, CancellationToken cancellationToken) =>
        {
            if (!Valid(request.Key)) return Results.BadRequest("key is required");

            await store.UnskipAsync(request.Key!, cancellationToken);
            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        group.MapPut("/settings", async (
            SettingsRequest request,
            CalendarAlertWorker worker,
            AlertPushWorker pushes,
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
            foreach (var (field, messages) in AlertSettings.ValidatePhone(
                         request.PhoneSound, request.PhoneSnoozeMinutes, "phoneSound", "phoneSnoozeMinutes"))
            {
                errors[field] = messages;
            }

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
                request.BackupDelaySeconds!.Value,
                request.PhoneSound!,
                request.PhoneSnoozeMinutes!.Value);
            await store.SaveSettingsAsync(settings, clock.GetCurrentInstant(), cancellationToken);

            // The phones hold the sound and the snooze, so a change to
            // either pushes them, as a phone settings save does.
            if (PhoneChanged(saved, settings)) await pushes.PlanChangedAsync(cancellationToken);

            // This replica plans with the new values now. The others read
            // the row at the start of their next pass.
            await worker.ReadNowAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // The phone's alarm sound and snooze, from the page or the phone.
        // Every other setting is left as saved. Only a change pushes the
        // phones: the same values again change nothing a phone holds.
        shared.MapPut("/phone-settings", async (
            PhoneSettingsRequest request,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var errors = AlertSettings.ValidatePhone(request.Sound, request.SnoozeMinutes, "sound", "snoozeMinutes");
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var saved = await AlertSettings.CurrentAsync(store, options, cancellationToken);
            var settings = saved with { PhoneSound = request.Sound!, PhoneSnoozeMinutes = request.SnoozeMinutes!.Value };
            if (PhoneChanged(saved, settings))
            {
                await store.SaveSettingsAsync(settings, clock.GetCurrentInstant(), cancellationToken);
                await pushes.PlanChangedAsync(cancellationToken);
            }

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
            AlertPushWorker pushes,
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
            await pushes.PlanChangedAsync(cancellationToken);
            var written = await google.SetMarkAsync(alert.EventId, request.Type == AlertTypes.Alarm, cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken, written));
        }).RequireRateLimiting(ActionsPolicy);

        // Acknowledge: the phone's alarm or the browser's ring was answered.
        // An occurrence on the list, or one that was sent and has since
        // dropped off it, so a phone that answers late still counts. The
        // first acknowledgement stands, and it cancels Pushover's repeats.
        shared.MapPost("/ack", async (
            AckRequest request,
            AlertPushWorker pushes,
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
                await pushes.PlanChangedAsync(cancellationToken);
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
            AlertPushWorker pushes,
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
            await pushes.PlanChangedAsync(cancellationToken);

            // This replica plans it on its next pass, now rather than at the
            // end of the wait.
            worker.Wake();
            return Results.Created((string?)null, await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // An edit of one listed occurrence, or of every occurrence of its
        // series, on the calendar the feed reads. The key travels in the
        // body alone: it holds the event's UID. Kept here until the feed
        // shows it, so the list and the alerts follow it at once
        // (EventChanges). The description, #critical and the stored type
        // are left as they are.
        shared.MapPut("/events", async (
            EditEventRequest request,
            CalendarAlertWorker worker,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            GoogleAlertEvents google,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var now = clock.GetCurrentInstant();
            var (errors, edit) = EditEventRequest.Validate(request, now);
            if (edit is null) return Results.ValidationProblem(errors);

            var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
            var snapshot = status.Snapshot();
            var before = await AlertsPlan.CurrentAsync(snapshot, store, settings, now, cancellationToken);
            var alert = before.FirstOrDefault(planned => planned.Key == request.Key);
            if (alert is null) return Results.NotFound();

            var outcome = await google.EditAsync(
                alert, request.Scope == EventScopes.Series && alert.Recurring, edit, snapshot.Zone, cancellationToken);
            if (outcome.Problem is not null) return WriteProblem(outcome);

            var zone = outcome.Series && outcome.TimeZone is { } named
                ? DateTimeZoneProviders.Tzdb.GetZoneOrNull(named) ?? snapshot.Zone
                : snapshot.Zone;
            var change = AlertEventChange.Edit(
                Guid.NewGuid(), alert, outcome.Series, edit, outcome.Series ? outcome.TimeZone : null, zone, now);
            await store.AddEventChangeAsync(change, cancellationToken);

            // A created event the feed does not have yet is planned from its
            // row, which takes the edit too.
            var created = await store.CreatedEventsAsync(cancellationToken);
            if (created.FirstOrDefault(row => AlertPlanner.EventIdFor(row.EventId) == alert.EventId) is { } row)
            {
                var start = edit.StartsAt;
                await store.UpdateCreatedEventAsync(
                    row with
                    {
                        Title = edit.Title,
                        Location = edit.LocationSet ? edit.Location : row.Location,
                        StartsAt = start,
                        EndsAt = start + (edit.DurationMinutes is { } minutes ? Duration.FromMinutes(minutes) : row.EndsAt - row.StartsAt),
                        LeadMinutes = edit.LeadMinutes ?? row.LeadMinutes
                    },
                    cancellationToken);
            }

            await EventMoves.CarryAsync(before, change, zone, store, now, cancellationToken);

            await pushes.PlanChangedAsync(cancellationToken);
            worker.Wake();
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // A deletion of one listed occurrence, or of the whole event. The
        // key travels in the body alone. Kept here until the feed no longer
        // lists it, so it is gone from the list and never alerts. A repeat
        // Pushover is still making for it is cancelled.
        shared.MapPost("/events/delete", async (
            DeleteEventRequest request,
            AlertDispatcher dispatcher,
            CalendarAlertWorker worker,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            GoogleAlertEvents google,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!Valid(request.Key)) errors["key"] = ["Required, at most 200 characters."];
            if (request.Scope is not (EventScopes.Occurrence or EventScopes.Series)) errors["scope"] = [EventScopes.Message];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var now = clock.GetCurrentInstant();
            var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
            var plan = await AlertsPlan.CurrentAsync(status.Snapshot(), store, settings, now, cancellationToken);
            var alert = plan.FirstOrDefault(planned => planned.Key == request.Key);
            if (alert is null) return Results.NotFound();

            var outcome = await google.DeleteAsync(alert, request.Scope == EventScopes.Series && alert.Recurring, cancellationToken);
            if (outcome.Problem is not null) return WriteProblem(outcome);

            await store.AddEventChangeAsync(AlertEventChange.Deletion(Guid.NewGuid(), alert, outcome.Series, now), cancellationToken);
            var created = await store.CreatedEventsAsync(cancellationToken);
            var pending = created.Where(row => AlertPlanner.EventIdFor(row.EventId) == alert.EventId).Select(row => row.EventId).ToList();
            if (pending.Count > 0) await store.ForgetCreatedEventsAsync(pending, cancellationToken);

            await pushes.PlanChangedAsync(cancellationToken);
            var gone = outcome.Series ? plan.Where(planned => planned.EventId == alert.EventId).Select(planned => planned.Key) : [alert.Key];
            foreach (var key in gone) await dispatcher.CancelRepeatsAsync(key, cancellationToken);

            worker.Wake();
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // Routine alarms: the phone rings them as phone alarms, never
        // through Pushover or a browser. The server stores them and fires
        // nothing. Each change pushes the phones, like any change they hold.
        shared.MapPost("/routines", async (
            RoutineRequest request,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var (errors, routine) = AlertRoutines.Validate(Guid.NewGuid(), request, whole: false, clock.GetCurrentInstant());
            if (routine is null) return Results.ValidationProblem(errors);

            if (!await store.AddRoutineAsync(routine, cancellationToken))
            {
                return Results.Problem(
                    detail: $"At most {AlertRoutines.MaxRoutines} routine alarms. Delete one first.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Created(
                $"/api/alerts/routines/{routine.Id}", await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPut("/routines/{id:guid}", async (
            Guid id,
            RoutineRequest request,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var (errors, routine) = AlertRoutines.Validate(id, request, whole: true, clock.GetCurrentInstant());
            if (routine is null) return Results.ValidationProblem(errors);
            if (!await store.UpdateRoutineAsync(routine, cancellationToken)) return Results.NotFound();

            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapDelete("/routines/{id:guid}", async (
            Guid id,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!await store.DeleteRoutineAsync(id, cancellationToken)) return Results.NotFound();

            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        // Countdowns: /dates and the phone show the time left. The server
        // stores them and fires nothing. Each change pushes the phones.
        shared.MapPost("/countdowns", async (
            CountdownRequest request,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var (errors, countdown) = AlertCountdowns.Validate(Guid.NewGuid(), request, clock.GetCurrentInstant());
            if (countdown is null) return Results.ValidationProblem(errors);

            if (!await store.AddCountdownAsync(countdown, cancellationToken))
            {
                return Results.Problem(
                    detail: $"At most {AlertCountdowns.MaxCountdowns} countdowns. Delete one first.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Created(
                $"/api/alerts/countdowns/{countdown.Id}", await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapPut("/countdowns/{id:guid}", async (
            Guid id,
            CountdownRequest request,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var (errors, countdown) = AlertCountdowns.Validate(id, request, clock.GetCurrentInstant());
            if (countdown is null) return Results.ValidationProblem(errors);
            if (!await store.UpdateCountdownAsync(countdown, cancellationToken)) return Results.NotFound();

            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
        }).RequireRateLimiting(ActionsPolicy);

        shared.MapDelete("/countdowns/{id:guid}", async (
            Guid id,
            AlertPushWorker pushes,
            AlertsStatus status,
            PushoverSounds sounds,
            IAlertStore store,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!await store.DeleteCountdownAsync(id, cancellationToken)) return Results.NotFound();

            await pushes.PlanChangedAsync(cancellationToken);
            return Results.Ok(await StateAsync(status, store, sounds, clock, options, cancellationToken));
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

        // The calling phone's push token, from Apple, and which of Apple's
        // servers it belongs to. A new one replaces the old. The token is
        // never answered, listed or logged: the list says only whether a
        // phone has one.
        device.MapPut("/devices/me/push", async (
            PushRequest request, HttpContext context, IAlertDeviceStore devices, CancellationToken cancellationToken) =>
        {
            var errors = ApnsPushTokens.Validate(request.ApnsToken, request.Environment);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (AlertsAuth.DeviceId(context.User) is not { } id) return Results.Forbid();

            await devices.SetPushAsync(id, request.ApnsToken, request.Environment, cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting(ActionsPolicy);

        device.MapDelete("/devices/me/push", async (
            HttpContext context, IAlertDeviceStore devices, CancellationToken cancellationToken) =>
        {
            if (AlertsAuth.DeviceId(context.User) is not { } id) return Results.Forbid();

            await devices.SetPushAsync(id, null, null, cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting(ActionsPolicy);

        // Test: ring until stopped. An alarm, with the alarm settings.
        group.MapPost("/test", async (AlertDispatcher dispatcher, CancellationToken cancellationToken) =>
            Answer(await dispatcher.SendTestAsync(AlertTypes.Alarm, cancellationToken))).RequireRateLimiting(ActionsPolicy);

        // Test: ring once. One sound, with the notification settings.
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
                // Edits and deletions an earlier run made would still hide or
                // move the standing events.
                var changes = await store.EventChangesAsync(cancellationToken);
                await store.ForgetEventChangesAsync([.. changes.Select(change => change.Id)], cancellationToken);
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

            // The pushes the fake Apple took, oldest first. Never a token.
            group.MapGet("/fake/pushes", (FakeAlertServices fake) => Results.Ok(fake.Pushes));

            // The receipts whose repeats an acknowledgement cancelled.
            group.MapGet("/fake/cancelled", (FakeAlertServices fake) => Results.Ok(fake.Cancelled));

            // The last message the fake Pushover took, so the browser suite
            // can see what Test: ring until stopped asked for.
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

    /// <summary>An edit or a deletion Google did not take: 409 for a refusal the owner can fix, 502 for the rest.</summary>
    private static IResult WriteProblem(EventWriteOutcome outcome) =>
        Results.Problem(
            detail: outcome.Problem,
            statusCode: outcome.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status502BadGateway);

    private static bool PhoneChanged(AlertSettings before, AlertSettings after) =>
        before.PhoneSound != after.PhoneSound || before.PhoneSnoozeMinutes != after.PhoneSnoozeMinutes;

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
                settings.BackupDelaySeconds,
                settings.PhoneSound,
                settings.PhoneSnoozeMinutes),
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
                            acknowledgement?.Via,
                            alert.Recurring,
                            alert.EndsAt?.ToDateTimeOffset());
                    })
            ],
            Push: new PushView(options.ApnsMissing().Count == 0, options.ApnsMissing()),
            Routines: [.. (await store.RoutinesAsync(cancellationToken)).Select(RoutineView.From)],
            Countdowns: [.. (await store.CountdownsAsync(cancellationToken)).Select(CountdownView.From)],
            CalendarWrite: calendarWrite);
    }

    public sealed record MuteRequest(string? Until);

    public sealed record SkipRequest(string? Key);

    /// <summary>A listed or sent alert's key, and "phone" or "browser".</summary>
    public sealed record AckRequest(string? Key, string? Via);

    public sealed record DeviceRequest(string? Name);

    /// <summary>The phone's alarm sound, one of <see cref="AlertSettings.PhoneSounds"/>, and its snooze, 1 to 30 minutes. Both required.</summary>
    public sealed record PhoneSettingsRequest(string? Sound, int? SnoozeMinutes);

    /// <summary>A paired phone as the list shows it. Never the token, its hash or the push token.</summary>
    /// <param name="Push">True when the phone has registered a push token.</param>
    public sealed record DeviceView(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, bool Push)
    {
        public static DeviceView From(AlertDevice device) =>
            new(device.Id, device.Name, device.CreatedAt.ToDateTimeOffset(), device.LastSeenAt?.ToDateTimeOffset(), device.Push);
    }

    /// <summary>A phone's push registration: Apple's token in lowercase hex, and "sandbox" or "production".</summary>
    public sealed record PushRequest(string? ApnsToken, string? Environment);

    /// <summary>Whether phone pushes go, and the names of the secrets that are missing when they do not. Never a value.</summary>
    public sealed record PushView(bool On, IReadOnlyList<string> Missing);

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
            var title = EventFields.Title(request.Title, errors);
            var start = EventFields.Start(request.StartsAt, now, errors);
            if (request.DurationMinutes is not (>= CreatedEvents.MinDurationMinutes and <= CreatedEvents.MaxDurationMinutes))
            {
                errors["durationMinutes"] = [$"{CreatedEvents.MinDurationMinutes} to {CreatedEvents.MaxDurationMinutes}."];
            }

            var location = EventFields.Location(request.Location, errors);
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
                title!,
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
        int? BackupDelaySeconds,
        string? PhoneSound,
        int? PhoneSnoozeMinutes);

    /// <summary>
    /// The page's whole state. Lists its fields: the settings in force, and
    /// no secret. TimeZone is the zone in use, the calendar's own when it
    /// names one. Settings.TimeZone is the fallback the owner set.
    /// CalendarWrite is set only in the answer to a type change or a new
    /// event whose write to Google did not happen, and says why. Routines
    /// lists every routine alarm by hour, minute and label. Countdowns lists
    /// every countdown by target and label.
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
        PushView Push,
        IReadOnlyList<RoutineView> Routines,
        IReadOnlyList<CountdownView> Countdowns,
        string? CalendarWrite = null);

    /// <summary>
    /// A countdown as the page and the phone read it. TargetAt is the
    /// instant with its offset. TimeZone is the zone its date is written in.
    /// </summary>
    public sealed record CountdownView(
        Guid Id,
        string Label,
        DateTimeOffset TargetAt,
        string TimeZone,
        DateTimeOffset UpdatedAt)
    {
        public static CountdownView From(AlertCountdown countdown) => new(
            countdown.Id,
            countdown.Label,
            countdown.TargetAt.ToDateTimeOffset(),
            countdown.TimeZone,
            countdown.UpdatedAt.ToDateTimeOffset());
    }

    /// <summary>
    /// A routine alarm as the page and the phone read it. Hour and minute
    /// are wall-clock time. Days are ISO weekdays, Monday 1, sorted, and
    /// empty rings once.
    /// </summary>
    public sealed record RoutineView(
        Guid Id,
        string Label,
        int Hour,
        int Minute,
        IReadOnlyList<int> Days,
        bool Enabled,
        int SnoozeMinutes,
        DateTimeOffset UpdatedAt)
    {
        public static RoutineView From(AlertRoutine routine) => new(
            routine.Id,
            routine.Label,
            routine.Hour,
            routine.Minute,
            routine.Days,
            routine.Enabled,
            routine.SnoozeMinutes,
            routine.UpdatedAt.ToDateTimeOffset());
    }

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
        int BackupDelaySeconds,
        string PhoneSound,
        int PhoneSnoozeMinutes);

    public sealed record Bound(int Min, int Max);

    /// <summary>
    /// What the form's inputs accept. The server checks the same numbers on
    /// save. Sounds lists the account's own uploads first, then Pushover's
    /// built-ins, and never the app token. PhoneSounds lists the sounds the
    /// phone app carries.
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
        IReadOnlyList<PushoverSound> Sounds,
        IReadOnlyList<PhoneSoundChoice> PhoneSounds,
        Bound PhoneSnoozeMinutes)
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
            sounds,
            AlertSettings.PhoneSounds,
            new Bound(AlertSettings.MinPhoneSnoozeMinutes, AlertSettings.MaxPhoneSnoozeMinutes));
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
        string? AcknowledgedVia,
        bool Recurring,
        DateTimeOffset? EndsAt);

    /// <summary>
    /// An edit of a listed occurrence. Key, scope, title and startsAt are
    /// required. A missing or null durationMinutes keeps the event's length,
    /// and a missing or null leadMinutes leaves its reminders. A missing
    /// location leaves it, and null or blank clears it.
    /// </summary>
    public sealed class EditEventRequest
    {
        private readonly string? _location;

        public string? Key { get; init; }

        public string? Scope { get; init; }

        public string? Title { get; init; }

        public string? StartsAt { get; init; }

        public int? DurationMinutes { get; init; }

        public string? Location
        {
            get => _location;
            init
            {
                _location = value;
                LocationGiven = true;
            }
        }

        /// <summary>The body carried a location, null included. Set by the serializer calling the setter.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool LocationGiven { get; private init; }

        public int? LeadMinutes { get; init; }

        public static (Dictionary<string, string[]> Errors, AlertEventEdit? Valid) Validate(EditEventRequest request, Instant now)
        {
            var errors = new Dictionary<string, string[]>();
            if (!Valid(request.Key)) errors["key"] = ["Required, at most 200 characters."];
            if (request.Scope is not (EventScopes.Occurrence or EventScopes.Series)) errors["scope"] = [EventScopes.Message];

            var title = EventFields.Title(request.Title, errors);
            var start = EventFields.Start(request.StartsAt, now, errors);
            if (request.DurationMinutes is { } duration
                && duration is < CreatedEvents.MinDurationMinutes or > CreatedEvents.MaxDurationMinutes)
            {
                errors["durationMinutes"] = [$"{CreatedEvents.MinDurationMinutes} to {CreatedEvents.MaxDurationMinutes}, or left out to keep the length."];
            }

            var location = EventFields.Location(request.Location, errors);
            if (request.LeadMinutes is { } lead && lead is < CreatedEvents.MinLeadMinutes or > CreatedEvents.MaxLeadMinutes)
            {
                errors["leadMinutes"] = [$"{CreatedEvents.MinLeadMinutes} to {CreatedEvents.MaxLeadMinutes}."];
            }

            return errors.Count > 0
                ? (errors, null)
                : (errors, new AlertEventEdit(title!, request.LocationGiven, location, start!.Value, request.DurationMinutes, request.LeadMinutes));
        }
    }

    /// <summary>A deletion of a listed occurrence: its key, and "occurrence" or "series".</summary>
    public sealed record DeleteEventRequest(string? Key, string? Scope);
}

/// <summary>Which occurrences an edit or a deletion is for.</summary>
public static class EventScopes
{
    /// <summary>The one listed occurrence. For an event that does not repeat, the event.</summary>
    public const string Occurrence = "occurrence";

    /// <summary>Every occurrence: the series' master in Google.</summary>
    public const string Series = "series";

    public const string Message = "\"occurrence\" or \"series\".";
}

/// <summary>The checks a new event and an edit share, each naming its field as the body spells it.</summary>
public static class EventFields
{
    public static string? Title(string? text, Dictionary<string, string[]> errors)
    {
        var title = text?.Trim() ?? "";
        if (title.Length is 0 or > CreatedEvents.MaxTitleLength || title.Any(char.IsControl))
        {
            errors["title"] = [$"1 to {CreatedEvents.MaxTitleLength} characters."];
            return null;
        }

        return title;
    }

    public static Instant? Start(string? text, Instant now, Dictionary<string, string[]> errors)
    {
        Instant? start = null;
        if (text is not null && NodaTime.Text.OffsetDateTimePattern.ExtendedIso.Parse(text.Trim()) is { Success: true } parsed)
        {
            start = parsed.Value.ToInstant();
        }

        if (start is not { } startsAt || startsAt <= now || startsAt > now + CreatedEvents.MaxAhead)
        {
            errors["startsAt"] = ["An ISO 8601 time with its offset, in the future and at most 366 days ahead."];
            return null;
        }

        return start;
    }

    /// <summary>Null for none. Blank is none.</summary>
    public static string? Location(string? text, Dictionary<string, string[]> errors)
    {
        var location = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (location is not null && (location.Length > CreatedEvents.MaxLocationLength || location.Any(char.IsControl)))
        {
            errors["location"] = [$"At most {CreatedEvents.MaxLocationLength} characters."];
        }

        return location;
    }
}
