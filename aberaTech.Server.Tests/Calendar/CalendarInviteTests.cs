using System.Net;
using System.Text.Json;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Calendar;

public class CalendarInviteTests
{
    /// <summary>
    /// The connect challenge must ask for the calendar scopes. It once did not:
    /// the handler's list stopped at "email", Google granted exactly that, and
    /// the capture step then refused every credential — so connecting a
    /// calendar silently stored nothing and the schedule offered time the host
    /// did not have.
    /// </summary>
    [Fact]
    public void Connecting_a_calendar_asks_for_the_calendar_scopes()
    {
        var scopes = CalendarAdminEndpoints.ConnectProperties().Scope;

        Assert.Contains(CalendarAdminEndpoints.ReadOnlyScope, scopes);
        Assert.Contains(CalendarAdminEndpoints.EventsScope, scopes);
    }

    [Fact]
    public void Connecting_still_signs_in_because_the_scope_override_replaces_the_list()
    {
        var scopes = CalendarAdminEndpoints.ConnectProperties().Scope;

        // Without these Google returns no address, and the admin allowlist
        // then has nothing to admit.
        Assert.Contains("openid", scopes);
        Assert.Contains("email", scopes);
    }

    [Fact]
    public void The_connect_grant_outlives_the_session()
    {
        var properties = CalendarAdminEndpoints.ConnectProperties();

        Assert.Equal("offline", properties.AccessType);
        Assert.Equal("consent", properties.Prompt);
    }

    [Fact]
    public void The_event_carries_the_visitor_and_the_exact_times()
    {
        var appointment = new Appointment
        {
            DisplayName = "Ada",
            Email = "ada@example.com",
            StartsAt = Instant.FromUtc(2027, 6, 1, 19, 40),
            EndsAt = Instant.FromUtc(2027, 6, 1, 19, 55)
        };

        using var body = JsonDocument.Parse(GoogleCalendarInvites.EventBody("Neb Abera", appointment));
        var root = body.RootElement;

        Assert.Equal("Neb Abera and Ada", root.GetProperty("summary").GetString());
        Assert.Equal("2027-06-01T19:40:00Z", root.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("2027-06-01T19:55:00Z", root.GetProperty("end").GetProperty("dateTime").GetString());
        Assert.Equal("ada@example.com",
            root.GetProperty("attendees")[0].GetProperty("email").GetString());
    }

    [Fact]
    public void The_event_id_is_the_bookings_id_in_a_form_google_accepts()
    {
        var appointment = new Appointment { Id = Guid.Parse("0f1d2c3b-4a59-4687-9a0b-1c2d3e4f5a6b"), Email = "ada@example.com" };

        var id = GoogleCalendarInvites.EventId(appointment);
        using var body = JsonDocument.Parse(GoogleCalendarInvites.EventBody("Neb Abera", appointment));

        Assert.Equal("0f1d2c3b4a5946879a0b1c2d3e4f5a6b", id);
        // Google's rule for a caller-chosen id: 5 to 1024 characters of a-v and 0-9.
        Assert.Matches("^[a-v0-9]{5,1024}$", id);
        Assert.Equal(id, body.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void A_booking_made_before_ids_were_chosen_keeps_googles_id()
    {
        var appointment = new Appointment { Id = Guid.NewGuid(), GoogleEventId = "abc123googlesown" };

        Assert.Equal("abc123googlesown", GoogleCalendarInvites.EventId(appointment));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true, false)]
    // The event exists: an earlier attempt made it and its reply was lost.
    [InlineData(HttpStatusCode.Conflict, true, false)]
    [InlineData(HttpStatusCode.BadRequest, false, true)]
    [InlineData(HttpStatusCode.Unauthorized, false, false)]
    [InlineData(HttpStatusCode.Forbidden, false, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false, false)]
    [InlineData(HttpStatusCode.InternalServerError, false, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false, false)]
    public void Googles_answer_to_a_create_decides_done_retry_or_stop(HttpStatusCode status, bool done, bool permanent)
    {
        var result = GoogleCalendarInvites.InterpretCreate(status, "abc12345");

        Assert.Equal(done, result.Accepted);
        Assert.Equal(permanent, result.Permanent);
        if (done)
        {
            // No receipt follows from Google, so the row settles here.
            Assert.True(result.DeliveryConfirmed);
            Assert.Equal("abc12345", result.ProviderMessageId);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.Gone, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public void Googles_answer_to_a_delete_counts_gone_as_done(HttpStatusCode status, bool done)
    {
        var result = GoogleCalendarInvites.InterpretDelete(status, "abc12345");

        Assert.Equal(done, result.Accepted);
        Assert.False(result.Permanent);
        if (done)
        {
            // Not the invite's id, which the outbox already holds once.
            Assert.Equal("withdraw-abc12345", result.ProviderMessageId);
        }
    }
}
