using NodaTime;

namespace aberaTech.Scheduling;

/// <summary>
/// The handful of things about this deployment that are not code.
/// </summary>
public sealed class SchedulingOptions
{
    public const string Section = "Scheduling";

    /// <summary>The name messages are signed with, so they do not read as spam.</summary>
    public string HostName { get; set; } = "Neb";

    /// <summary>
    /// The host's own zone. Used to interpret availability rules and as the
    /// fallback for rendering a message when a visitor's browser reported a zone
    /// this build's tzdb does not recognise.
    /// </summary>
    public string HostZoneId { get; set; } = "America/New_York";

    /// <summary>How long each conversation is assumed to take unless told otherwise.</summary>
    public int DefaultAppointmentMinutes { get; set; } = 15;

    /// <summary>
    /// How far ahead a slot must be to be offered. A slot starting in four
    /// minutes is free on paper and useless in practice, and offering it invites
    /// a booking that races the appointment before it.
    /// </summary>
    public int BookingLeadMinutes { get; set; } = 30;

    /// <summary>The furthest ahead anybody may book.</summary>
    public int HorizonDays { get; set; } = 21;

    /// <summary>How long before an appointment the final reminder goes out.</summary>
    public int ReminderLeadMinutes { get; set; } = 60;

    /// <summary>
    /// How long before an appointment the earlier reminder goes out.
    /// </summary>
    /// <remarks>
    /// A day is the convention, and the reason is practical rather than
    /// aesthetic: it is the last point at which somebody can still rearrange
    /// their day, where an hour's notice only tells them they are about to be
    /// late.
    /// </remarks>
    public int EarlyReminderLeadMinutes { get; set; } = 24 * 60;

    /// <summary>
    /// Where to text the host about bookings and cancellations. Empty means
    /// they are not told.
    /// </summary>
    /// <remarks>
    /// Deliberately not the same as the queue notifications, which go to
    /// visitors. The host wants to know that something changed on their
    /// calendar, not to receive a copy of every message they caused.
    /// </remarks>
    public string HostPhoneE164 { get; set; } = string.Empty;

    public DateTimeZone HostZone =>
        DateTimeZoneProviders.Tzdb.GetZoneOrNull(HostZoneId)
        ?? throw new InvalidOperationException($"Scheduling:HostZoneId '{HostZoneId}' is not an IANA zone id.");
}
