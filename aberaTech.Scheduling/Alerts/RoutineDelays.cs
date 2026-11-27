using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One ring of a routine moved later. The routine's own ring for that cycle
/// and each of its step rings ring Minutes after their planned time. Minutes
/// count from the plan, so a second delay replaces the first. Keys and the
/// cycle stamp stay as planned, so completions, outcomes and
/// acknowledgments still find their ring. CycleAt is the routine's planned
/// ring for that cycle when the delay was saved.
/// </summary>
public sealed record RoutineDelay(Guid RoutineId, string Cycle, int Minutes, Instant CycleAt, Instant At)
{
    public Duration Shift => Duration.FromMinutes(Minutes);
}

/// <summary>
/// The bounds of a delay and the rule for which rings it moves. Neb asked
/// for this on 2026-11-23: he does his morning beauty routine after his
/// workout on some days, and the alarm and its steps have to wait for one
/// ring. Retention and the cycle check are those of
/// <see cref="RoutineOutcomes"/>.
/// </summary>
public static class RoutineDelays
{
    public const int MinMinutes = 1;

    /// <summary>
    /// Seven days. Neb asked on 2026-11-26 to delay past three hours, with a
    /// number and a unit. Twelve hours until then.
    /// </summary>
    public const int MaxMinutes = 10080;

    /// <summary>The longest a delay moves a ring.</summary>
    public static readonly Duration Longest = Duration.FromMinutes(MaxMinutes);

    /// <summary>Delays by routine and cycle.</summary>
    public static IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineDelay> ByCycle(IEnumerable<RoutineDelay> delays) =>
        delays.ToDictionary(delay => (delay.RoutineId, delay.Cycle));

    /// <summary>How much later every ring of the cycle rings. Zero when it has no delay.</summary>
    public static Duration Shift(
        IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineDelay>? delays, Guid routineId, string cycle) =>
        delays is not null && delays.TryGetValue((routineId, cycle), out var delay) ? delay.Shift : Duration.Zero;

    /// <summary>
    /// The ring keys of one cycle, the routine's own and each step's, with
    /// the time each rings after a shift.
    /// </summary>
    public static IEnumerable<(string Key, Instant At)> CycleRings(AlertRoutine routine, string cycle, Instant cycleAt, Duration shift)
    {
        yield return ($"{RoutineRings.KeyPrefix}{routine.Id:D}:{cycle}", cycleAt + shift);
        foreach (var (step, minutes) in AlertRoutines.Offsets(routine, cycle))
        {
            yield return (RoutineRings.StepKey(routine.Id, cycle, step.Id), cycleAt + shift + Duration.FromMinutes(minutes));
        }
    }
}

/// <summary>The body of a delay: minutes after the planned ring, 1 to 10080 (7 days).</summary>
public sealed record DelayRequest(int? Minutes);

/// <summary>A delay as the status lists it, for cycles from two days ago on.</summary>
public sealed record RoutineDelayView(Guid RoutineId, string Cycle, int Minutes, DateTimeOffset At);

/// <summary>The check on a delay's body, naming its field as the body spells it.</summary>
public static class DelayFields
{
    public static (Dictionary<string, string[]> Errors, int? Minutes) Validate(DelayRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request?.Minutes is not (>= RoutineDelays.MinMinutes and <= RoutineDelays.MaxMinutes) || request.Minutes is not { } minutes)
        {
            errors["minutes"] = [$"{RoutineDelays.MinMinutes} to {RoutineDelays.MaxMinutes} minutes after the planned ring."];
            return (errors, null);
        }

        return (errors, minutes);
    }
}
