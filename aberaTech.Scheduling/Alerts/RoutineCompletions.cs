using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One step checked off for one cycle of its routine, with an optional note
/// and photo as proof. The labels are snapshots, so history still reads
/// after the routine or the step is deleted. CycleAt is the routine's ring
/// for that cycle and StepAt the step's, both as planned when it was saved.
/// </summary>
public sealed record RoutineCompletion(
    Guid Id,
    Guid RoutineId,
    Guid StepId,
    string Cycle,
    string RoutineLabel,
    string StepLabel,
    Instant CycleAt,
    Instant StepAt,
    Instant CompletedAt,
    string? Note,
    bool HasPhoto)
{
    /// <summary>The key of the step ring this completion keeps from ringing.</summary>
    public string StepKey => RoutineRings.StepKey(RoutineId, Cycle, StepId);
}

/// <summary>
/// The bounds of a completion, and the history built from completions and
/// the routines' schedules. A step checked off does not ring for that cycle.
/// The next cycle sets every step again. Neb asked for this on 2026-11-19.
/// </summary>
public static class RoutineCompletions
{
    public const int MaxNoteLength = 2000;

    /// <summary>A photo's size once decoded.</summary>
    public const int MaxPhotoBytes = 2_000_000;

    /// <summary>The body the completion route takes: a photo in base64 and a note, with room for the JSON around them.</summary>
    public const long MaxBodyBytes = 3 * 1024 * 1024;

    /// <summary>How long completions, notes and photos are kept, and how far back history reads.</summary>
    public static readonly Duration KeepFor = Duration.FromDays(14);

    /// <summary>How far back the status lists completions, for the phone to skip completed step alarms.</summary>
    public static readonly Duration ListedFor = Duration.FromDays(2);

    /// <summary>How far ahead a cycle may be checked off: the next one may be done early.</summary>
    public static readonly Duration EarlyFor = Duration.FromDays(2);

    /// <summary>How far ahead of the server's clock a completion time may be, for a phone's clock that runs fast.</summary>
    public static readonly Duration ClockSkew = Duration.FromMinutes(5);

    /// <summary>A JPEG starts with FF D8 FF. Nothing else is stored.</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    /// <summary>
    /// The routine's ring for <paramref name="cycle"/> by its current
    /// schedule, from <see cref="KeepFor"/> ago to <see cref="EarlyFor"/>
    /// ahead. Null when the stamp names no such ring.
    /// </summary>
    public static Instant? CycleAt(AlertRoutine routine, string cycle, DateTimeZone zone, Instant now)
    {
        if (RoutineRings.ParseStamp(cycle) is null) return null;
        var from = now - KeepFor;
        var until = now + EarlyFor;
        foreach (var (scheduled, at) in RoutineRings.Cycles(routine, zone, from, until))
        {
            if (at < from || at > until) continue;
            if (RoutineRings.Stamp(scheduled) == cycle) return at;
        }

        return null;
    }

    /// <summary>
    /// Every cycle from <see cref="KeepFor"/> ago to now, newest first. A
    /// cycle is a ring of a current routine that is switched on and has
    /// steps, from its creation on. Each lists the steps that existed at
    /// their ring, with the completion or the acknowledgment if there was
    /// one. A completion whose routine, step or cycle is no longer in the
    /// schedule is listed too, under its routine and cycle, by the labels it
    /// kept. A cycle marked Done or Skipped carries its outcome, and one whose
    /// routine is gone, has no steps, or no longer rings then is listed by the
    /// label the outcome kept, once its ring time has come.
    /// </summary>
    public static HistoryView History(
        IReadOnlyList<AlertRoutine> routines,
        IReadOnlyList<RoutineCompletion> completions,
        IReadOnlyDictionary<string, AlertAcknowledgement> acknowledged,
        DateTimeZone zone,
        Instant now,
        IReadOnlyList<RoutineOutcome>? outcomes = null)
    {
        var from = now - KeepFor;
        var marked = (outcomes ?? [])
            .Where(outcome => outcome.CycleAt >= from && outcome.CycleAt <= now)
            .ToDictionary(outcome => (outcome.RoutineId, outcome.Cycle));
        var byKey = completions
            .Where(completion => completion.CycleAt >= from && completion.CycleAt <= now)
            .ToDictionary(completion => completion.StepKey, StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var cycles = new Dictionary<(Guid, string), HistoryCycle>();

        foreach (var routine in routines)
        {
            if (!routine.Enabled || routine.Steps.Count == 0) continue;
            foreach (var (scheduled, at) in RoutineRings.Cycles(routine, zone, from, now))
            {
                if (at < from || at > now || at < routine.CreatedAt) continue;
                var cycle = RoutineRings.Stamp(scheduled);
                var entry = new HistoryCycle(
                    routine.Id, routine.Label, cycle, at, acknowledged.GetValueOrDefault(RoutineRings.Key(routine.Id, scheduled)),
                    marked.GetValueOrDefault((routine.Id, cycle)));
                foreach (var (step, minutes) in AlertRoutines.Offsets(routine, scheduled.Date))
                {
                    var stepAt = at + Duration.FromMinutes(minutes);
                    var key = RoutineRings.StepKey(routine.Id, cycle, step.Id);
                    var completion = byKey.GetValueOrDefault(key);
                    if (step.CreatedAt > stepAt && completion is null) continue;
                    if (completion is not null) used.Add(key);
                    entry.Steps.Add(new HistoryStep(step.Id, step.Label, stepAt, acknowledged.GetValueOrDefault(key)?.At, completion));
                }

                cycles[(routine.Id, cycle)] = entry;
            }
        }

        // Completions the schedule no longer has a place for: a deleted
        // routine or step, a cycle that moved, a routine switched off.
        foreach (var completion in byKey.Values.Where(completion => !used.Contains(completion.StepKey)).OrderBy(completion => completion.StepAt))
        {
            if (!cycles.TryGetValue((completion.RoutineId, completion.Cycle), out var entry))
            {
                var parent = RoutineRings.ParseStamp(completion.Cycle) is { } scheduled
                    ? acknowledged.GetValueOrDefault(RoutineRings.Key(completion.RoutineId, scheduled))
                    : null;
                entry = new HistoryCycle(
                    completion.RoutineId, completion.RoutineLabel, completion.Cycle, completion.CycleAt, parent,
                    marked.GetValueOrDefault((completion.RoutineId, completion.Cycle)));
                cycles[(completion.RoutineId, completion.Cycle)] = entry;
            }

            entry.Steps.Add(new HistoryStep(
                completion.StepId, completion.StepLabel, completion.StepAt, acknowledged.GetValueOrDefault(completion.StepKey)?.At, completion));
        }

        // Outcomes the schedule no longer has a place for, and those of a
        // routine with no steps, which the schedule does not list.
        foreach (var outcome in marked.Values.Where(outcome => !cycles.ContainsKey((outcome.RoutineId, outcome.Cycle))))
        {
            var parent = RoutineRings.ParseStamp(outcome.Cycle) is { } scheduled
                ? acknowledged.GetValueOrDefault(RoutineRings.Key(outcome.RoutineId, scheduled))
                : null;
            cycles[(outcome.RoutineId, outcome.Cycle)] =
                new HistoryCycle(outcome.RoutineId, outcome.RoutineLabel, outcome.Cycle, outcome.CycleAt, parent, outcome);
        }

        return new HistoryView(
            (int)KeepFor.TotalDays,
            [
                .. cycles.Values
                    .OrderByDescending(cycle => cycle.At)
                    .ThenBy(cycle => cycle.RoutineId)
                    .Select(cycle => cycle.View())
            ]);
    }

    private sealed class HistoryCycle(
        Guid routineId, string label, string cycle, Instant at, AlertAcknowledgement? acknowledgement, RoutineOutcome? outcome)
    {
        public Guid RoutineId => routineId;

        public Instant At => at;

        public List<HistoryStep> Steps { get; } = [];

        public HistoryCycleView View() => new(
            routineId,
            label,
            cycle,
            at.ToDateTimeOffset(),
            acknowledgement?.At.ToDateTimeOffset(),
            acknowledgement?.Via,
            [.. Steps.Select(step => step.View())],
            outcome is { } marked ? new HistoryOutcomeView(marked.Status, marked.At.ToDateTimeOffset(), marked.Note) : null);
    }

    private sealed record HistoryStep(Guid StepId, string Label, Instant At, Instant? AcknowledgedAt, RoutineCompletion? Completion)
    {
        public HistoryStepView View() => new(
            StepId,
            Label,
            At.ToDateTimeOffset(),
            AcknowledgedAt?.ToDateTimeOffset(),
            Completion is { } done ? new HistoryCompletionView(done.Id, done.CompletedAt.ToDateTimeOffset(), done.Note, done.HasPhoto) : null);
    }
}

/// <summary>GET /api/alerts/history: the days kept, and every cycle in them, newest first.</summary>
public sealed record HistoryView(int Days, IReadOnlyList<HistoryCycleView> Cycles);

/// <summary>
/// One ring of a routine with steps, or of one marked Done or Skipped.
/// Cycle is the stamp the keys carry. At is the routine's ring. The
/// acknowledgment is the Stop of that ring. Outcome is null when the ring
/// was neither marked Done nor Skipped.
/// </summary>
public sealed record HistoryCycleView(
    Guid RoutineId,
    string Label,
    string Cycle,
    DateTimeOffset At,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedVia,
    IReadOnlyList<HistoryStepView> Steps,
    HistoryOutcomeView? Outcome);

/// <summary>One step of a cycle. Completion null means it was not checked off: missed, unless the cycle is still going.</summary>
public sealed record HistoryStepView(
    Guid StepId,
    string Label,
    DateTimeOffset At,
    DateTimeOffset? AcknowledgedAt,
    HistoryCompletionView? Completion);

/// <summary>A completion as history shows it. The photo is at /api/alerts/completions/{id}/photo when HasPhoto.</summary>
public sealed record HistoryCompletionView(Guid Id, DateTimeOffset CompletedAt, string? Note, bool HasPhoto);

/// <summary>A completion as the status lists it for the phone: which step of which cycle, and when. Never a note or a photo.</summary>
public sealed record RoutineCompletionView(
    Guid RoutineId, Guid StepId, string Cycle, DateTimeOffset CompletedAt, Guid Id, string? Note, bool HasPhoto);

/// <summary>
/// The body of a completion. Note null clears it. Photo null keeps the
/// stored photo, "" removes it, and anything else is a JPEG in base64.
/// CompletedAt null is now.
/// </summary>
public sealed record CompletionRequest(string? Note, string? Photo, string? CompletedAt);

/// <summary>A completion's body once checked: the time, the note, and what to do with the photo.</summary>
public sealed record ValidCompletion(Instant CompletedAt, string? Note, byte[]? Photo, bool KeepPhoto);

/// <summary>The checks on a completion's body, each naming its field as the body spells it.</summary>
public static class CompletionFields
{
    public static (Dictionary<string, string[]> Errors, ValidCompletion? Valid) Validate(CompletionRequest? request, Instant now)
    {
        var errors = new Dictionary<string, string[]>();
        request ??= new CompletionRequest(null, null, null);

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is not null && note.Length > RoutineCompletions.MaxNoteLength)
        {
            errors["note"] = [$"At most {RoutineCompletions.MaxNoteLength} characters."];
        }

        byte[]? photo = null;
        var keep = request.Photo is null;
        if (request.Photo is { Length: > 0 } text)
        {
            // Four base64 characters carry three bytes: a longer text cannot fit.
            if (text.Length > (RoutineCompletions.MaxPhotoBytes + 2) / 3 * 4)
            {
                errors["photo"] = [$"A JPEG of at most {RoutineCompletions.MaxPhotoBytes:N0} bytes."];
            }
            else
            {
                var buffer = new byte[text.Length / 4 * 3 + 3];
                if (!Convert.TryFromBase64String(text, buffer, out var written))
                {
                    errors["photo"] = ["A JPEG in base64."];
                }
                else if (written > RoutineCompletions.MaxPhotoBytes)
                {
                    errors["photo"] = [$"A JPEG of at most {RoutineCompletions.MaxPhotoBytes:N0} bytes."];
                }
                else if (!RoutineCompletions.IsJpeg(buffer.AsSpan(0, written)))
                {
                    errors["photo"] = ["A JPEG. Other images are not stored."];
                }
                else
                {
                    photo = buffer[..written];
                }
            }
        }

        var completedAt = now;
        if (request.CompletedAt is { } given)
        {
            if (NodaTime.Text.OffsetDateTimePattern.ExtendedIso.Parse(given.Trim()) is { Success: true } parsed
                && parsed.Value.ToInstant() is var at
                && at <= now + RoutineCompletions.ClockSkew
                && at >= now - RoutineCompletions.KeepFor)
            {
                completedAt = at;
            }
            else
            {
                errors["completedAt"] = ["An ISO 8601 time with its offset, from 14 days ago to now."];
            }
        }

        return errors.Count > 0 ? (errors, null) : (errors, new ValidCompletion(completedAt, note, photo, keep));
    }
}
