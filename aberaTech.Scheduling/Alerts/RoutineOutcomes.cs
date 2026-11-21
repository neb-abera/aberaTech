using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One ring of a routine marked Done or Skipped. Done keeps the routine's
/// own ring for that cycle from ringing, and its steps still ring unless
/// each is checked off. Skipped keeps the routine's ring and every step ring
/// of the cycle from ringing. RoutineLabel is a snapshot, so history still
/// reads after the routine is deleted. CycleAt is the routine's ring for
/// that cycle as planned when it was saved.
/// </summary>
public sealed record RoutineOutcome(
    Guid RoutineId,
    string Cycle,
    string Status,
    string RoutineLabel,
    Instant CycleAt,
    Instant At,
    string? Note)
{
    /// <summary>The key of the routine's own ring for this cycle.</summary>
    public string Key => $"{RoutineRings.KeyPrefix}{RoutineId:D}:{Cycle}";

    public bool Skipped => Status == RoutineOutcomes.SkippedStatus;
}

/// <summary>
/// The statuses an outcome takes, and the rule for which rings an outcome
/// keeps from ringing. Neb asked for this on 2026-11-21, after he did his
/// morning routine before the 07:00 alarm and had to switch the alarm off
/// and on again. Bounds, retention and the cycle check are those of
/// <see cref="RoutineCompletions"/>.
/// </summary>
public static class RoutineOutcomes
{
    public const string DoneStatus = "done";

    public const string SkippedStatus = "skipped";

    /// <summary>The longest status, for the column.</summary>
    public const int MaxStatusLength = 7;

    /// <summary>Outcomes by routine and cycle.</summary>
    public static IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineOutcome> ByCycle(IEnumerable<RoutineOutcome> outcomes) =>
        outcomes.ToDictionary(outcome => (outcome.RoutineId, outcome.Cycle));

    /// <summary>
    /// Whether an outcome keeps a ring from ringing: the routine's own ring
    /// for either status, a step ring for Skipped.
    /// </summary>
    public static bool Silences(
        IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineOutcome>? outcomes, Guid routineId, string cycle, Guid? stepId) =>
        outcomes is not null
        && outcomes.TryGetValue((routineId, cycle), out var outcome)
        && (stepId is null || outcome.Skipped);

    /// <summary>Whether an outcome keeps the ring with this key from ringing. False for a key that is not a routine ring's.</summary>
    public static bool Silences(IReadOnlyDictionary<(Guid RoutineId, string Cycle), RoutineOutcome>? outcomes, string key) =>
        RoutineRings.ParseKey(key) is { } parsed && Silences(outcomes, parsed.RoutineId, parsed.Cycle, parsed.StepId);
}

/// <summary>
/// The body of an outcome. Status "done" or "skipped". Note null clears it.
/// At null is now.
/// </summary>
public sealed record OutcomeRequest(string? Status, string? Note, string? At);

/// <summary>An outcome's body once checked.</summary>
public sealed record ValidOutcome(string Status, Instant At, string? Note);

/// <summary>An outcome as the status lists it, for cycles from two days ago on.</summary>
public sealed record RoutineOutcomeView(Guid RoutineId, string Cycle, string Status, DateTimeOffset At, string? Note);

/// <summary>An outcome as history shows it on its cycle.</summary>
public sealed record HistoryOutcomeView(string Status, DateTimeOffset At, string? Note);

/// <summary>The checks on an outcome's body, each naming its field as the body spells it.</summary>
public static class OutcomeFields
{
    public static (Dictionary<string, string[]> Errors, ValidOutcome? Valid) Validate(OutcomeRequest? request, Instant now)
    {
        var errors = new Dictionary<string, string[]>();
        request ??= new OutcomeRequest(null, null, null);

        if (request.Status is not (RoutineOutcomes.DoneStatus or RoutineOutcomes.SkippedStatus))
        {
            errors["status"] = ["\"done\" or \"skipped\"."];
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is not null && note.Length > RoutineCompletions.MaxNoteLength)
        {
            errors["note"] = [$"At most {RoutineCompletions.MaxNoteLength} characters."];
        }

        var at = now;
        if (request.At is { } given)
        {
            if (NodaTime.Text.OffsetDateTimePattern.ExtendedIso.Parse(given.Trim()) is { Success: true } parsed
                && parsed.Value.ToInstant() is var instant
                && instant <= now + RoutineCompletions.ClockSkew
                && instant >= now - RoutineCompletions.KeepFor)
            {
                at = instant;
            }
            else
            {
                errors["at"] = ["An ISO 8601 time with its offset, from 14 days ago to now."];
            }
        }

        return errors.Count > 0 ? (errors, null) : (errors, new ValidOutcome(request.Status!, at, note));
    }
}
