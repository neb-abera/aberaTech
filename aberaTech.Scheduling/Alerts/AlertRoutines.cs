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
    Instant UpdatedAt);

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

        if (errors.Count > 0) return (errors, null);

        return (errors, new AlertRoutine(
            id,
            label,
            request.Hour!.Value,
            request.Minute!.Value,
            days,
            request.Enabled ?? true,
            request.SnoozeMinutes ?? DefaultSnoozeMinutes,
            now));
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

/// <summary>A routine alarm as the page and the phone send it. Every field is checked by <see cref="AlertRoutines.Validate"/>.</summary>
public sealed record RoutineRequest(
    string? Label,
    int? Hour,
    int? Minute,
    IReadOnlyList<int>? Days,
    bool? Enabled,
    int? SnoozeMinutes);
