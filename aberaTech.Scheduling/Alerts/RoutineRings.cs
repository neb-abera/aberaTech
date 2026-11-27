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

    /// <summary>
    /// "2026-10-28T06:30": a cycle, the scheduled local date and time of the
    /// routine's own ring. For an interval of minutes or hours that is the
    /// time of the ring, which is not the routine's hour:minute.
    /// </summary>
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
            // A delayed cycle from before the window can ring inside it.
            var reach = Duration.FromMinutes(routine.Steps.Sum(step => step.OffsetMinutes))
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
                foreach (var (step, minutes) in AlertRoutines.Offsets(routine, scheduled.Date))
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
    /// switched on. A wall-clock schedule (weekdays, or an interval of days,
    /// weeks, months or years) gets a day either side of the window, so a
    /// zone far from UTC never cuts it. An interval of minutes or hours is
    /// elapsed time and gets the window itself. One that rings once gets its
    /// single ring, the next hour:minute after it was saved. The caller
    /// keeps the ones it wants by instant.
    /// </summary>
    /// <remarks>
    /// The work is in proportion to the window. A series from years ago
    /// starts at the first ring near <paramref name="from"/>, found by
    /// arithmetic.
    /// </remarks>
    public static IEnumerable<(LocalDateTime Scheduled, Instant At)> Cycles(
        AlertRoutine routine, DateTimeZone zone, Instant from, Instant until)
    {
        if (routine.RingsOnce)
        {
            if (Once(routine, zone) is { } once) yield return once;
            yield break;
        }

        if (until < from) yield break;
        var time = new LocalTime(routine.Hour, routine.Minute);
        var first = from.InZone(zone).Date.PlusDays(-1);
        var last = until.InZone(zone).Date.PlusDays(1);
        var every = Math.Clamp(routine.Every, RoutineRepeats.MinEvery, RoutineRepeats.MaxEvery);

        if (routine.Repeat != RoutineRepeats.Interval)
        {
            // Weekdays. Every week needs no start. Every N weeks counts whole
            // weeks, Monday first, from the week of the start date.
            LocalDate? start = every > 1 ? routine.StartsOn : null;
            if (every > 1 && start is null) yield break;
            if (start is { } begins && begins > first) first = begins;
            for (var date = first; date <= last; date = date.PlusDays(1))
            {
                if (!routine.Days.Contains((int)date.DayOfWeek)) continue;
                if (start is { } counted && WeeksBetween(counted, date) % every != 0) continue;
                var scheduled = date.At(time);
                yield return (scheduled, zone.ResolveLocal(scheduled, PhoneRules).ToInstant());
            }

            yield break;
        }

        // A series that starts after the window has no ring in it. Checked
        // by date, before any instant is made from a date at the calendar's end.
        if (routine.StartsOn is not { } anchor || anchor > last) yield break;

        switch (routine.Unit)
        {
            case RoutineRepeats.Minutes or RoutineRepeats.Hours:
                foreach (var ring in Elapsed(zone, anchor.At(time), routine.Unit == RoutineRepeats.Minutes
                             ? Duration.FromMinutes(every)
                             : Duration.FromHours(every), from, until))
                {
                    yield return ring;
                }

                break;

            case RoutineRepeats.Days or RoutineRepeats.Weeks:
                var step = routine.Unit == RoutineRepeats.Days ? every : every * 7;
                var behind = Period.DaysBetween(anchor, first);
                for (var date = behind <= 0 ? anchor : anchor.PlusDays((behind + step - 1) / step * step); date <= last; date = date.PlusDays(step))
                {
                    var scheduled = date.At(time);
                    yield return (scheduled, zone.ResolveLocal(scheduled, PhoneRules).ToInstant());
                }

                break;

            case RoutineRepeats.Months or RoutineRepeats.Years:
                var months = routine.Unit == RoutineRepeats.Months ? every : every * 12;
                var elapsed = (first.Year - anchor.Year) * 12 + first.Month - anchor.Month;
                // Each ring is counted from the start date, never from the
                // ring before it, so the 31st stays the 31st.
                for (var count = Math.Max(0, elapsed / months - 1); ; count++)
                {
                    var date = anchor.PlusMonths(count * months);
                    if (date > last) break;
                    // A month or year without the day is skipped, as a countdown skips it.
                    if (date.Day != anchor.Day || date < first) continue;
                    var scheduled = date.At(time);
                    yield return (scheduled, zone.ResolveLocal(scheduled, PhoneRules).ToInstant());
                }

                break;
        }
    }

    /// <summary>Whole weeks from the Monday of <paramref name="start"/>'s week to the Monday of <paramref name="date"/>'s.</summary>
    private static int WeeksBetween(LocalDate start, LocalDate date) =>
        Period.DaysBetween(Monday(start), Monday(date)) / 7;

    private static LocalDate Monday(LocalDate date) => date.PlusDays(1 - (int)date.DayOfWeek);

    /// <summary>
    /// The rings of an interval of minutes or hours from
    /// <paramref name="from"/> to <paramref name="until"/>: the start, then
    /// every <paramref name="step"/> of elapsed time after it. Each ring's
    /// stamp is its own local date and time. When the clocks go back, a ring
    /// whose stamp an earlier ring of the series already has is dropped, so
    /// no two rings share a key.
    /// </summary>
    private static IEnumerable<(LocalDateTime Scheduled, Instant At)> Elapsed(
        DateTimeZone zone, LocalDateTime start, Duration step, Instant from, Instant until)
    {
        var anchor = zone.ResolveLocal(start, PhoneRules).ToInstant();
        var minute = Duration.FromMinutes(1);
        for (var at = Aligned(anchor, step, from); at <= until; at += step)
        {
            var local = at.InZone(zone).LocalDateTime;
            var scheduled = local.Date.At(new LocalTime(local.Hour, local.Minute));
            var passes = zone.MapLocal(scheduled);
            if (passes.Count == 2
                && passes.First().ToInstant() is var earlier
                && earlier + minute <= at
                && Aligned(anchor, step, earlier) < earlier + minute)
            {
                continue;
            }

            yield return (scheduled, at);
        }
    }

    /// <summary>The first instant of the series from <paramref name="anchor"/> every <paramref name="step"/> that is at or after <paramref name="from"/>.</summary>
    private static Instant Aligned(Instant anchor, Duration step, Instant from)
    {
        if (from <= anchor) return anchor;
        // Whole numbers: a double loses the last ring of a series decades long.
        var behind = (from - anchor).ToBigIntegerNanoseconds();
        var length = step.ToBigIntegerNanoseconds();
        return anchor + step * (long)((behind + length - 1) / length);
    }

    /// <summary>The single ring of a routine that rings once: the next hour:minute after it was saved.</summary>
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
        // A step rings at most a day after its cycle's ring, and a delay
        // moves a cycle up to RoutineDelays.Longest. A cycle moved into the
        // window is read with its check-offs and its outcome, or a step
        // checked off, or a ring marked Done, would ring at the moved time.
        var cyclesFrom = from - Duration.FromMinutes(AlertRoutines.MaxStepOffsetMinutes) - RoutineDelays.Longest - Duration.FromDays(1);
        var completed = (await store.CompletionsAsync(cyclesFrom, cancellationToken))
            .Select(completion => completion.StepKey)
            .ToHashSet(StringComparer.Ordinal);
        var outcomes = RoutineOutcomes.ByCycle(await store.OutcomesAsync(cyclesFrom, cancellationToken));
        var delays = RoutineDelays.ByCycle(await store.DelaysAsync(cyclesFrom, cancellationToken));
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
