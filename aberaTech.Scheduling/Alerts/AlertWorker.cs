using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>The last thing sent, or tried: when, what, and how it went.</summary>
public sealed record LastSend(Instant At, string Title, string Outcome);

/// <summary>What the page shows about the worker. One copy per process.</summary>
public sealed record AlertsSnapshot(
    IReadOnlyList<PlannedAlert> Plan,
    DateTimeZone Zone,
    Instant? LastFetchAt,
    string? LastFetchError,
    Instant? LastSuccessAt,
    LastSend? LastSend);

/// <summary>
/// The worker's state in memory: the list from the last good read, and how
/// the last read and the last send went. Mute, skip and the send claims are
/// in the database; this is only what a restart can afford to lose, because
/// the next read is at most one poll away.
/// </summary>
public sealed class AlertsStatus(AlertsOptions options)
{
    private readonly Lock _lock = new();
    private AlertsSnapshot _snapshot = new([], AlertSettings.Defaults(options).FallbackZone(), null, null, null, null);

    public AlertsSnapshot Snapshot()
    {
        lock (_lock) return _snapshot;
    }

    public void Planned(CalendarPlan plan, Instant at)
    {
        lock (_lock)
        {
            _snapshot = _snapshot with
            {
                Plan = plan.Alerts, Zone = plan.Zone, LastFetchAt = at, LastFetchError = null, LastSuccessAt = at
            };
        }
    }

    public void FetchFailed(string error, Instant at)
    {
        lock (_lock) _snapshot = _snapshot with { LastFetchAt = at, LastFetchError = error };
    }

    public void Sent(LastSend send)
    {
        lock (_lock) _snapshot = _snapshot with { LastSend = send };
    }
}

/// <summary>How one due alert ended.</summary>
public enum DeliveryOutcome
{
    Sent,
    Failed,
    Skipped,
    Muted,
    AlreadyClaimed,
    Started,

    /// <summary>
    /// The event's type is none. Nothing is claimed or recorded, so a
    /// change to Notification or Alarm before the start still sends.
    /// </summary>
    Off
}

/// <summary>
/// Sends one due alert, after the checks that must be read at that moment
/// rather than at the last calendar read: skipped, muted, the event's type,
/// already claimed. The settings are read then too, so a save changes the
/// next send.
/// </summary>
public sealed class AlertDispatcher(
    IServiceScopeFactory scopes,
    AlertsStatus status,
    AlertsOptions options,
    IClock clock,
    ILogger<AlertDispatcher> logger)
{
    public async Task<DeliveryOutcome> DeliverAsync(PlannedAlert alert, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAlertStore>();

        var now = clock.GetCurrentInstant();
        if (alert.StartsAt <= now) return DeliveryOutcome.Started;
        if (await store.IsSkippedAsync(alert.Key, cancellationToken)) return DeliveryOutcome.Skipped;
        if (await store.MutedUntilAsync(cancellationToken) is { } until && until > now) return DeliveryOutcome.Muted;
        var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
        var type = AlertTypes.Resolve(alert, await store.EventTypeAsync(alert.EventId, cancellationToken), settings);
        if (settings.DeliveryFor(type.Type) is not { } delivery) return DeliveryOutcome.Off;
        if (!await store.TryClaimAsync(alert.Key, alert.StartsAt, now, cancellationToken)) return DeliveryOutcome.AlreadyClaimed;

        var pushover = scope.ServiceProvider.GetRequiredService<PushoverClient>();
        var result = await pushover.SendAsync(
            alert.Title,
            AlertText.Message(alert, status.Snapshot().Zone),
            delivery,
            cancellationToken);

        var done = clock.GetCurrentInstant();
        await store.RecordOutcomeAsync(alert.Key, result.Outcome, done, cancellationToken);
        status.Sent(new LastSend(done, alert.Title, result.Outcome));

        // The event's start and the outcome. Never its title: the log leaves
        // this process for Application Insights.
        if (result.Ok)
        {
            logger.LogInformation("Calendar alert sent for an event starting at {StartsAt}.", alert.StartsAt);
            return DeliveryOutcome.Sent;
        }

        logger.LogWarning("Calendar alert for an event starting at {StartsAt} failed ({Failure}).", alert.StartsAt, result.Error);
        return DeliveryOutcome.Failed;
    }

    /// <summary>
    /// The page's two test buttons: Send test alert goes as an alarm, Send
    /// test notification as a notification. Not deduplicated and not muted:
    /// pressing one is the owner asking. Each goes with the saved settings
    /// for its type, so it shows what an event of that type will do.
    /// </summary>
    public async Task<PushoverResult> SendTestAsync(string type, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var settings = await AlertSettings.CurrentAsync(
            scope.ServiceProvider.GetRequiredService<IAlertStore>(), options, cancellationToken);
        var delivery = settings.DeliveryFor(type)
                       ?? throw new ArgumentOutOfRangeException(nameof(type), type, "A test is an alarm or a notification.");
        var pushover = scope.ServiceProvider.GetRequiredService<PushoverClient>();
        var now = clock.GetCurrentInstant();
        var name = AlertText.TestName(type);

        var result = await pushover.SendAsync(
            name, AlertText.TestMessage(now, status.Snapshot().Zone, type, delivery), delivery, cancellationToken);

        status.Sent(new LastSend(clock.GetCurrentInstant(), name, result.Outcome));
        if (!result.Ok) logger.LogWarning("{Test} failed ({Failure}).", name, result.Error);
        return result;
    }

    /// <summary>
    /// Send test on one listed alert: that event's own text, sent as its
    /// type with that type's saved settings, titled "Test: ". Null when the
    /// type is none: there is nothing to test. Nothing is claimed and mute
    /// and skip are not read, so the real alert still goes at its time.
    /// </summary>
    public async Task<PushoverResult?> SendEventTestAsync(PlannedAlert alert, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAlertStore>();
        var settings = await AlertSettings.CurrentAsync(store, options, cancellationToken);
        var type = AlertTypes.Resolve(alert, await store.EventTypeAsync(alert.EventId, cancellationToken), settings);
        if (settings.DeliveryFor(type.Type) is not { } delivery) return null;
        var pushover = scope.ServiceProvider.GetRequiredService<PushoverClient>();
        var title = AlertText.TestTitle(alert.Title);

        var result = await pushover.SendAsync(
            title, AlertText.Message(alert, status.Snapshot().Zone), delivery, cancellationToken);

        status.Sent(new LastSend(clock.GetCurrentInstant(), title, result.Outcome));
        if (!result.Ok) logger.LogWarning("Test of the alert for an event starting at {StartsAt} failed ({Failure}).", alert.StartsAt, result.Error);
        return result;
    }
}

/// <summary>
/// Reads the calendar every <see cref="AlertSettings.PollMinutes"/> and sends
/// each alert at its own time from the list in memory. A reminder at 08:47
/// goes at 08:47, not at the next read.
/// </summary>
/// <remarks>
/// The settings are read at the start of every pass. A saved change to
/// what the plan depends on (lead, look-ahead, all-day, zone, addresses)
/// reads the calendar again in that pass, on every replica.
///
/// Every replica runs this. The claim in <see cref="IAlertStore"/> decides
/// which one sends, so two replicas, or one restarted mid-minute, send once.
/// An alert whose time passed while the process was down still goes if the
/// event has not started.
/// </remarks>
public sealed class CalendarAlertWorker(
    IServiceScopeFactory scopes,
    AlertDispatcher dispatcher,
    AlertsStatus status,
    AlertsOptions options,
    IClock clock,
    ILogger<CalendarAlertWorker> logger) : BackgroundService
{
    /// <summary>How long a send that could not reach the database waits before it is tried again.</summary>
    public static readonly Duration RetryAfter = Duration.FromSeconds(30);

    /// <summary>Claims and skips for events this long past are forgotten.</summary>
    public static readonly Duration KeepFor = Duration.FromDays(14);

    /// <summary>
    /// A type chosen for an event that has not been in the feed for this long
    /// is forgotten. Long enough that a feed read that briefly drops an event
    /// does not lose the choice. Short enough that deleted events do not pile up.
    /// </summary>
    public static readonly Duration KeepChoicesFor = Duration.FromDays(60);

    private readonly SemaphoreSlim _tick = new(1, 1);
    private readonly HashSet<string> _handled = new(StringComparer.Ordinal);
    private Instant? _lastRead;
    private string? _plannedWith;
    private AlertSettings _settings = AlertSettings.Defaults(options);

    /// <summary>
    /// One pass: read the calendar if a read is due, send whatever is due,
    /// and say when to come back.
    /// </summary>
    public Task<Instant> TickAsync(CancellationToken cancellationToken) => TickAsync(readNow: false, cancellationToken);

    /// <summary>
    /// A pass that reads the calendar whether or not a read is due. For the
    /// development calendar's reset, which moves every event at once.
    /// </summary>
    public Task<Instant> ReadNowAsync(CancellationToken cancellationToken) => TickAsync(readNow: true, cancellationToken);

    private async Task<Instant> TickAsync(bool readNow, CancellationToken cancellationToken)
    {
        await _tick.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetCurrentInstant();
            var settings = await SettingsAsync(cancellationToken);
            if (readNow || _lastRead is null || now >= _lastRead + settings.Poll || _plannedWith != settings.PlanKey)
            {
                await ReadAsync(now, settings, cancellationToken);
                _lastRead = now;
            }

            Instant? retry = null;
            var plan = status.Snapshot().Plan;
            foreach (var alert in plan)
            {
                now = clock.GetCurrentInstant();
                if (alert.AlertAt > now || alert.StartsAt <= now || _handled.Contains(alert.Key)) continue;

                try
                {
                    // An event set to send nothing is asked again next pass:
                    // switched on before its start, it still goes.
                    if (await dispatcher.DeliverAsync(alert, cancellationToken) != DeliveryOutcome.Off) _handled.Add(alert.Key);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Nothing was claimed, or the claim is ours and stays:
                    // either way a later pass decides again.
                    logger.LogWarning("A calendar alert could not be sent ({Failure}); trying again shortly.", exception.GetType().Name);
                    retry = now + RetryAfter;
                }
            }

            now = clock.GetCurrentInstant();
            var next = _lastRead.Value + settings.Poll;
            foreach (var alert in plan)
            {
                if (alert.AlertAt > now && alert.AlertAt < next && !_handled.Contains(alert.Key)) next = alert.AlertAt;
            }

            return retry is { } at && at < next ? at : next;
        }
        finally
        {
            _tick.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Instant next;
            try
            {
                next = await TickAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError("The calendar alert pass failed ({Failure}).", exception.GetType().Name);
                next = clock.GetCurrentInstant() + RetryAfter;
            }

            var wait = next - clock.GetCurrentInstant();
            if (wait < Duration.Zero) wait = Duration.Zero;
            if (wait > _settings.Poll) wait = _settings.Poll;

            try
            {
                await Task.Delay(wait.ToTimeSpan(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>The saved settings, or the last ones read when the database does not answer.</summary>
    private async Task<AlertSettings> SettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            _settings = await AlertSettings.CurrentAsync(
                scope.ServiceProvider.GetRequiredService<IAlertStore>(), options, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Reading the alert settings failed ({Failure}). The last ones stay in use.", exception.GetType().Name);
        }

        return _settings;
    }

    private async Task ReadAsync(Instant now, AlertSettings settings, CancellationToken cancellationToken)
    {
        _plannedWith = settings.PlanKey;
        await using var scope = scopes.CreateAsyncScope();
        CalendarPlan plan;
        try
        {
            var ics = await scope.ServiceProvider.GetRequiredService<CalendarFeed>().FetchAsync(cancellationToken);
            plan = AlertPlanner.Plan(ics, now, settings);
            status.Planned(plan, now);

            var keys = plan.Alerts.Select(alert => alert.Key).ToHashSet(StringComparer.Ordinal);
            _handled.IntersectWith(keys);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The message only when it is ours: an HttpRequestException can
            // carry the address, which is the secret.
            var reason = exception is CalendarFeedException ? exception.Message : exception.GetType().Name;
            status.FetchFailed(reason, now);
            logger.LogWarning("Reading the calendar failed ({Failure}). The last list stays in use.", reason);
            return;
        }

        try
        {
            var store = scope.ServiceProvider.GetRequiredService<IAlertStore>();
            await store.PruneAsync(now - KeepFor, cancellationToken);
            await store.SeenEventsAsync(plan.EventIds, now, now - KeepChoicesFor, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Pruning old calendar alert rows failed ({Failure}).", exception.GetType().Name);
        }
    }
}

public static class AlertsRegistration
{
    /// <summary>
    /// The worker, the send path and the two HTTP clients. The caller
    /// registers <see cref="AlertsOptions"/>, an <see cref="IClock"/> and an
    /// <see cref="IAlertStore"/>.
    /// </summary>
    public static IServiceCollection AddCalendarAlerts(this IServiceCollection services)
    {
        services.AddSingleton<AlertsStatus>();
        services.AddSingleton<AlertDispatcher>();
        services.AddSingleton<CalendarAlertWorker>();
        services.AddHostedService(provider => provider.GetRequiredService<CalendarAlertWorker>());

        // No request logging on either client: the default logger writes the
        // full URL, and the calendar's URL is its secret.
        services.AddHttpClient<CalendarFeed>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllLoggers();
        services.AddHttpClient<PushoverClient>(client => client.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
        return services;
    }
}
