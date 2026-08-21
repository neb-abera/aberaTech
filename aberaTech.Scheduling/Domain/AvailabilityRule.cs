using NodaTime;
using NodaTime.TimeZones;

namespace aberaTech.Scheduling.Domain;

/// <summary>
/// A recurring weekly period during which the host accepts appointments,
/// expressed in the host's own civil time.
/// </summary>
/// <remarks>
/// The rule deliberately stores a <see cref="LocalTime"/> and an IANA zone id
/// rather than an instant. "I am free Tuesdays from nine to five" is a statement
/// about a wall clock, and it stays true across a daylight saving transition
/// even though the number of elapsed hours it describes does not. Storing the
/// instant instead would silently freeze the rule to whichever offset was in
/// force the day it was written, which is the classic way a scheduler drifts by
/// an hour twice a year.
///
/// <see cref="Materialise"/> is the only place civil time becomes elapsed time,
/// and every consumer downstream works in instants.
/// </remarks>
public sealed record AvailabilityRule
{
    public AvailabilityRule(IsoDayOfWeek day, LocalTime start, LocalTime end, string zoneId)
    {
        if (day == IsoDayOfWeek.None)
        {
            throw new ArgumentException("A rule must name a day of the week.", nameof(day));
        }

        if (end <= start)
        {
            throw new ArgumentException(
                $"A rule must end after it starts; got {start} to {end}.",
                nameof(end));
        }

        Zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(zoneId)
               ?? throw new ArgumentException($"'{zoneId}' is not an IANA time zone id.", nameof(zoneId));

        Day = day;
        Start = start;
        End = end;
        ZoneId = zoneId;
    }

    public IsoDayOfWeek Day { get; }

    public LocalTime Start { get; }

    public LocalTime End { get; }

    public string ZoneId { get; }

    /// <summary>The zone resolved from <see cref="ZoneId"/> against NodaTime's own tzdb.</summary>
    /// <remarks>
    /// Resolved from the bundled tzdb rather than the operating system's, so the
    /// answer does not depend on how recently the base image was rebuilt. A
    /// container with stale tzdata is otherwise indistinguishable from correct
    /// until a government moves a transition date.
    /// </remarks>
    public DateTimeZone Zone { get; }

    /// <summary>
    /// Turns this rule into the concrete elapsed-time interval it describes on
    /// <paramref name="date"/>, or null if the rule does not apply that day.
    /// </summary>
    /// <remarks>
    /// Both boundaries are resolved leniently. On a spring-forward day a local
    /// time inside the gap does not exist at all, and on a fall-back day an
    /// ambiguous local time happens twice; the lenient resolver pushes the
    /// former forward and takes the earlier of the latter. That makes the
    /// returned interval shorter or longer than the wall-clock arithmetic
    /// suggests, which is the correct answer: a "01:00 to 05:00" window really
    /// is three hours long on the morning the clocks go forward.
    /// </remarks>
    public Interval? Materialise(LocalDate date)
    {
        if (date.DayOfWeek != Day)
        {
            return null;
        }

        var start = Zone.ResolveLocal(date.At(Start), Resolvers.LenientResolver).ToInstant();
        var end = Zone.ResolveLocal(date.At(End), Resolvers.LenientResolver).ToInstant();

        // Defensive. Lenient resolution is monotonic, so a rule validated as
        // start < end should always come back ordered; this guards the
        // invariant the rest of the planner assumes rather than describing a
        // transition that is known to break it.
        return end <= start ? null : new Interval(start, end);
    }
}
