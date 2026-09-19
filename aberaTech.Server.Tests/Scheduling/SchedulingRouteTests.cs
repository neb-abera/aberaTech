using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Scheduling;

/// <summary>
/// The public booking routes, from the entry point inward. There is no
/// account here by design: the page is for anybody, and the id a booking or
/// a place in the line comes back with is the whole capability to read and
/// cancel it. So the tests are about what the routes give away — nothing
/// about anybody else — and what an id somebody was never given can do,
/// which is nothing.
/// </summary>
public sealed class SchedulingRouteTests : IDisposable
{
    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public SchedulingRouteTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("routes");
        _app = new TestApp(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Scheduling"] = _database.ConnectionString,
            ["ClientAddress:ForwardedHops"] = "0"
        });
    }

    [Fact]
    public async Task Without_a_database_the_state_route_still_answers_and_says_so()
    {
        // The route the page loads first, on a deployment with nothing behind
        // it: JSON that says unavailable, never the HTML shell and never a 500.
        using var app = new TestApp(new Dictionary<string, string?>());
        using var visitor = app.CreateClient();

        using var response = await visitor.GetAsync("/api/scheduling/state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unavailable", state.GetProperty("mode").GetString());
        Assert.Equal("Neb Abera", state.GetProperty("hostName").GetString());
    }

    [PostgresFact]
    public async Task The_state_offers_days_and_times_and_says_nothing_about_anybody()
    {
        await OpenEveryDayAsync();
        using var visitor = _app!.CreateClient();

        using var response = await visitor.GetAsync("/api/scheduling/state?zone=Etc/UTC");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var state = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("slots", state.GetProperty("mode").GetString());
        Assert.Equal("Etc/UTC", state.GetProperty("viewerZoneId").GetString());
        Assert.True(state.GetProperty("availableDates").GetArrayLength() > 0);
        Assert.True(state.GetProperty("slots").GetArrayLength() > 0);
        Assert.Equal(15, state.GetProperty("slots")[0].GetProperty("minutes").GetInt32());
        Assert.DoesNotContain("displayName", raw);
        Assert.DoesNotContain("phone", raw, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task A_booking_is_confirmed_takes_its_time_off_the_page_and_is_cancelled_by_its_id()
    {
        await OpenEveryDayAsync();
        using var visitor = _app!.CreateClient();
        var (date, startsAt) = await FirstSlotAsync(visitor);

        using var booked = await visitor.PostAsJsonAsync("/api/scheduling/book", new
        {
            startsAt, name = "Private Snuffy", zoneId = "Etc/UTC", smsConsent = false
        });

        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var confirmation = await booked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(startsAt, confirmation.GetProperty("startsAt").GetString());
        var id = confirmation.GetProperty("id").GetGuid();
        Assert.Equal(4, id.Version);

        Assert.DoesNotContain(startsAt, await SlotsOnAsync(visitor, date));

        using var cancelled = await visitor.DeleteAsync($"/api/scheduling/book/{id}");
        using var again = await visitor.DeleteAsync($"/api/scheduling/book/{id}");

        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        // A second press of the same link is the usual case, not an error.
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Contains(startsAt, await SlotsOnAsync(visitor, date));
    }

    [PostgresFact]
    public async Task A_booking_id_nobody_was_given_cancels_nothing()
    {
        // User B against user A's appointment: B never received A's id, and
        // the id is the only thing the route accepts. A guess is a 404 —
        // never a 403, which would confirm the guess named something — and
        // A's time stays booked.
        await OpenEveryDayAsync();
        using var alice = _app!.CreateClient();
        using var bob = _app.CreateClient();
        var (date, startsAt) = await FirstSlotAsync(alice);
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync("/api/scheduling/book", new
        {
            startsAt, name = "Alice", zoneId = "Etc/UTC"
        })).StatusCode);

        using var guessed = await bob.DeleteAsync($"/api/scheduling/book/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);
        Assert.DoesNotContain(startsAt, await SlotsOnAsync(bob, date));
    }

    [PostgresFact]
    public async Task A_booking_the_page_never_offered_is_refused()
    {
        await OpenEveryDayAsync();
        using var visitor = _app!.CreateClient();

        using var past = await visitor.PostAsJsonAsync("/api/scheduling/book", new
        {
            startsAt = "2020-01-01T09:00:00Z", name = "Private Snuffy", zoneId = "Etc/UTC"
        });
        using var nameless = await visitor.PostAsJsonAsync("/api/scheduling/book", new
        {
            startsAt = (await FirstSlotAsync(visitor)).StartsAt, name = "", zoneId = "Etc/UTC"
        });
        using var garbled = await visitor.PostAsJsonAsync("/api/scheduling/book", new
        {
            startsAt = "tomorrow-ish", name = "Private Snuffy", zoneId = "Etc/UTC"
        });

        Assert.Equal(HttpStatusCode.Conflict, past.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, garbled.StatusCode);
    }

    [PostgresFact]
    public async Task With_a_queue_open_the_state_is_the_line_and_names_nobody_in_it()
    {
        await OpenQueueAsync();
        using var visitor = _app!.CreateClient();
        await JoinAsync(visitor, "Private Snuffy");
        await JoinAsync(visitor, "Specialist Jones");

        using var response = await visitor.GetAsync("/api/scheduling/state");

        var raw = await response.Content.ReadAsStringAsync();
        var state = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("queue", state.GetProperty("mode").GetString());
        Assert.Equal(2, state.GetProperty("queue").GetProperty("waiting").GetInt32());
        Assert.True(state.GetProperty("queue").GetProperty("acceptingJoins").GetBoolean());
        Assert.DoesNotContain("Snuffy", raw);
        Assert.DoesNotContain("Jones", raw);
        Assert.DoesNotContain("displayName", raw);
    }

    [PostgresFact]
    public async Task A_place_in_the_line_is_read_by_its_id_and_by_nothing_else()
    {
        await OpenQueueAsync();
        using var alice = _app!.CreateClient();
        using var bob = _app.CreateClient();
        var alicesPlace = await JoinAsync(alice, "Alice");

        using var hers = await alice.GetAsync($"/api/scheduling/queue/{alicesPlace}");
        using var guessed = await bob.GetAsync($"/api/scheduling/queue/{Guid.NewGuid()}");
        using var left = await bob.DeleteAsync($"/api/scheduling/queue/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, hers.StatusCode);
        var place = await hers.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, place.GetProperty("position").GetInt32());
        Assert.Equal("Waiting", place.GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, left.StatusCode);

        // Still first in line after Bob's guesses.
        Assert.Equal("Waiting", (await alice.GetFromJsonAsync<JsonElement>($"/api/scheduling/queue/{alicesPlace}")).GetProperty("state").GetString());
    }

    // ------------------------------------------------------------ plumbing

    /// <summary>Opening hours all day, every day, so there is always a slot to book.</summary>
    private async Task OpenEveryDayAsync()
    {
        using var scope = _app!.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        for (var day = 1; day <= 7; day++)
        {
            database.AvailabilityRules.Add(new AvailabilityRuleRecord
            {
                Id = Guid.NewGuid(),
                Day = (IsoDayOfWeek)day,
                StartsAt = new LocalTime(0, 0),
                EndsAt = new LocalTime(23, 45),
                ZoneId = "Etc/UTC",
                Active = true
            });
        }

        await database.SaveChangesAsync();
    }

    private async Task OpenQueueAsync()
    {
        using var scope = _app!.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var now = SystemClock.Instance.GetCurrentInstant();
        database.QueueSessions.Add(new QueueSession
        {
            Id = Guid.NewGuid(),
            Name = "Office hours",
            OpensAt = now,
            ClosesAt = now + Duration.FromHours(8),
            DefaultDuration = Duration.FromMinutes(15),
            Open = true
        });
        await database.SaveChangesAsync();
    }

    private static async Task<Guid> JoinAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/scheduling/queue", new { name, zoneId = "Etc/UTC" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(string Date, string StartsAt)> FirstSlotAsync(HttpClient client)
    {
        var state = await client.GetFromJsonAsync<JsonElement>("/api/scheduling/state?zone=Etc/UTC");
        return (state.GetProperty("selectedDate").GetString()!, state.GetProperty("slots")[0].GetProperty("startsAt").GetString()!);
    }

    private static async Task<IReadOnlyList<string>> SlotsOnAsync(HttpClient client, string date)
    {
        var state = await client.GetFromJsonAsync<JsonElement>($"/api/scheduling/state?zone=Etc/UTC&date={date}");
        return [.. state.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("startsAt").GetString()!)];
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
