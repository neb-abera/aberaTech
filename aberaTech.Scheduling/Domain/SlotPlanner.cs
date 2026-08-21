using NodaTime;

namespace aberaTech.Scheduling.Domain;

/// <summary>A bookable period of elapsed time.</summary>
public readonly record struct Slot(Instant Start, Instant End)
{
    public Duration Length => End - Start;

    public bool Overlaps(Interval other) => Start < other.End && other.Start < End;
}

/// <summary>
/// Turns availability rules and a calendar's busy periods into the concrete
/// slots a visitor may book.
/// </summary>
/// <remarks>
/// Pure: no clock, no database, no calendar client. Everything it needs arrives
/// as an argument, so the daylight saving and boundary behaviour can be tested
/// exhaustively without standing anything up. The impure edges — reading Google
/// Calendar, reading the rules — live in the API layer and hand their results
/// here.
/// </remarks>
public static class SlotPlanner
{
    /// <summary>
    /// Every slot of <paramref name="slotLength"/> that fits inside the given
    /// rules over the inclusive date range, minus anything overlapping
    /// <paramref name="busy"/>, minus anything starting before
    /// <paramref name="notBefore"/>.
    /// </summary>
    /// <param name="notBefore">
    /// Usually now plus a lead time. A slot that starts in four minutes is
    /// technically free and practically useless, and offering it invites a
    /// booking that races the appointment it is booking against.
    /// </param>
    public static IReadOnlyList<Slot> Plan(
        IEnumerable<AvailabilityRule> rules,
        LocalDate from,
        LocalDate to,
        Duration slotLength,
        IReadOnlyCollection<Interval> busy,
        Instant notBefore)
    {
        if (slotLength <= Duration.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(slotLength), "A slot must have positive length.");
        }

        if (to < from)
        {
            throw new ArgumentException("The range must end on or after it starts.", nameof(to));
        }

        var ruleList = rules as IReadOnlyList<AvailabilityRule> ?? rules.ToList();
        var slots = new List<Slot>();

        for (var date = from; date <= to; date = date.PlusDays(1))
        {
            foreach (var rule in ruleList)
            {
                if (rule.Materialise(date) is not { } window)
                {
                    continue;
                }

                slots.AddRange(Divide(window, slotLength));
            }
        }

        // Sorting before filtering keeps the result stable when two rules in
        // different zones produce interleaved windows for the same day.
        slots.Sort((left, right) => left.Start.CompareTo(right.Start));

        return slots
            .Where(slot => slot.Start >= notBefore)
            .Where(slot => !busy.Any(slot.Overlaps))
            .ToList();
    }

    /// <summary>
    /// Cuts a window into whole slots, discarding any remainder at the end.
    /// </summary>
    /// <remarks>
    /// The walk adds elapsed durations to an instant rather than incrementing a
    /// wall clock, so a window that spans a daylight saving transition yields
    /// the number of slots that will really fit in it: six half hours in the
    /// "01:00 to 05:00" that is three hours long the morning the clocks move.
    /// A partial slot at the end is dropped rather than truncated, because a
    /// twenty minute booking sold as thirty overruns whatever follows it.
    /// </remarks>
    private static IEnumerable<Slot> Divide(Interval window, Duration slotLength)
    {
        for (var start = window.Start; start + slotLength <= window.End; start += slotLength)
        {
            yield return new Slot(start, start + slotLength);
        }
    }
}
