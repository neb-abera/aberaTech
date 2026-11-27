using NodaTime;
using NodaTime.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// A routine alarm: a wall-clock time, how it repeats, a label and a
/// snooze. It rings on a paired phone as a phone alarm. The server stores
/// it and never fires it, and never converts its time.
/// </summary>
/// <param name="Days">
/// ISO weekdays, Monday 1 to Sunday 7, unique and sorted. Empty with
/// Repeat "weekdays" rings once at the next hour:minute. Always empty with
/// Repeat "interval".
/// </param>
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

    /// <summary>"weekdays": on Days, every Every weeks. "interval": every Every Units from StartsOn.</summary>
    public string Repeat { get; init; } = RoutineRepeats.Weekdays;

    /// <summary>The N of "every N": 1 to 999.</summary>
    public int Every { get; init; } = 1;

    /// <summary>minutes, hours, days, weeks, months or years. Always weeks with Repeat "weekdays".</summary>
    public string Unit { get; init; } = RoutineRepeats.Weeks;

    /// <summary>
    /// The local date the series counts from. Null for weekdays every week,
    /// which counts from nothing.
    /// </summary>
    public LocalDate? StartsOn { get; init; }

    /// <summary>It rings once and then deletes itself: weekdays with no days.</summary>
    public bool RingsOnce => Repeat == RoutineRepeats.Weekdays && Days.Count == 0;

    /// <summary>It rings again after a ring: anything that does not ring once.</summary>
    public bool Repeats => !RingsOnce;
}

/// <summary>How a routine alarm repeats, as the page and the phone spell it.</summary>
public static class RoutineRepeats
{
    public const string Weekdays = "weekdays";

    public const string Interval = "interval";

    public const string Minutes = "minutes";

    public const string Hours = "hours";

    public const string Days = "days";

    public const string Weeks = "weeks";

    public const string Months = "months";

    public const string Years = "years";

    /// <summary>The column width of Repeat and of Unit.</summary>
    public const int MaxLength = 16;

    public const int MinEvery = 1;

    public const int MaxEvery = 999;

    public static readonly IReadOnlyList<string> Choices = [Weekdays, Interval];

    public static readonly IReadOnlyList<string> Units = [Minutes, Hours, Days, Weeks, Months, Years];

    private static readonly LocalDatePattern Date = LocalDatePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd");

    /// <summary>"2026-11-25": StartsOn as the page and the phone read and send it.</summary>
    public static string? Format(LocalDate? date) => date is { } given ? Date.Format(given) : null;

    /// <summary>The date a "yyyy-MM-dd" text names, or null when it names none.</summary>
    public static LocalDate? Parse(string? text) =>
        text is { Length: 10 } && Date.Parse(text) is { Success: true } parsed ? parsed.Value : null;
}

/// <summary>
/// A step of a routine alarm: a sub-alarm that rings OffsetMinutes after the
/// item before it, the routine's own ring for the first step. Its id is the
/// phone's own when the phone made it. CreatedAt is when the server first
/// stored it, so history lists it only for cycles after that.
/// </summary>
public sealed record AlertRoutineStep(Guid Id, string Label, int OffsetMinutes, Instant CreatedAt)
{
    /// <summary>1 rings at every ring of the routine. 2 rings every other day, from StartsOn.</summary>
    public int EveryDays { get; init; } = 1;

    /// <summary>The first day it rings when EveryDays is above 1, in the routine's zone. Null when EveryDays is 1.</summary>
    public LocalDate? StartsOn { get; init; }

    /// <summary>
    /// True when it rings in the routine's ring on <paramref name="date"/>:
    /// every ring, or a whole number of EveryDays from StartsOn, before or after it.
    /// </summary>
    public bool RingsOn(LocalDate date) =>
        EveryDays <= 1 || StartsOn is not { } start
        || ((Period.Between(start, date, PeriodUnits.Days).Days % EveryDays) + EveryDays) % EveryDays == 0;
}

/// <summary>
/// A step as the page and the phone send it. A missing id gets a new one.
/// EveryDays missing is 1. StartsOn is "yyyy-MM-dd", and needed when EveryDays is above 1.
/// </summary>
public sealed record RoutineStepRequest(Guid? Id, string? Label, int? OffsetMinutes, int? EveryDays = null, string? StartsOn = null);

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

    /// <summary>A step rings at most this many days apart.</summary>
    public const int MaxEveryDays = 365;

    /// <summary>What an empty, blank or missing step label is stored as.</summary>
    public const string DefaultStepLabel = "Step";

    /// <summary>
    /// The fields that are wrong, by the name the body spells them, or the
    /// routine to store. A create may leave out enabled (true) and
    /// snoozeMinutes (9). An update sends the whole body. Repeat, every,
    /// unit and startsOn may be left out of either: a create takes weekdays,
    /// 1, weeks and no date, and an update keeps what <paramref name="saved"/>
    /// holds, so a phone build that knows none of them erases none of them.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, AlertRoutine? Valid) Validate(
        Guid id, RoutineRequest request, bool whole, Instant now, AlertRoutine? saved = null)
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

        var (repeat, every, unit, startsOn) = ValidateRepeat(request, saved, days, errors);

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
            Repeat = repeat,
            Every = every,
            Unit = unit,
            StartsOn = startsOn,
        });
    }

    /// <summary>
    /// How the routine repeats, once checked. Weekdays repeat by weeks, and
    /// every 2 weeks or more needs a day and a start date. Weekdays every
    /// week stores no start date. An interval takes no days and needs a
    /// start date.
    /// </summary>
    private static (string Repeat, int Every, string Unit, LocalDate? StartsOn) ValidateRepeat(
        RoutineRequest request, AlertRoutine? saved, int[] days, Dictionary<string, string[]> errors)
    {
        var repeat = request.Repeat ?? saved?.Repeat ?? RoutineRepeats.Weekdays;
        var every = request.Every ?? saved?.Every ?? RoutineRepeats.MinEvery;
        var unit = request.Unit ?? saved?.Unit ?? RoutineRepeats.Weeks;
        var startsOn = request.StartsOn is null ? saved?.StartsOn : RoutineRepeats.Parse(request.StartsOn);

        if (!RoutineRepeats.Choices.Contains(repeat)) errors["repeat"] = ["weekdays or interval."];
        if (!RoutineRepeats.Units.Contains(unit)) errors["unit"] = ["minutes, hours, days, weeks, months or years."];
        if (every is < RoutineRepeats.MinEvery or > RoutineRepeats.MaxEvery)
        {
            errors["every"] = [$"{RoutineRepeats.MinEvery} to {RoutineRepeats.MaxEvery}."];
        }

        if (request.StartsOn is not null && startsOn is null) errors["startsOn"] = ["A date as yyyy-MM-dd."];

        if (repeat == RoutineRepeats.Weekdays)
        {
            if (!errors.ContainsKey("unit") && unit != RoutineRepeats.Weeks) errors["unit"] = ["weeks when it repeats on weekdays."];
            if (every == RoutineRepeats.MinEvery)
            {
                // Every week counts from nothing. A date sent with it is not kept.
                errors.Remove("startsOn");
                startsOn = null;
            }
            else if (!errors.ContainsKey("every"))
            {
                if (days.Length == 0 && !errors.ContainsKey("days")) errors["days"] = ["At least one weekday when it repeats every 2 weeks or more."];
                if (startsOn is null && !errors.ContainsKey("startsOn")) errors["startsOn"] = ["The date the weeks count from, as yyyy-MM-dd."];
            }
        }
        else if (repeat == RoutineRepeats.Interval)
        {
            if (days.Length > 0) errors["days"] = ["Empty when it repeats by an interval."];
            if (startsOn is null && !errors.ContainsKey("startsOn")) errors["startsOn"] = ["The date the interval counts from, as yyyy-MM-dd."];
        }

        return (repeat, every, unit, startsOn);
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

            var every = step.EveryDays ?? 1;
            if (every is < 1 or > MaxEveryDays)
            {
                errors[$"steps[{index}].everyDays"] = [$"1 (every ring) to {MaxEveryDays} days."];
                continue;
            }

            LocalDate? startsOn = null;
            if (every > 1)
            {
                if (step.StartsOn is not { } text
                    || !LocalDatePattern.Iso.Parse(text).TryGetValue(default, out var date))
                {
                    errors[$"steps[{index}].startsOn"] = ["The first day it rings, yyyy-MM-dd."];
                    continue;
                }

                startsOn = date;
            }

            sum += offset;
            var id = step.Id is { } sent && sent != Guid.Empty ? sent : Guid.NewGuid();
            if (!ids.Add(id)) errors["steps"] = ["Each step id once."];
            steps.Add(new AlertRoutineStep(id, label, offset, now) { EveryDays = every, StartsOn = startsOn });
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

    /// <summary>
    /// The minutes from the routine's own ring to each step's ring, in order,
    /// for its ring on <paramref name="date"/>. A step that does not ring that
    /// day is left out and takes no time: the step after it follows the one
    /// before it by its own offset. Neb asked on 2026-11-26 for one night
    /// routine with tretinoin and night serum on alternate days.
    /// </summary>
    public static IReadOnlyList<(AlertRoutineStep Step, int Minutes)> Offsets(AlertRoutine routine, LocalDate date)
    {
        var total = 0;
        return [.. routine.Steps.Where(step => step.RingsOn(date)).Select(step => (step, total += step.OffsetMinutes))];
    }

    /// <summary>The steps of the ring with this stamp, or none when the stamp is not one.</summary>
    public static IReadOnlyList<(AlertRoutineStep Step, int Minutes)> Offsets(AlertRoutine routine, string cycle) =>
        RoutineRings.ParseStamp(cycle) is { } scheduled ? Offsets(routine, scheduled.Date) : [];

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
/// update keeps the stored steps, and an empty list removes them. Repeat,
/// Every, Unit and StartsOn null or missing take the defaults on a create
/// and keep the stored values on an update.
/// </summary>
public sealed record RoutineRequest(
    string? Label,
    int? Hour,
    int? Minute,
    IReadOnlyList<int>? Days,
    bool? Enabled,
    int? SnoozeMinutes,
    IReadOnlyList<RoutineStepRequest?>? Steps = null,
    string? Repeat = null,
    int? Every = null,
    string? Unit = null,
    string? StartsOn = null);
