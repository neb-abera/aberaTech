using NodaTime;

namespace aberaTech.Scheduling.Domain;

/// <summary>
/// The rules one booking link books under.
/// </summary>
/// <param name="EarliestDate">
/// The first day anybody holding the link may book, as a date on the host's
/// calendar. Null means from today.
/// </param>
/// <param name="HorizonDays">How many days past today the last bookable day is.</param>
/// <param name="LeadMinutes">How far ahead of now a slot must start to be offered.</param>
/// <param name="AppointmentMinutes">How long each meeting is.</param>
/// <param name="ShowsQueue">Whether an open queue replaces the slots for this link.</param>
public sealed record BookingPolicy(
    LocalDate? EarliestDate,
    int HorizonDays,
    int LeadMinutes,
    int AppointmentMinutes,
    bool ShowsQueue);

/// <summary>What a policy may book, from a given moment.</summary>
/// <param name="From">The first day to plan, in the host's zone.</param>
/// <param name="To">The last day to plan, in the host's zone. Before From when nothing can be booked.</param>
/// <param name="NotBefore">No slot starting before this instant is offered.</param>
public readonly record struct BookingWindow(LocalDate From, LocalDate To, Instant NotBefore)
{
    /// <summary>Whether the window holds any day at all.</summary>
    public bool IsEmpty => To < From;

    /// <summary>
    /// The window <paramref name="policy"/> books in at <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// The one place the earliest date becomes an instant. The page and the
    /// booking check both call it, so the times a visitor is shown and the
    /// times the server accepts cannot drift apart.
    ///
    /// The earliest date is a date on the host's calendar: 16 November means
    /// midnight on 16 November where the host is. A visitor elsewhere sees it
    /// open at that same instant in their own time.
    /// </remarks>
    public static BookingWindow For(BookingPolicy policy, Instant now, DateTimeZone hostZone)
    {
        var today = now.InZone(hostZone).Date;
        var from = policy.EarliestDate is { } earliest && earliest > today ? earliest : today;
        var to = today.PlusDays(policy.HorizonDays);

        var notBefore = now + Duration.FromMinutes(policy.LeadMinutes);

        if (policy.EarliestDate is { } opens)
        {
            var opensAt = hostZone.AtStartOfDay(opens).ToInstant();
            if (opensAt > notBefore)
            {
                notBefore = opensAt;
            }
        }

        return new BookingWindow(from, to, notBefore);
    }
}
