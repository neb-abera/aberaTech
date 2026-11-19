using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A routine alarm: a wall-clock time, the weekdays it repeats on, a label
/// and a snooze. It rings on a paired phone as a phone alarm. The server
/// stores it and never fires it, and never converts its time.
/// </summary>
/// <param name="Days">ISO weekdays, Monday 1 to Sunday 7, unique and sorted. Empty rings once at the next hour:minute.</param>
public sealed record AlertRoutine(
    Guid Id,
    string Label,
    int Hour,
    int Minute,
    IReadOnlyList<int> Days,
    bool Enabled,
    int SnoozeMinutes,
    Instant UpdatedAt)
{
    /// <summary>The steps that ring after it, in order. Each offset counts from the item before it.</summary>
    public IReadOnlyList<AlertRoutineStep> Steps { get; init; } = [];

    /// <summary>When it was first stored. History lists no cycle before it.</summary>
    public Instant CreatedAt { get; init; } = UpdatedAt;
}

/// <summary>
/// A step of a routine alarm: a sub-alarm that rings OffsetMinutes after the
/// item before it, the routine's own ring for the first step. Its id is the
/// phone's own when the phone made it. CreatedAt is when the server first
/// stored it, so history lists it only for cycles after that.
/// </summary>
public sealed record AlertRoutineStep(Guid Id, string Label, int OffsetMinutes, Instant CreatedAt);

/// <summary>A step as the page and the phone send it. A missing id gets a new one.</summary>
public sealed record RoutineStepRequest(Guid? Id, string? Label, int? OffsetMinutes);

/// <summary>The bounds of a routine alarm, and the check a create or an update passes before it is stored.</summary>
public static class AlertRoutines
{
    /// <summary>At most this many. A create beyond it is refused.</summary>
    public const int MaxRoutines = 50;

    public const int MaxLabelLength = 60;

    /// <summary>What an empty, blank or missing label is stored as.</summary>
    public const string DefaultLabel = "Alarm";

    public const int MinSnoozeMinutes = 1;

    public const int MaxSnoozeMinutes = 30;

    public const int DefaultSnoozeMinutes = 9;

    /// <summary>At most this many steps in one routine.</summary>
    public const int MaxSteps = 20;

    /// <summary>One offset, and the sum of them all: a day.</summary>
    public const int MaxStepOffsetMinutes = 1440;

    /// <summary>What an empty, blank or missing step label is stored as.</summary>
    public const string DefaultStepLabel = "Step";

    /// <summary>
    /// The fields that are wrong, by the name the body spells them, or the
    /// routine to store. A create may leave out enabled (true) and
    /// snoozeMinutes (9). An update sends the whole body.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, AlertRoutine? Valid) Validate(
        Guid id, RoutineRequest request, bool whole, Instant now)
    {
        var errors = new Dictionary<string, string[]>();

        var label = string.IsNullOrWhiteSpace(request.Label) ? DefaultLabel : request.Label.Trim();
        if (label.Length > MaxLabelLength || label.Any(char.IsControl))
        {
            errors["label"] = [$"At most {MaxLabelLength} characters, no control characters."];
        }

        if (request.Hour is not (>= 0 and <= 23)) errors["hour"] = ["0 to 23."];
        if (request.Minute is not (>= 0 and <= 59)) errors["minute"] = ["0 to 59."];

        int[] days = [];
        if (request.Days is not { } given
            || given.Any(day => day is < 1 or > 7)
            || given.Distinct().Count() != given.Count)
        {
            errors["days"] = ["Weekdays 1 (Monday) to 7 (Sunday), each once. Empty rings once."];
        }
        else
        {
            days = [.. given.Order()];
        }

        if (whole && request.Enabled is null) errors["enabled"] = ["true or false."];

        if (request.SnoozeMinutes is { } snooze ? snooze is < MinSnoozeMinutes or > MaxSnoozeMinutes : whole)
        {
            errors["snoozeMinutes"] = [$"{MinSnoozeMinutes} to {MaxSnoozeMinutes}."];
        }

        var steps = ValidateSteps(request.Steps, errors, now);

        if (errors.Count > 0) return (errors, null);

        return (errors, new AlertRoutine(
            id,
            label,
            request.Hour!.Value,
            request.Minute!.Value,
            days,
            request.Enabled ?? true,
            request.SnoozeMinutes ?? DefaultSnoozeMinutes,
            now)
        {
            Steps = steps ?? [],
        });
    }

    /// <summary>
    /// The steps to store, or null when the body sent none. Errors go under
    /// "steps" for the list and "steps[i].label" or "steps[i].offsetMinutes"
    /// for one step. Every step gets <paramref name="now"/> as its creation
    /// time. An update carries the stored time over (<see cref="KeepCreated"/>).
    /// </summary>
    private static List<AlertRoutineStep>? ValidateSteps(
        IReadOnlyList<RoutineStepRequest?>? given, Dictionary<string, string[]> errors, Instant now)
    {
        if (given is null) return null;

        if (given.Count > MaxSteps)
        {
            errors["steps"] = [$"At most {MaxSteps} steps."];
            return null;
        }

        var steps = new List<AlertRoutineStep>();
        var ids = new HashSet<Guid>();
        var sum = 0;
        for (var index = 0; index < given.Count; index++)
        {
            if (given[index] is not { } step)
            {
                errors[$"steps[{index}]"] = ["A step: label and offsetMinutes."];
                continue;
            }

            var label = string.IsNullOrWhiteSpace(step.Label) ? DefaultStepLabel : step.Label.Trim();
            if (label.Length > MaxLabelLength || label.Any(char.IsControl))
            {
                errors[$"steps[{index}].label"] = [$"At most {MaxLabelLength} characters, no control characters."];
            }

            if (step.OffsetMinutes is not (>= 0 and <= MaxStepOffsetMinutes) || step.OffsetMinutes is not { } offset)
            {
                errors[$"steps[{index}].offsetMinutes"] = [$"0 to {MaxStepOffsetMinutes} minutes after the item before it."];
                continue;
            }

            sum += offset;
            var id = step.Id is { } sent && sent != Guid.Empty ? sent : Guid.NewGuid();
            if (!ids.Add(id)) errors["steps"] = ["Each step id once."];
            steps.Add(new AlertRoutineStep(id, label, offset, now));
        }

        if (sum > MaxStepOffsetMinutes)
        {
            errors["steps"] = [$"The offsets add up to at most {MaxStepOffsetMinutes} minutes."];
        }

        return steps;
    }

    /// <summary>
    /// The steps an update stores: the ones it sent, each keeping the
    /// creation time of the stored step with its id, or the stored steps
    /// when it sent none.
    /// </summary>
    public static AlertRoutine KeepCreated(AlertRoutine update, AlertRoutine saved, bool stepsSent) =>
        update with
        {
            CreatedAt = saved.CreatedAt,
            Steps = stepsSent
                ? [.. update.Steps.Select(step =>
                    saved.Steps.FirstOrDefault(old => old.Id == step.Id) is { } old ? step with { CreatedAt = old.CreatedAt } : step)]
                : saved.Steps,
        };

    /// <summary>The minutes from the routine's own ring to each step's ring, in order.</summary>
    public static IReadOnlyList<(AlertRoutineStep Step, int Minutes)> Offsets(AlertRoutine routine)
    {
        var total = 0;
        return [.. routine.Steps.Select(step => (step, total += step.OffsetMinutes))];
    }

    /// <summary>The order the status lists them in: hour, minute, then label.</summary>
    public static IReadOnlyList<AlertRoutine> Sorted(IEnumerable<AlertRoutine> routines) =>
    [
        .. routines
            .OrderBy(routine => routine.Hour)
            .ThenBy(routine => routine.Minute)
            .ThenBy(routine => routine.Label, StringComparer.Ordinal)
            .ThenBy(routine => routine.Id)
    ];
}

/// <summary>
/// A routine alarm as the page and the phone send it. Every field is checked
/// by <see cref="AlertRoutines.Validate"/>. Steps null or missing on an
/// update keeps the stored steps, and an empty list removes them.
/// </summary>
public sealed record RoutineRequest(
    string? Label,
    int? Hour,
    int? Minute,
    IReadOnlyList<int>? Days,
    bool? Enabled,
    int? SnoozeMinutes,
    IReadOnlyList<RoutineStepRequest?>? Steps = null);
