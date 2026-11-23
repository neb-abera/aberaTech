using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One ring of a routine alarm or of one of its steps. AlertAt is the ring.
/// StartsAt is when it stops: the ring and the saved StopAfterMinutes.
/// </summary>
/// <param name="Key">
/// routine:{id}:{scheduled local date and time}, and for a step
/// :step:{step id} after it, with the routine's scheduled time. The phone
/// makes the same key.
/// </param>
/// <param name="StepId">The step's id. Null for the routine's own ring.</param>
public sealed record RoutineRing(string Key, Guid RoutineId, string Label, Instant AlertAt, Instant StartsAt, Guid? StepId = null)
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
/// acknowledgment stops all of them.
/// </summary>
/// <remarks>
/// The phone's rules. A routine with days rings on those ISO weekdays. One
/// with none rings once, at the next hour:minute after it was saved. A
/// wall-clock time a clock change skips rings at the first time that
/// exists after it. A time that happens twice rings the first time. An
/// edit changes rings after the edit only: a ring of the new time that
/// fell before the save never rings, and a ring of the old time that was
/// ringing when the save came keeps ringing (<see cref="IAlertStore.HoldRingsAsync"/>).
/// A step rings its total offset after its routine's ring, in elapsed
/// time, under a key with the routine's scheduled time. A step checked off
/// for a cycle does not ring for it (<see cref="RoutineCompletions"/>). A
/// delayed cycle rings later, its steps with it, under the keys of its
/// planned time (<see cref="RoutineDelays"/>).
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

    /// <summary>The text between a step ring's cycle and its step id.</summary>
    public const string StepInfix = ":step:";

    /// <summary>"routine:0d8c…:2026-10-28T06:30": the routine's id in lowercase and the scheduled local time, never a snoozed one.</summary>
    public static string Key(Guid routineId, LocalDateTime scheduled) =>
        $"{KeyPrefix}{routineId:D}:{Stamp(scheduled)}";

    /// <summary>"routine:0d8c…:2026-10-28T06:30:step:5a1e…": a step's ring in the cycle of the routine's ring at that time.</summary>
    public static string StepKey(Guid routineId, string cycle, Guid stepId) =>
        $"{KeyPrefix}{routineId:D}:{cycle}{StepInfix}{stepId:D}";

    /// <summary>"2026-10-28T06:30": a cycle, the scheduled local time of the routine's own ring.</summary>
    public static string Stamp(LocalDateTime scheduled) => KeyTime.Format(scheduled);

    /// <summary>The cycle a stamp names, or null when it is not one.</summary>
    public static LocalDateTime? ParseStamp(string? stamp) =>
        stamp is not null && KeyTime.Parse(stamp) is { Success: true } parsed ? parsed.Value : null;

    /// <summary>The routine, the cycle and the step, if any, a ring key names. Null for anything else.</summary>
    public static (Guid RoutineId, string Cycle, Guid? StepId)? ParseKey(string? key)
    {
        if (!IsKey(key)) return null;
        var rest = key![KeyPrefix.Length..];
        if (rest.Length < 37 || rest[36] != ':' || !Guid.TryParseExact(rest[..36], "D", out var routineId)) return null;
        rest = rest[37..];
        Guid? stepId = null;
        if (rest.IndexOf(StepInfix, StringComparison.Ordinal) is var at and >= 0)
        {
            if (!Guid.TryParseExact(rest[(at + StepInfix.Length)..], "D", out var step)) return null;
            stepId = step;
            rest = rest[..at];
        }

        return ParseStamp(rest) is null ? null : (routineId, rest, stepId);
    }

    public static bool IsKey(string? key) => key?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true;

    /// <summary>The event id a ring is planned under. No type can be chosen for it, so it is always an alarm.</summary>
    public static string EventId(Guid routineId) => $"{KeyPrefix}{routineId:D}";

    /// <summary>
    /// Every ring of the enabled routines and of their steps that falls from
    /// <paramref name="from"/> to <paramref name="until"/>, soonest first. A
    /// step rings its cumulative offset after the routine's ring, so a step of
    /// a ring before <paramref name="from"/> can fall inside. A step ring
    /// whose key is in <paramref name="completed"/> was checked off for that
    /// cycle and is left out. A cycle in <paramref name="outcomes"/> leaves
    /// out the routine's ring, and when Skipped its step rings too. A cycle in
    /// <paramref name="delays"/> rings that much later, its steps with it,
    /// under the same keys.
    /// </summary>
    public static IReadOnlyList<RoutineRing> Plan(
        IEnumerable<AlertRoutine> routines,
        DateTimeZone zone,
        Instant from,
        Instant until,
        Duration stopAfter,
        IReadOnlySet<string>? completed = null,
        IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineOutcome>? outcomes = null,
        IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineDelay>? delays = null)
    {
        var rings = new List<RoutineRing>();
        foreach (var routine in routines)
        {
            if (!routine.Enabled) continue;
            var offsets = AlertRoutines.Offsets(routine);
            // A delayed cycle from before the window can ring inside it.
            var reach = Duration.FromMinutes(offsets.Count == 0 ? 0 : offsets[^1].Minutes)
                        + (delays is { Count: > 0 } ? RoutineDelays.Longest : Duration.Zero);

            void Add(Instant at, string key, string label, Guid? stepId)
            {
                if (at <= routine.UpdatedAt || at < from || at > until) return;
                rings.Add(new RoutineRing(key, routine.Id, label, at, at + stopAfter, stepId));
            }

            foreach (var (scheduled, planned) in Cycles(routine, zone, from - reach, until))
            {
                var cycle = Stamp(scheduled);
                var at = planned + RoutineDelays.Shift(delays, routine.Id, cycle);
                if (!RoutineOutcomes.Silences(outcomes, routine.Id, cycle, null)) Add(at, Key(routine.Id, scheduled), routine.Label, null);
                foreach (var (step, minutes) in offsets)
                {
                    var key = StepKey(routine.Id, cycle, step.Id);
                    if (completed?.Contains(key) == true || RoutineOutcomes.Silences(outcomes, routine.Id, cycle, step.Id)) continue;
                    Add(at + Duration.FromMinutes(minutes), key, step.Label, step.Id);
                }
            }
        }

        return [.. rings.OrderBy(ring => ring.AlertAt).ThenBy(ring => ring.Key, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The routine's own rings by its current schedule, whether or not it is
    /// switched on, with a day either side of the window so a zone far from
    /// UTC never cuts it: every one of its weekdays, or for one with no days
    /// its single ring, the next hour:minute after it was saved. The caller
    /// keeps the ones it wants by instant.
    /// </summary>
    public static IEnumerable<(LocalDateTime Scheduled, Instant At)> Cycles(
        AlertRoutine routine, DateTimeZone zone, Instant from, Instant until)
    {
        var time = new LocalTime(routine.Hour, routine.Minute);
        if (routine.Days.Count == 0)
        {
            if (Once(routine, zone) is { } once) yield return once;
            yield break;
        }

        var last = until.InZone(zone).Date.PlusDays(1);
        for (var date = from.InZone(zone).Date.PlusDays(-1); date <= last; date = date.PlusDays(1))
        {
            if (!routine.Days.Contains((int)date.DayOfWeek)) continue;
            var scheduled = date.At(time);
            yield return (scheduled, zone.ResolveLocal(scheduled, PhoneRules).ToInstant());
        }
    }

    /// <summary>The single ring of a routine with no days: the next hour:minute after it was saved.</summary>
    public static (LocalDateTime Scheduled, Instant At)? Once(AlertRoutine routine, DateTimeZone zone)
    {
        var time = new LocalTime(routine.Hour, routine.Minute);
        var date = routine.UpdatedAt.InZone(zone).Date;
        for (var day = 0; day < 3; day++, date = date.PlusDays(1))
        {
            var at = zone.ResolveLocal(date.At(time), PhoneRules).ToInstant();
            if (at > routine.UpdatedAt) return (date.At(time), at);
        }

        return null;
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
        // A step rings at most a day after its cycle's ring.
        var completed = (await store.CompletionsAsync(
                from - Duration.FromMinutes(AlertRoutines.MaxStepOffsetMinutes) - Duration.FromDays(1), cancellationToken))
            .Select(completion => completion.StepKey)
            .ToHashSet(StringComparer.Ordinal);
        var outcomes = RoutineOutcomes.ByCycle(await store.OutcomesAsync(
            from - Duration.FromMinutes(AlertRoutines.MaxStepOffsetMinutes) - Duration.FromDays(1), cancellationToken));
        var delays = RoutineDelays.ByCycle(await store.DelaysAsync(
            from - Duration.FromMinutes(AlertRoutines.MaxStepOffsetMinutes) - RoutineDelays.Longest - Duration.FromDays(1), cancellationToken));
        var planned = Plan(routines, zone, from, until, Duration.FromMinutes(settings.StopAfterMinutes), completed, outcomes, delays);

        var enabled = routines.Where(routine => routine.Enabled).Select(routine => routine.Id).ToHashSet();
        var held = (await store.HeldRingsAsync(cancellationToken))
            .Where(ring => enabled.Contains(ring.RoutineId) && ring.AlertAt >= from && ring.AlertAt <= until)
            .Where(ring => !completed.Contains(ring.Key) && !RoutineOutcomes.Silences(outcomes, ring.Key))
            .Select(ring => ring with { StepId = ParseKey(ring.Key)?.StepId })
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
