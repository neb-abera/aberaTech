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
    public void The_created_events_id_is_read_from_googles_response()
    {
        Assert.Equal("abc123", GoogleCalendarInvites.ParseEventId("""{"id":"abc123","status":"confirmed"}"""));
        Assert.Null(GoogleCalendarInvites.ParseEventId("""{"status":"confirmed"}"""));
    }
}
