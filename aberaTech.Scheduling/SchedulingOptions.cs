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

    /// <summary>How many days of availability the booking page shows at once.</summary>
    public int HorizonDays { get; set; } = 21;

    public DateTimeZone HostZone =>
        DateTimeZoneProviders.Tzdb.GetZoneOrNull(HostZoneId)
        ?? throw new InvalidOperationException($"Scheduling:HostZoneId '{HostZoneId}' is not an IANA zone id.");
}
