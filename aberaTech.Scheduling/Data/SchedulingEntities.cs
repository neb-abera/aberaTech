using aberaTech.Scheduling.Domain;
using NodaTime;

namespace aberaTech.Scheduling.Data;

/// <summary>A stored availability rule. See <see cref="AvailabilityRule"/> for why it holds civil time.</summary>
public class AvailabilityRuleRecord
{
    public Guid Id { get; set; }

    public IsoDayOfWeek Day { get; set; }

    public LocalTime StartsAt { get; set; }

    public LocalTime EndsAt { get; set; }

    public string ZoneId { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public AvailabilityRule ToDomain() => new(Day, StartsAt, EndsAt, ZoneId);
}

/// <summary>A booked appointment.</summary>
public class Appointment
{
    public Guid Id { get; set; }

    /// <summary>Stored as timestamptz. The zone below is for display only.</summary>
    public Instant StartsAt { get; set; }

    public Instant EndsAt { get; set; }

    /// <summary>
    /// The zone the visitor booked in, so a confirmation can be phrased in the
    /// time they actually saw rather than the host's.
    /// </summary>
    public string BookedZoneId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Empty when they declined texts, since there is then nothing to use it for.</summary>
    public string PhoneE164 { get; set; } = string.Empty;

    /// <summary>
    /// Null unless they asked for a calendar invite. Entering an address is the
    /// ask; there is no separate checkbox, because an optional field left blank
    /// already says no.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The Google Calendar event carrying the invite, so cancelling the booking
    /// can cancel the invitation too. Null when no invite was sent — no email
    /// given, no calendar connected, or the grant lacks the events scope.
    /// </summary>
    public string? GoogleEventId { get; set; }

    /// <summary>
    /// Whether they actively agreed to be texted about this appointment.
    /// </summary>
    /// <remarks>
    /// Recorded per booking rather than per person, because consent is given in
    /// a particular context for a particular purpose, and someone who agreed to
    /// a reminder in March has not thereby agreed to anything in November.
    ///
    /// The carrier rules require the box to be unticked until the visitor ticks
    /// it, so the default here is false and stays false unless they said yes.
    /// </remarks>
    public bool SmsConsent { get; set; }

    /// <summary>When they ticked the box, or null if they did not.</summary>
    /// <remarks>
    /// Consent is a thing that happened at a moment, and "did they agree" is a
    /// weaker record than "they agreed at this time, having been shown this".
    /// </remarks>
    public Instant? ConsentedAt { get; set; }

    /// <summary>
    /// The server's own copy of the wording shown when they agreed.
    /// </summary>
    /// <remarks>
    /// Stored per row rather than as a version number pointing at text held
    /// elsewhere, because the point of the record is to survive the text being
    /// changed later.
    /// </remarks>
    public string? ConsentDisclosure { get; set; }

    public Instant CreatedAt { get; set; }

    public bool Cancelled { get; set; }
}

public class QueueSession
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Instant OpensAt { get; set; }

    public Instant ClosesAt { get; set; }

    /// <summary>How long the host expects each conversation to take by default.</summary>
    public Duration DefaultDuration { get; set; } = Duration.FromMinutes(15);

    public bool Open { get; set; }

    public List<QueueEntryRecord> Entries { get; set; } = [];
}

public class QueueEntryRecord
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public QueueSession? Session { get; set; }

    public int Position { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string PhoneE164 { get; set; } = string.Empty;

    /// <summary>
    /// The IANA zone the visitor's browser reported when they joined. Every
    /// message they receive is rendered in it, so they are never shown a time in
    /// the host's zone or, worse, the server's UTC.
    /// </summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>Whether they actively agreed to be texted about this queue place.</summary>
    public bool SmsConsent { get; set; }

    /// <summary>When they ticked the box, or null if they did not.</summary>
    /// <remarks>
    /// Consent is a thing that happened at a moment, and "did they agree" is a
    /// weaker record than "they agreed at this time, having been shown this".
    /// </remarks>
    public Instant? ConsentedAt { get; set; }

    /// <summary>
    /// The server's own copy of the wording shown when they agreed.
    /// </summary>
    /// <remarks>
    /// Stored per row rather than as a version number pointing at text held
    /// elsewhere, because the point of the record is to survive the text being
    /// changed later.
    /// </remarks>
    public string? ConsentDisclosure { get; set; }


    public Duration Expected { get; set; }

    public QueueEntryState State { get; set; } = QueueEntryState.Waiting;

    public Instant JoinedAt { get; set; }

    public Instant? StartedAt { get; set; }

    /// <summary>
    /// The projected start most recently put in front of this person, and the
    /// two milestone flags. Persisted rather than recomputed because the whole
    /// point is to compare against what they were actually told, which is a
    /// fact about the past that no amount of recalculation can recover.
    /// </summary>
    public Instant? LastAnnouncedStart { get; set; }

    public bool ImminentSent { get; set; }

    public bool TurnSent { get; set; }

    public QueueEntry ToDomain() => new(Id, Position, Expected, State, StartedAt);

    public NotificationState ToNotificationState() => new(LastAnnouncedStart, ImminentSent, TurnSent);
}

/// <summary>
/// The host's stored Google authorisation, so free/busy can be read without him
/// being present.
/// </summary>
/// <remarks>
/// One row. A refresh token is a long-lived key to somebody's calendar, so it is
/// stored encrypted rather than in plain text: the database is encrypted at rest
/// by the platform, but that protects against a stolen disk, not against
/// anything that can run a SELECT. Encrypting the column means a leaked backup
/// or an accidental dump does not hand over the calendar with it.
/// </remarks>
public class HostCalendarCredential
{
    public Guid Id { get; set; }

    /// <summary>Protected with ASP.NET Data Protection, never the raw token.</summary>
    public string ProtectedRefreshToken { get; set; } = string.Empty;

    /// <summary>Which calendar to read. "primary" unless the host says otherwise.</summary>
    public string CalendarId { get; set; } = "primary";

    /// <summary>
    /// The scopes Google actually granted, space separated as Google returns
    /// them. What the grant can do is a property of the grant, not of whatever
    /// the code happens to request today: a credential stored before the events
    /// scope was asked for can read free/busy but cannot send invites, and the
    /// only honest way to know is to have kept the answer.
    /// </summary>
    public string GrantedScopes { get; set; } = string.Empty;

    /// <summary>The account that granted access, so the page can say whose calendar this is.</summary>
    public string ConnectedEmail { get; set; } = string.Empty;

    public Instant ConnectedAt { get; set; }
}

/// <summary>
/// A number that has asked not to be messaged.
/// </summary>
/// <remarks>
/// The carrier stops delivery the moment somebody replies STOP, so this is not
/// what protects them. What it does is stop this application from queueing
/// messages that cannot arrive: without it, every future booking would produce
/// rows that fail, dead letter, and spend a daily allowance the deliverable
/// messages have to come out of.
///
/// It also means the suppression survives here rather than living only inside
/// the provider, which is what "honour opt-outs" is actually asking for.
/// </remarks>
public class SmsOptOut
{
    public Guid Id { get; set; }

    public string PhoneE164 { get; set; } = string.Empty;

    public Instant OptedOutAt { get; set; }

    /// <summary>How it was learned, for when somebody asks why they stopped hearing from us.</summary>
    public string Reason { get; set; } = string.Empty;
}
