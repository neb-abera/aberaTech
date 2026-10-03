using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One ring of a routine alarm. AlertAt is the ring. StartsAt is when it
/// stops: the ring and the saved StopAfterMinutes.
/// </summary>
/// <param name="Key">routine:{id}:{scheduled local date and time}. The phone makes the same key.</param>
public sealed record RoutineRing(string Key, Guid RoutineId, string Label, Instant AlertAt, Instant StartsAt)
{
    /// <summary>The ring as the send path takes it: an alarm titled with the label.</summary>
    public PlannedAlert ToPlanned() => new(
        Key,
        Label,
        null,
        StartsAt,
        AlertAt,
        AlertSource.Reminder,
        RoutineRings.EventId(RoutineId),
        Critical: true,
        Routine: true);
}

/// <summary>
/// The rings of the routine alarms, worked out on the server as the phone
/// works them out, so the browser and Pushover ring them too and one
/// acknowledgement stops all of them.
/// </summary>
/// <remarks>
/// The phone's rules. A routine with days rings on those ISO weekdays. One
/// with none rings once, at the next hour:minute after it was saved. A
/// wall-clock time a clock change skips rings at the first time that
/// exists after it. A time that happens twice rings the first time. An
/// edit changes rings after the edit only: a ring of the new time that
/// fell before the save never rings, and a ring of the old time that was
/// ringing when the save came keeps ringing (<see cref="IAlertStore.HoldRingsAsync"/>).
/// </remarks>
public static class RoutineRings
{
    public const string KeyPrefix = "routine:";

    /// <summary>How far ahead the status lists rings.</summary>
    public static readonly Duration Listed = Duration.FromHours(24);

    private static readonly LocalDateTimePattern KeyTime =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm");

    /// <summary>A repeated time resolves to its first instant. A skipped one to the first instant after the gap.</summary>
    private static readonly ZoneLocalMappingResolver PhoneRules =
        Resolvers.CreateMappingResolver(Resolvers.ReturnEarlier, Resolvers.ReturnStartOfIntervalAfter);

    /// <summary>"routine:0d8c…:2026-10-28T06:30": the routine's id in lowercase and the scheduled local time, never a snoozed one.</summary>
    public static string Key(Guid routineId, LocalDateTime scheduled) =>
        $"{KeyPrefix}{routineId:D}:{KeyTime.Format(scheduled)}";

    public static bool IsKey(string? key) => key?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true;

    /// <summary>The event id a ring is planned under. No type can be chosen for it, so it is always an alarm.</summary>
    public static string EventId(Guid routineId) => $"{KeyPrefix}{routineId:D}";

    /// <summary>
    /// Every ring of the enabled routines whose ring falls from
    /// <paramref name="from"/> to <paramref name="until"/>, soonest first.
    /// </summary>
    public static IReadOnlyList<RoutineRing> Plan(
        IEnumerable<AlertRoutine> routines, DateTimeZone zone, Instant from, Instant until, Duration stopAfter)
    {
        var rings = new List<RoutineRing>();
        foreach (var routine in routines)
        {
            if (!routine.Enabled) continue;
            var time = new LocalTime(routine.Hour, routine.Minute);

            void Add(LocalDate date)
            {
                var scheduled = date.At(time);
                var at = zone.ResolveLocal(scheduled, PhoneRules).ToInstant();
                if (at <= routine.UpdatedAt || at < from || at > until) return;
                rings.Add(new RoutineRing(Key(routine.Id, scheduled), routine.Id, routine.Label, at, at + stopAfter));
            }

            if (routine.Days.Count == 0)
            {
                // Once, at the next hour:minute after the save.
                var date = routine.UpdatedAt.InZone(zone).Date;
                for (var day = 0; day < 3; day++, date = date.PlusDays(1))
                {
                    if (zone.ResolveLocal(date.At(time), PhoneRules).ToInstant() <= routine.UpdatedAt) continue;
                    Add(date);
                    break;
                }

                continue;
            }

            // A day either side, so a zone far from UTC never cuts the window.
            var last = until.InZone(zone).Date.PlusDays(1);
            for (var date = from.InZone(zone).Date.PlusDays(-1); date <= last; date = date.PlusDays(1))
            {
                if (routine.Days.Contains((int)date.DayOfWeek)) Add(date);
            }
        }

        return [.. rings.OrderBy(ring => ring.AlertAt).ThenBy(ring => ring.Key, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The zone the rings are planned in: the zone of the paired phone seen
    /// most recently that reported one, else the settings' fallback zone.
    /// </summary>
    public static async Task<DateTimeZone> ZoneAsync(
        IAlertDeviceStore? devices, AlertSettings settings, CancellationToken cancellationToken) =>
        devices is not null
        && await devices.LatestTimeZoneAsync(cancellationToken) is { } name
        && DateTimeZoneProviders.Tzdb.GetZoneOrNull(name) is { } zone
            ? zone
            : settings.FallbackZone();

    /// <summary>
    /// The rings from the stored routines and the rings an edit held, from
    /// <paramref name="from"/> to <paramref name="until"/>. A held ring wins
    /// over a planned one with the same key: the edit came after it rang.
    /// </summary>
    public static async Task<IReadOnlyList<RoutineRing>> CurrentAsync(
        IAlertStore store,
        IAlertDeviceStore? devices,
        AlertSettings settings,
        Instant from,
        Instant until,
        CancellationToken cancellationToken)
    {
        var zone = await ZoneAsync(devices, settings, cancellationToken);
        var routines = await store.RoutinesAsync(cancellationToken);
        var planned = Plan(routines, zone, from, until, Duration.FromMinutes(settings.StopAfterMinutes));

        var enabled = routines.Where(routine => routine.Enabled).Select(routine => routine.Id).ToHashSet();
        var held = (await store.HeldRingsAsync(cancellationToken))
            .Where(ring => enabled.Contains(ring.RoutineId) && ring.AlertAt >= from && ring.AlertAt <= until)
            .ToList();
        var keys = held.Select(ring => ring.Key).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. held.Concat(planned.Where(ring => !keys.Contains(ring.Key)))
                .OrderBy(ring => ring.AlertAt)
                .ThenBy(ring => ring.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>The rings ringing now or still to ring within the next 24 hours, as the status lists them.</summary>
    public static Task<IReadOnlyList<RoutineRing>> ListedAsync(
        IAlertStore store, IAlertDeviceStore? devices, AlertSettings settings, Instant now, CancellationToken cancellationToken) =>
        CurrentAsync(store, devices, settings, now - Duration.FromMinutes(settings.StopAfterMinutes), now + Listed, cancellationToken);

    /// <summary>The rings ringing at <paramref name="now"/>: rung and not yet stopped.</summary>
    public static async Task<IReadOnlyList<RoutineRing>> RingingAsync(
        IAlertStore store, IAlertDeviceStore? devices, AlertSettings settings, Instant now, CancellationToken cancellationToken) =>
        [.. (await ListedAsync(store, devices, settings, now, cancellationToken)).Where(ring => ring.AlertAt <= now && ring.StartsAt > now)];
}
