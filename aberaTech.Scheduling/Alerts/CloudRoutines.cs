using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using aberaTech.Scheduling.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// The owner's Claude cloud routines, and when each one runs on his clock.
/// </summary>
/// <remarks>
/// A routine's own schedule holds one fixed zone, and nothing outside a
/// Claude session can change it. On 2026-11-26 the owner was in Kansas City
/// and six routines still ran on Eastern Time. So the site is the clock: it
/// knows the zone his phone reports, and it starts each routine through the
/// routine's API trigger at the local time written here.
/// </remarks>
public sealed class CloudRoutinesOptions
{
    public const string Section = "CloudRoutines";

    public const int MaxRoutines = 12;

    public const int MaxTimes = 48;

    /// <summary>The routine's API trigger. {0} is the routine's id.</summary>
    public string FireUrl { get; init; } = "https://api.anthropic.com/v1/claude_code/routines/{0}/fire";

    /// <summary>How long after its minute a run may still start. A restart during a deploy takes about two.</summary>
    public int GraceMinutes { get; init; } = 20;

    public List<CloudRoutineOptions> Routines { get; init; } = [];

    /// <summary>
    /// The routines, checked. Throws on the first bad value and names its
    /// setting, never the value: one of them is a token.
    /// </summary>
    public IReadOnlyList<CloudRoutine> Parsed()
    {
        if (Routines.Count > MaxRoutines) throw Bad("Routines", $"holds more than {MaxRoutines} routines");
        if (GraceMinutes is < 1 or > 120) throw Bad("GraceMinutes", "is not 1 to 120");
        if (Routines.Count > 0
            && !(Uri.TryCreate(string.Format(CultureInfo.InvariantCulture, FireUrl, "trig_x"), UriKind.Absolute, out var url)
                 && url.Scheme == Uri.UriSchemeHttps))
        {
            throw Bad("FireUrl", "is not an https address");
        }

        var routines = new List<CloudRoutine>();
        for (var index = 0; index < Routines.Count; index++)
        {
            var routine = Routines[index];
            var at = $"Routines__{index}__";
            if (!CloudRoutine.NamePattern().IsMatch(routine.Name ?? "")) throw Bad(at + "Name", "is not 1 to 48 of a-z, 0-9 and -");
            if (routines.Any(other => other.Name == routine.Name)) throw Bad(at + "Name", "repeats another routine's name");
            if (!CloudRoutine.IdPattern().IsMatch(routine.Id ?? "")) throw Bad(at + "Id", "is not a routine id (trig_...)");
            if (string.IsNullOrWhiteSpace(routine.Token)) throw Bad(at + "Token", "is empty");
            var times = CloudRoutineSchedule.Times(routine.At) ?? throw Bad(at + "At", "is not times such as 05:30 or 06:15-20:15/120");
            if (times.Count > MaxTimes) throw Bad(at + "At", $"names more than {MaxTimes} times");
            var days = CloudRoutineSchedule.Days(routine.Days) ?? throw Bad(at + "Days", "is not days such as Sun or Mon,Wed,Fri");
            routines.Add(new CloudRoutine(routine.Name!, routine.Id!, routine.Token!.Trim(), times, days, routine.At!.Trim(), (routine.Days ?? "").Trim()));
        }

        return routines;
    }

    private static InvalidOperationException Bad(string setting, string problem) =>
        new($"{Section}__{setting} {problem}.");
}

/// <summary>One routine as the configuration writes it.</summary>
public sealed class CloudRoutineOptions
{
    /// <summary>The routine's name, as the fire record and the status show it.</summary>
    public string? Name { get; init; }

    /// <summary>The routine's id, trig_....</summary>
    public string? Id { get; init; }

    /// <summary>The routine's API trigger token. It can start this routine and nothing else.</summary>
    public string? Token { get; init; }

    /// <summary>Local times, comma separated: 05:30, or a range with a step in minutes, 06:15-20:15/120.</summary>
    public string? At { get; init; }

    /// <summary>The days it runs, comma separated: Sun, or Mon,Wed,Fri. Blank is every day.</summary>
    public string? Days { get; init; }
}

/// <summary>One routine, checked. A class and not a record, so nothing prints its token.</summary>
public sealed partial class CloudRoutine(
    string name, string id, string token, IReadOnlyList<LocalTime> times, IReadOnlySet<IsoDayOfWeek> days, string at, string daysText)
{
    public string Name { get; } = name;

    public string Id { get; } = id;

    public string Token { get; } = token;

    public IReadOnlyList<LocalTime> Times { get; } = times;

    /// <summary>Empty is every day.</summary>
    public IReadOnlySet<IsoDayOfWeek> Days { get; } = days;

    public string At { get; } = at;

    public string DaysText { get; } = daysText;

    public bool RunsOn(LocalDate date) => Days.Count == 0 || Days.Contains(date.DayOfWeek);

    [GeneratedRegex("^[a-z0-9-]{1,48}$")]
    public static partial Regex NamePattern();

    [GeneratedRegex("^trig_[A-Za-z0-9]{1,64}$")]
    public static partial Regex IdPattern();
}

/// <summary>
/// Which runs are due on the owner's clock. Pure: a moment, a zone and the
/// routines in, the due runs out.
/// </summary>
public static class CloudRoutineSchedule
{
    public const int MaxKeyLength = 96;

    private static readonly LocalTimePattern Clock = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    private static readonly LocalDateTimePattern KeyTime = LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd'T'HH:mm");

    private static readonly LocalDateTimePattern Spoken = LocalDateTimePattern.CreateWithInvariantCulture("ddd d MMM uuuu 'at' HH:mm");

    /// <summary>One run: the routine and the local minute it belongs to.</summary>
    /// <param name="Key">
    /// The routine's name and the local date and time. One row per key, ever,
    /// so a local minute that comes round twice, when the phone moves west or
    /// the clocks go back, starts one run.
    /// </param>
    public sealed record Slot(CloudRoutine Routine, LocalDateTime At, string Key);

    /// <summary>The times in an At value, in order, or null when it is not one.</summary>
    public static IReadOnlyList<LocalTime>? Times(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var times = new SortedSet<LocalTime>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries))
        {
            var slash = part.Split('/');
            var ends = slash[0].Split('-');
            if (slash.Length > 2 || ends.Length > 2 || (slash.Length == 2) != (ends.Length == 2)) return null;
            var first = Clock.Parse(ends[0]);
            if (!first.Success) return null;
            if (ends.Length == 1)
            {
                times.Add(first.Value);
                continue;
            }

            var last = Clock.Parse(ends[1]);
            if (!last.Success || last.Value < first.Value) return null;
            if (!int.TryParse(slash[1], NumberStyles.None, CultureInfo.InvariantCulture, out var step) || step is < 5 or > 720) return null;
            for (var minutes = 0L; ; minutes += step)
            {
                var time = first.Value.PlusMinutes(minutes);
                if (minutes >= 24 * 60 || time < first.Value || time > last.Value) break;
                times.Add(time);
            }
        }

        return [.. times];
    }

    /// <summary>The days in a Days value, empty for every day, or null when it is not one.</summary>
    public static IReadOnlySet<IsoDayOfWeek>? Days(string? value)
    {
        var days = new HashSet<IsoDayOfWeek>();
        if (string.IsNullOrWhiteSpace(value)) return days;
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries))
        {
            var day = part.ToUpperInvariant() switch
            {
                "MON" => IsoDayOfWeek.Monday,
                "TUE" => IsoDayOfWeek.Tuesday,
                "WED" => IsoDayOfWeek.Wednesday,
                "THU" => IsoDayOfWeek.Thursday,
                "FRI" => IsoDayOfWeek.Friday,
                "SAT" => IsoDayOfWeek.Saturday,
                "SUN" => IsoDayOfWeek.Sunday,
                _ => IsoDayOfWeek.None,
            };
            if (day == IsoDayOfWeek.None) return null;
            days.Add(day);
        }

        return days;
    }

    /// <summary>
    /// The runs whose local minute has come, and came less than
    /// <paramref name="grace"/> ago, in <paramref name="zone"/>.
    /// </summary>
    public static IReadOnlyList<Slot> Due(IReadOnlyList<CloudRoutine> routines, Instant now, DateTimeZone zone, Duration grace)
    {
        var local = now.InZone(zone).LocalDateTime;
        var slots = new List<Slot>();
        foreach (var routine in routines)
        {
            // Yesterday too: 23:55 is still due at 00:05.
            foreach (var date in (LocalDate[])[local.Date.PlusDays(-1), local.Date])
            {
                if (!routine.RunsOn(date)) continue;
                foreach (var time in routine.Times)
                {
                    var at = date + time;
                    if (at > local) continue;
                    if (Period.Between(at, local, PeriodUnits.Seconds).Seconds >= (long)grace.TotalSeconds) continue;
                    slots.Add(new Slot(routine, at, Key(routine, at)));
                }
            }
        }

        return slots;
    }

    /// <summary>The routine's next local minute after <paramref name="now"/>, in <paramref name="zone"/>. Null when it has none in 8 days.</summary>
    public static LocalDateTime? Next(CloudRoutine routine, Instant now, DateTimeZone zone)
    {
        var local = now.InZone(zone).LocalDateTime;
        for (var day = 0; day <= 8; day++)
        {
            var date = local.Date.PlusDays(day);
            if (!routine.RunsOn(date)) continue;
            foreach (var time in routine.Times)
            {
                if (date + time > local) return date + time;
            }
        }

        return null;
    }

    public static string Key(CloudRoutine routine, LocalDateTime at) => $"cloud:{routine.Name}:{KeyTime.Format(at)}";

    /// <summary>What the run is told: the zone the phone is in, and the time there.</summary>
    public static string Text(Slot slot, DateTimeZone zone, Instant now) =>
        $"abera.tech started this run for its {Clock.Format(slot.At.TimeOfDay)} slot. "
        + $"Neb's iPhone reports the time zone {zone.Id}. It is {Spoken.Format(now.InZone(zone).LocalDateTime)} there. "
        + $"Write every time and date in {zone.Id}.";
}

/// <summary>What the API trigger answered. 0 is no answer at all.</summary>
public sealed record CloudRoutineResult(int Status, string? Reason)
{
    public bool Fired => Status == 200;

    /// <summary>
    /// Worth one more try: the service was busy or never reached. A timeout
    /// is not, because the run may have started.
    /// </summary>
    public bool Retryable => Status is 429 or >= 500 || (Status == 0 && Reason != "Timeout");

    public string Outcome => Fired ? "fired" : $"failed: {Status} {Reason}".TrimEnd();
}

/// <summary>Starts a routine through its API trigger.</summary>
public sealed partial class CloudRoutineClient(HttpClient http, CloudRoutinesOptions options)
{
    public async Task<CloudRoutineResult> FireAsync(CloudRoutine routine, string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, string.Format(CultureInfo.InvariantCulture, options.FireUrl, routine.Id));
        request.Headers.TryAddWithoutValidation("authorization", $"Bearer {routine.Token}");
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        request.Headers.TryAddWithoutValidation("anthropic-beta", "experimental-cc-routine-2026-04-01");
        request.Content = new StringContent(JsonSerializer.Serialize(new { text }), Encoding.UTF8, "application/json");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            return status == 200 ? new CloudRoutineResult(200, null) : new CloudRoutineResult(status, await ReasonAsync(response, cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return new CloudRoutineResult(0, exception.HttpRequestError == HttpRequestError.Unknown
                ? nameof(HttpRequestException)
                : exception.HttpRequestError.ToString());
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CloudRoutineResult(0, "Timeout");
        }
    }

    /// <summary>The error's type word, such as rate_limit_error. Never its message.</summary>
    private static async Task<string?> ReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length > 4096) return null;
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.Object
                   && error.TryGetProperty("type", out var type)
                   && type.GetString() is { } word
                   && ReasonPattern().IsMatch(word)
                ? word
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex("^[a-z_]{1,40}$")]
    private static partial Regex ReasonPattern();
}

/// <summary>One run the clock started, or tried to.</summary>
public sealed record CloudRoutineFire(string Key, string Zone, Instant ClaimedAt, string Outcome, Instant? CompletedAt);

/// <summary>The runs the clock has claimed. The key's row is the claim.</summary>
public interface ICloudRoutineFireStore
{
    /// <summary>True for the one caller that may start this run. False for every other, ever.</summary>
    Task<bool> TryClaimAsync(string key, string zone, Instant now, CancellationToken cancellationToken);

    Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken);

    /// <summary>The newest claims first.</summary>
    Task<IReadOnlyList<CloudRoutineFire>> RecentAsync(int limit, CancellationToken cancellationToken);

    Task PruneAsync(Instant before, CancellationToken cancellationToken);
}

public sealed class DatabaseCloudRoutineFireStore(SchedulingDbContext database) : ICloudRoutineFireStore
{
    public async Task<bool> TryClaimAsync(string key, string zone, Instant now, CancellationToken cancellationToken)
    {
        // The primary key does the deciding, as it does for an alert: a
        // read-then-insert would let two replicas both start the run.
        var inserted = await database.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "CloudRoutineFires" ("Key", "Zone", "ClaimedAt", "Outcome")
             VALUES ({key}, {zone}, {now}, 'claimed')
             ON CONFLICT ("Key") DO NOTHING
             """,
            cancellationToken);

        return inserted == 1;
    }

    public async Task RecordOutcomeAsync(string key, string outcome, Instant now, CancellationToken cancellationToken)
    {
        var text = outcome.Length <= 64 ? outcome : outcome[..64];
        await database.CloudRoutineFires
            .Where(fire => fire.Key == key)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(fire => fire.Outcome, text)
                    .SetProperty(fire => fire.CompletedAt, (Instant?)now),
                cancellationToken);
    }

    public async Task<IReadOnlyList<CloudRoutineFire>> RecentAsync(int limit, CancellationToken cancellationToken) =>
        await database.CloudRoutineFires.AsNoTracking()
            .OrderByDescending(fire => fire.ClaimedAt)
            .ThenBy(fire => fire.Key)
            .Take(limit)
            .Select(fire => new CloudRoutineFire(fire.Key, fire.Zone, fire.ClaimedAt, fire.Outcome, fire.CompletedAt))
            .ToListAsync(cancellationToken);

    public async Task PruneAsync(Instant before, CancellationToken cancellationToken) =>
        await database.CloudRoutineFires.Where(fire => fire.ClaimedAt < before).ExecuteDeleteAsync(cancellationToken);
}

/// <summary>The wait before the one retry. Replaced in tests, which read the time asked for.</summary>
public class CloudRoutineDelay
{
    public virtual Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}

/// <summary>
/// Starts each cloud routine at its local time, in the zone of the paired
/// phone seen most recently, else the alerts' fallback zone. Every replica
/// runs this, and the claim row lets one of them start a run.
/// </summary>
public sealed class CloudRoutineClock(
    IServiceScopeFactory scopes,
    CloudRoutinesOptions options,
    AlertsOptions alerts,
    IClock clock,
    CloudRoutineDelay delay,
    ILogger<CloudRoutineClock> logger) : BackgroundService
{
    public static readonly Duration Tick = Duration.FromSeconds(30);

    public static readonly Duration RetryAfter = Duration.FromSeconds(20);

    public static readonly Duration Kept = Duration.FromDays(60);

    private readonly IReadOnlyList<CloudRoutine> _routines = options.Parsed();

    /// <summary>Off, and silent, until a routine is configured.</summary>
    public bool On => _routines.Count > 0;

    public IReadOnlyList<CloudRoutine> Routines => _routines;

    /// <summary>One look at the clock. Returns the runs this pass claimed.</summary>
    public async Task<int> PassAsync(CancellationToken cancellationToken)
    {
        if (!On) return 0;

        var now = clock.GetCurrentInstant();
        await using var scope = scopes.CreateAsyncScope();
        var fires = scope.ServiceProvider.GetRequiredService<ICloudRoutineFireStore>();
        var settings = await AlertSettings.CurrentAsync(
            scope.ServiceProvider.GetRequiredService<IAlertStore>(), alerts, cancellationToken);
        var zone = await RoutineRings.ZoneAsync(
            scope.ServiceProvider.GetRequiredService<IAlertDeviceStore>(), settings, cancellationToken);

        var claimed = 0;
        foreach (var slot in CloudRoutineSchedule.Due(_routines, now, zone, Duration.FromMinutes(options.GraceMinutes)))
        {
            if (!await fires.TryClaimAsync(slot.Key, zone.Id, now, cancellationToken)) continue;
            claimed++;

            var client = scope.ServiceProvider.GetRequiredService<CloudRoutineClient>();
            var text = CloudRoutineSchedule.Text(slot, zone, now);
            var result = await client.FireAsync(slot.Routine, text, cancellationToken);
            if (result.Retryable)
            {
                CloudRoutineLog.Retrying(logger, slot.Routine.Name, result.Status, result.Reason ?? "");
                await delay.WaitAsync(RetryAfter.ToTimeSpan(), cancellationToken);
                result = await client.FireAsync(slot.Routine, text, cancellationToken);
            }

            await fires.RecordOutcomeAsync(slot.Key, result.Outcome, clock.GetCurrentInstant(), cancellationToken);
            if (result.Fired) CloudRoutineLog.Fired(logger, slot.Routine.Name, slot.Key, zone.Id);
            else CloudRoutineLog.Failed(logger, slot.Routine.Name, slot.Key, result.Status, result.Reason ?? "");
        }

        await fires.PruneAsync(now - Kept, cancellationToken);
        return claimed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!On) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PassAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // The type alone: a message can carry an address.
                logger.LogWarning("The cloud routine clock pass failed ({Failure}).", exception.GetType().Name);
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(Tick.ToTimeSpan(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

/// <summary>
/// The clock's outcomes, under fixed ids beside the push events. A line
/// carries the routine's name, the run's key, the zone and the status.
/// Never the token.
/// </summary>
public static partial class CloudRoutineLog
{
    public const int FiredId = 4021;

    public const int RetryingId = 4022;

    public const int FailedId = 4023;

    [LoggerMessage(EventId = FiredId, EventName = "CloudRoutineFired", Level = LogLevel.Information,
        Message = "Cloud routine {Routine} started for {Key} in {Zone}.")]
    public static partial void Fired(ILogger logger, string routine, string key, string zone);

    [LoggerMessage(EventId = RetryingId, EventName = "CloudRoutineRetrying", Level = LogLevel.Warning,
        Message = "Cloud routine {Routine} answered {Status} {Reason}. Trying once more in 20 s.")]
    public static partial void Retrying(ILogger logger, string routine, int status, string reason);

    [LoggerMessage(EventId = FailedId, EventName = "CloudRoutineFailed", Level = LogLevel.Warning,
        Message = "Cloud routine {Routine} did not start for {Key} ({Status} {Reason}).")]
    public static partial void Failed(ILogger logger, string routine, string key, int status, string reason);
}

/// <summary>What the owner reads at /api/alerts/cloud-routines. No id and no token.</summary>
public sealed record CloudRoutineStatus(
    bool On, string Zone, string ZoneFrom, IReadOnlyList<CloudRoutineStatus.Line> Routines, IReadOnlyList<CloudRoutineFire> Recent)
{
    /// <param name="Next">The next run's local date and time in <see cref="CloudRoutineStatus.Zone"/>.</param>
    public sealed record Line(string Name, string At, string Days, string? Next, Instant? NextAt);

    private static readonly LocalDateTimePattern Shown = LocalDateTimePattern.CreateWithInvariantCulture("ddd d MMM uuuu HH:mm");

    public static async Task<CloudRoutineStatus> ReadAsync(
        CloudRoutineClock clock,
        IAlertStore store,
        IAlertDeviceStore devices,
        ICloudRoutineFireStore fires,
        AlertsOptions alerts,
        Instant now,
        CancellationToken cancellationToken)
    {
        var settings = await AlertSettings.CurrentAsync(store, alerts, cancellationToken);
        var zone = await RoutineRings.ZoneAsync(devices, settings, cancellationToken);
        var phone = await devices.LatestTimeZoneAsync(cancellationToken);
        var lines = clock.Routines
            .Select(routine =>
            {
                var next = CloudRoutineSchedule.Next(routine, now, zone);
                return new Line(
                    routine.Name, routine.At, routine.DaysText,
                    next is { } at ? Shown.Format(at) : null,
                    next is { } local ? zone.AtLeniently(local).ToInstant() : null);
            })
            .ToList();

        return new CloudRoutineStatus(
            clock.On, zone.Id, phone == zone.Id ? "phone" : "fallback", lines, await fires.RecentAsync(40, cancellationToken));
    }
}

public static class CloudRoutineClockRegistration
{
    public static IServiceCollection AddCloudRoutineClock(this IServiceCollection services)
    {
        services.AddSingleton<CloudRoutineDelay>();
        services.AddSingleton<CloudRoutineClock>();
        services.AddHostedService(provider => provider.GetRequiredService<CloudRoutineClock>());
        // No request logging: the default logger writes headers at Trace, and one is the token.
        services.AddHttpClient<CloudRoutineClient>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllLoggers();
        return services;
    }
}
