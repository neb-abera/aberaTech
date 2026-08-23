namespace aberaTech.Scheduling.Calendar;

public sealed class GoogleCalendarOptions
{
    public const string Section = "GoogleCalendar";

    /// <summary>
    /// How long a free/busy answer is reused before asking Google again.
    /// </summary>
    /// <remarks>
    /// The booking page is public, so without a cache every visitor refreshing
    /// it becomes a Google API call, and a link sent to twenty-eight people at
    /// once becomes twenty-eight. A minute is short enough that an event added
    /// on a phone shows up while the host is still looking at the phone, and
    /// long enough that a burst of traffic costs one call.
    /// </remarks>
    public int CacheSeconds { get; set; } = 60;

    /// <summary>Give up rather than make somebody wait on a slow calendar.</summary>
    /// <remarks>
    /// A timeout here is not an error path, it is the normal degradation: the
    /// composite source logs it and carries on with local bookings, so a slow
    /// Google makes the page slightly less accurate rather than unavailable.
    /// </remarks>
    public int TimeoutSeconds { get; set; } = 5;
}
