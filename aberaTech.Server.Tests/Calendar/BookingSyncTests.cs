using System.Net;
using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Calendar;

/// <summary>
/// What a booking does about its Google Calendar event, and how Google's
/// answer is read. Pure: no database and no network.
/// </summary>
public class BookingSyncTests
{
    private static readonly Instant Start = Instant.FromUtc(2027, 6, 1, 19, 40);
    private static readonly Instant End = Instant.FromUtc(2027, 6, 1, 19, 55);

    private const string Guest = "ada@example.com";

    private static string Event(
        string start = "2027-06-01T19:40:00Z",
        string end = "2027-06-01T19:55:00Z",
        string status = "confirmed",
        string attendees = """[{"email":"ada@example.com","responseStatus":"needsAction"}]""") =>
        $$"""{"id":"abc","status":"{{status}}","start":{"dateTime":"{{start}}"},"end":{"dateTime":"{{end}}"},"attendees":{{attendees}}}""";

    [Fact]
    public void An_event_that_matches_the_booking_changes_nothing()
    {
        var google = BookingEvent.FromGoogle(HttpStatusCode.OK, Event(), Guest);

        Assert.Equal(BookingEventState.Scheduled, google.State);
        Assert.Equal(BookingSyncAction.Keep, BookingSync.Decide(Start, End, google).Action);
    }

    [Fact]
    public void A_decline_by_the_visitor_cancels_the_booking()
    {
        var google = BookingEvent.FromGoogle(
            HttpStatusCode.OK,
            Event(attendees: """[{"email":"Ada@Example.com","responseStatus":"declined"}]"""),
            Guest);

        Assert.True(google.Declined);
        Assert.Equal(BookingSyncAction.CancelDeclined, BookingSync.Decide(Start, End, google).Action);
    }

    [Theory]
    [InlineData("needsAction")]
    [InlineData("tentative")]
    [InlineData("accepted")]
    public void Any_other_reply_keeps_the_booking(string reply)
    {
        var google = BookingEvent.FromGoogle(
            HttpStatusCode.OK,
            Event(attendees: $$"""[{"email":"ada@example.com","responseStatus":"{{reply}}"}]"""),
            Guest);

        Assert.Equal(BookingSyncAction.Keep, BookingSync.Decide(Start, End, google).Action);
    }

    [Fact]
    public void A_decline_by_somebody_else_on_the_event_keeps_the_booking()
    {
        var google = BookingEvent.FromGoogle(
            HttpStatusCode.OK,
            Event(attendees:
                """[{"email":"ada@example.com","responseStatus":"accepted"},{"email":"other@example.com","responseStatus":"declined"},{"email":"host@example.com","self":true,"responseStatus":"declined"}]"""),
            Guest);

        Assert.False(google.Declined);
        Assert.Equal(BookingSyncAction.Keep, BookingSync.Decide(Start, End, google).Action);
    }

    [Fact]
    public void A_deleted_event_cancels_the_booking()
    {
        // Google's reference: get always returns a deleted event, as
        // status "cancelled", and only the id is guaranteed.
        var google = BookingEvent.FromGoogle(HttpStatusCode.OK, """{"id":"abc","status":"cancelled"}""", Guest);

        Assert.Equal(BookingEventState.Deleted, google.State);
        Assert.Equal(BookingSyncAction.CancelDeleted, BookingSync.Decide(Start, End, google).Action);
    }

    [Fact]
    public void A_moved_event_moves_the_booking_to_the_exact_instants()
    {
        // 3:00 PM to 3:30 PM Eastern on 2 June 2027 is 19:00 to 19:30 UTC.
        var google = BookingEvent.FromGoogle(
            HttpStatusCode.OK,
            Event(start: "2027-06-02T15:00:00-04:00", end: "2027-06-02T15:30:00-04:00"),
            Guest);

        var decision = BookingSync.Decide(Start, End, google);

        Assert.Equal(BookingSyncAction.Move, decision.Action);
        Assert.Equal(Instant.FromUtc(2027, 6, 2, 19, 0), decision.StartsAt);
        Assert.Equal(Instant.FromUtc(2027, 6, 2, 19, 30), decision.EndsAt);
    }

    [Fact]
    public void A_longer_event_at_the_same_start_moves_the_end()
    {
        var google = BookingEvent.FromGoogle(HttpStatusCode.OK, Event(end: "2027-06-01T20:10:00Z"), Guest);

        var decision = BookingSync.Decide(Start, End, google);

        Assert.Equal(BookingSyncAction.Move, decision.Action);
        Assert.Equal(Start, decision.StartsAt);
        Assert.Equal(Instant.FromUtc(2027, 6, 1, 20, 10), decision.EndsAt);
    }

    [Fact]
    public void A_decline_wins_over_a_move()
    {
        var google = BookingEvent.FromGoogle(
            HttpStatusCode.OK,
            Event(
                start: "2027-06-02T19:00:00Z",
                end: "2027-06-02T19:15:00Z",
                attendees: """[{"email":"ada@example.com","responseStatus":"declined"}]"""),
            Guest);

        Assert.Equal(BookingSyncAction.CancelDeclined, BookingSync.Decide(Start, End, google).Action);
    }

    [Theory]
    // A reconnected, different calendar answers 404 for every booking.
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void An_error_from_google_changes_nothing(HttpStatusCode status)
    {
        var google = BookingEvent.FromGoogle(status, """{"status":"cancelled"}""", Guest);

        Assert.Equal(BookingEventState.Unknown, google.State);
        Assert.Equal(BookingSyncAction.Keep, BookingSync.Decide(Start, End, google).Action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"cancelled\"")]
    [InlineData("{}")]
    [InlineData("""{"status":"confirmed"}""")]
    // An all-day event has a date and no dateTime.
    [InlineData("""{"status":"confirmed","start":{"date":"2027-06-01"},"end":{"date":"2027-06-02"}}""")]
    // No offset, so the instant is not known.
    [InlineData("""{"status":"confirmed","start":{"dateTime":"2027-06-01T19:40:00"},"end":{"dateTime":"2027-06-01T19:55:00"}}""")]
    // Ends before it starts.
    [InlineData("""{"status":"confirmed","start":{"dateTime":"2027-06-01T19:40:00Z"},"end":{"dateTime":"2027-06-01T19:00:00Z"}}""")]
    [InlineData("""{"status":"confirmed","start":"2027-06-01T19:40:00Z","end":7}""")]
    [InlineData("""{"status":7,"start":{"dateTime":7},"end":{"dateTime":null}}""")]
    public void A_body_that_is_not_a_timed_event_changes_nothing(string? body)
    {
        var google = BookingEvent.FromGoogle(HttpStatusCode.OK, body, Guest);

        Assert.Equal(BookingEventState.Unknown, google.State);
        Assert.NotNull(google.Reason);
        Assert.Equal(BookingSyncAction.Keep, BookingSync.Decide(Start, End, google).Action);
    }

    [Theory]
    [InlineData("""{"status":"confirmed","start":{"dateTime":"2027-06-01T19:40:00Z"},"end":{"dateTime":"2027-06-01T19:55:00Z"}}""")]
    [InlineData("""{"status":"confirmed","start":{"dateTime":"2027-06-01T19:40:00Z"},"end":{"dateTime":"2027-06-01T19:55:00Z"},"attendees":"declined"}""")]
    [InlineData("""{"status":"confirmed","start":{"dateTime":"2027-06-01T19:40:00Z"},"end":{"dateTime":"2027-06-01T19:55:00Z"},"attendees":[7,null,"declined",{"email":7,"responseStatus":"declined"}]}""")]
    public void Attendees_in_any_other_shape_are_not_a_decline(string body)
    {
        var google = BookingEvent.FromGoogle(HttpStatusCode.OK, body, Guest);

        Assert.Equal(BookingEventState.Scheduled, google.State);
        Assert.False(google.Declined);
    }

    [Fact]
    public void A_booking_with_no_email_is_never_declined()
    {
        var body = Event(attendees: """[{"email":"","responseStatus":"declined"},{"responseStatus":"declined"}]""");

        Assert.False(BookingEvent.FromGoogle(HttpStatusCode.OK, body, null).Declined);
        Assert.False(BookingEvent.FromGoogle(HttpStatusCode.OK, body, "").Declined);
    }

    /// <summary>
    /// The body is untrusted. Whatever arrives, the reader answers and never
    /// throws, and bytes that are not an event never cancel or move anything.
    /// </summary>
    [Fact]
    public void No_body_makes_the_reader_throw()
    {
        var random = new Random(20261115);
        var valid = Event(attendees: """[{"email":"ada@example.com","responseStatus":"declined"}]""");
        const string Alphabet = "{}[]\":,\\ \n\tabcdeltnsu0123456789-+.TZé😀\0";

        for (var round = 0; round < 20_000; round++)
        {
            string body;
            if (round % 2 == 0)
            {
                // Noise from the characters JSON is made of.
                var length = random.Next(0, 80);
                var noise = new char[length];
                for (var index = 0; index < length; index++) noise[index] = Alphabet[random.Next(Alphabet.Length)];
                body = new string(noise);
            }
            else
            {
                // A real event with a few characters cut, swapped or repeated.
                var characters = valid.ToCharArray().ToList();
                for (var edit = random.Next(1, 6); edit > 0 && characters.Count > 0; edit--)
                {
                    var at = random.Next(characters.Count);
                    switch (random.Next(3))
                    {
                        case 0: characters.RemoveAt(at); break;
                        case 1: characters[at] = Alphabet[random.Next(Alphabet.Length)]; break;
                        default: characters.Insert(at, characters[at]); break;
                    }
                }

                body = new string([.. characters]);
            }

            var google = BookingEvent.FromGoogle(HttpStatusCode.OK, body, Guest);
            var decision = BookingSync.Decide(Start, End, google);

            if (google.State == BookingEventState.Scheduled)
            {
                Assert.True(google.EndsAt > google.StartsAt, body);
            }

            if (decision.Action == BookingSyncAction.Move)
            {
                Assert.True(decision.EndsAt > decision.StartsAt, body);
            }
        }
    }

    [Fact]
    public void A_move_key_fits_the_column_and_differs_by_kind_and_second()
    {
        var id = Guid.Parse("0f1d2c3b-4a59-4687-9a0b-1c2d3e4f5a6b");
        var now = Instant.FromUtc(2099, 12, 31, 23, 59, 59);

        var key = BookingChanges.MoveKey(id, NotificationKind.HostMoved, now);

        // OutboxMessage.IdempotencyKey holds 64 characters.
        Assert.True(key.Length <= 64, key);
        Assert.NotEqual(key, BookingChanges.MoveKey(id, NotificationKind.Moved, now));
        Assert.NotEqual(key, BookingChanges.MoveKey(id, NotificationKind.HostMoved, now + Duration.FromSeconds(1)));
    }
}
