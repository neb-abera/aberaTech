using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A one-time alarm deletes itself once its ring is acknowledged, on the
/// phone, in Pushover or in a browser. That is a routine alarm with no
/// repeat days, and a countdown with its alarm on that does not repeat. A
/// countdown's calendar event stays, as the record of the day. Neb asked
/// for this on 2026-11-03.
/// </summary>
public static class OneTimeAlarms
{
    /// <summary>Deletes every one-time alarm whose ring was acknowledged. True when any went.</summary>
    public static async Task<bool> ForgetAcknowledgedAsync(IAlertStore store, Instant now, CancellationToken cancellationToken)
    {
        var acknowledged = await store.AcknowledgementsAsync(cancellationToken);
        if (acknowledged.Count == 0) return false;
        var gone = false;

        foreach (var routine in await store.RoutinesAsync(cancellationToken))
        {
            if (routine.Days.Count > 0) continue;
            var prefix = $"{RoutineRings.KeyPrefix}{routine.Id:D}:";
            // A ring acknowledged before the last save was a ring of the routine as it was then.
            var rang = acknowledged.Any(pair =>
                pair.Key.StartsWith(prefix, StringComparison.Ordinal) && pair.Value.At >= routine.UpdatedAt);
            if (!rang) continue;
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
}
