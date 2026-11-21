using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A one-time alarm deletes itself once its ring is acknowledged, on the
/// phone, in Pushover or in a browser. That is a routine alarm with no
/// repeat days, and a countdown with its alarm on that does not repeat. A
/// countdown's calendar event stays, as the record of the day. Neb asked
/// for this on 2026-11-03.
///
/// A routine with no repeat days that has steps is not deleted on the Stop
/// of its own ring, or its steps would never ring. It goes once its ring has
/// rung and every step ring of that cycle has stopped, been acknowledged, or
/// been checked off, whichever comes first. The phone follows the same rule.
/// Set on 2026-11-19.
///
/// Done or Skipped on its single ring answers that ring, even before it
/// rings. Skipped settles every step too. After Done each step still has to
/// stop, be acknowledged, or be checked off. Set on 2026-11-21.
/// </summary>
public static class OneTimeAlarms
{
    /// <summary>Deletes every one-time alarm that is done. True when any went.</summary>
    /// <param name="devices">The phones, for the zone a routine's steps ring in. Null plans in the settings' zone.</param>
    public static async Task<bool> ForgetAcknowledgedAsync(
        IAlertStore store,
        IAlertDeviceStore? devices,
        AlertSettings settings,
        Instant now,
        CancellationToken cancellationToken)
    {
        var acknowledged = await store.AcknowledgementsAsync(cancellationToken);
        var gone = false;
        DateTimeZone? zone = null;
        IReadOnlySet<string>? completed = null;
        IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineOutcome>? outcomes = null;

        foreach (var routine in await store.RoutinesAsync(cancellationToken))
        {
            if (routine.Days.Count > 0) continue;
            zone ??= await RoutineRings.ZoneAsync(devices, settings, cancellationToken);
            outcomes ??= RoutineOutcomes.ByCycle(await store.OutcomesAsync(now - RoutineCompletions.KeepFor, cancellationToken));
            // An outcome counts on the cycle the routine rings now: one on a
            // cycle an edit moved away from was for the routine as it was then.
            var outcome = RoutineRings.Once(routine, zone) is { } single
                ? outcomes.GetValueOrDefault((routine.Id, RoutineRings.Stamp(single.Scheduled)))
                : null;
            bool done;
            if (routine.Steps.Count == 0)
            {
                var prefix = $"{RoutineRings.KeyPrefix}{routine.Id:D}:";
                // A ring acknowledged before the last save was a ring of the routine as it was then.
                done = outcome is not null || acknowledged.Any(pair =>
                    pair.Key.StartsWith(prefix, StringComparison.Ordinal) && pair.Value.At >= routine.UpdatedAt);
            }
            else
            {
                completed ??= (await store.CompletionsAsync(now - RoutineCompletions.KeepFor, cancellationToken))
                    .Select(completion => completion.StepKey)
                    .ToHashSet(StringComparer.Ordinal);
                done = StepsDone(routine, zone, Duration.FromMinutes(settings.StopAfterMinutes), acknowledged, completed, outcome, now);
            }

            if (!done) continue;
            if (await store.DeleteRoutineAsync(routine.Id, cancellationToken))
            {
                await store.ForgetHeldRingsAsync(routine.Id, cancellationToken);
                gone = true;
            }
        }

        foreach (var countdown in await store.CountdownsAsync(cancellationToken))
        {
            if (!countdown.Alarm || countdown.Repeat != CountdownRepeats.None || countdown.CalendarEventId is not { } uid) continue;
            if (countdown.TargetAt > now) continue;
            if (!acknowledged.ContainsKey(AlertPlanner.KeyFor(uid, countdown.TargetAt))) continue;
            gone |= await store.DeleteCountdownAsync(countdown.Id, cancellationToken);
        }

        return gone;
    }

    /// <summary>
    /// The single ring has rung or has an outcome, and each step ring of its
    /// cycle has stopped ringing, was acknowledged after the last save, or
    /// was checked off. Skipped settles every step.
    /// </summary>
    private static bool StepsDone(
        AlertRoutine routine,
        DateTimeZone zone,
        Duration stopAfter,
        IReadOnlyDictionary<string, AlertAcknowledgement> acknowledged,
        IReadOnlySet<string> completed,
        RoutineOutcome? outcome,
        Instant now)
    {
        if (RoutineRings.Once(routine, zone) is not { } once) return false;
        if (outcome is { Skipped: true }) return true;
        if (outcome is null && once.At > now) return false;
        var (scheduled, at) = once;
        var cycle = RoutineRings.Stamp(scheduled);
        return AlertRoutines.Offsets(routine).All(pair =>
        {
            var key = RoutineRings.StepKey(routine.Id, cycle, pair.Step.Id);
            return at + Duration.FromMinutes(pair.Minutes) + stopAfter <= now
                   || completed.Contains(key)
                   || (acknowledged.TryGetValue(key, out var stop) && stop.At >= routine.UpdatedAt);
        });
    }
}
