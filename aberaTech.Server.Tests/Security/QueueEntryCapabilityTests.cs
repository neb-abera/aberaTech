using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// A queue entry id is the capability to read and cancel that one place. A
/// phone number is not a secret — a soldier's number is known to their whole
/// platoon — so nothing a caller can do with a number alone may produce
/// somebody else's id, or say whether that number is in the line.
/// </summary>
public sealed class QueueEntryCapabilityTests : IDisposable
{
    private const string VictimPhone = "+12025550143";

    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public QueueEntryCapabilityTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("queue");
        _app = new TestApp(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Scheduling"] = _database.ConnectionString
        });
    }

    [PostgresFact]
    public async Task Joining_with_a_number_already_in_the_line_does_not_hand_over_that_place()
    {
        await OpenQueueAsync();
        using var victim = _app!.CreateClient();
        using var stranger = _app.CreateClient();

        var victimId = await JoinAsync(victim, "Private Snuffy", VictimPhone);
        var strangerId = await JoinAsync(stranger, "Somebody Else", VictimPhone);

        // The whole defect: this used to be the victim's id, which reads their
        // position and cancels their place.
        Assert.NotEqual(victimId, strangerId);

        var left = await stranger.DeleteAsync($"/api/scheduling/queue/{strangerId}");
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);

        var place = await victim.GetFromJsonAsync<JsonElement>($"/api/scheduling/queue/{victimId}");
        Assert.Equal("Waiting", place.GetProperty("state").GetString());
        Assert.Equal(1, place.GetProperty("position").GetInt32());
    }

    [PostgresFact]
    public async Task A_number_already_in_the_line_answers_exactly_like_one_that_is_not()
    {
        // Whether a given number is waiting to see the commander is itself
        // what must not leak, so the two answers have to be indistinguishable:
        // same status, same shape, and an id that behaves the same afterwards.
        await OpenQueueAsync();
        using var client = _app!.CreateClient();
        await JoinAsync(client, "Private Snuffy", VictimPhone);

        var duplicate = await PostJoinAsync(client, "Probe", VictimPhone);
        var fresh = await PostJoinAsync(client, "Probe", "+12025550188");

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(fresh.StatusCode, duplicate.StatusCode);

        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        var freshBody = await fresh.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            freshBody.EnumerateObject().Select(property => property.Name),
            duplicateBody.EnumerateObject().Select(property => property.Name));

        foreach (var id in new[] { duplicateBody, freshBody }.Select(body => body.GetProperty("id").GetGuid()))
        {
            var place = await client.GetAsync($"/api/scheduling/queue/{id}");
            Assert.Equal(HttpStatusCode.OK, place.StatusCode);
        }
    }

    [PostgresFact]
    public async Task A_second_join_with_the_same_number_earns_that_number_no_second_stream_of_texts()
    {
        // The reason the match on the number existed at all: without it, five
        // joins a minute with somebody else's number is five times the texts
        // to a person who asked for none of them, on the host's bill.
        await OpenQueueAsync();
        using var client = _app!.CreateClient();
        await JoinAsync(client, "Private Snuffy", VictimPhone);
        var secondId = await JoinAsync(client, "Probe", VictimPhone);

        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        var second = await database.QueueEntries.SingleAsync(entry => entry.Id == secondId);
        Assert.False(second.SmsConsent);
        Assert.Equal(string.Empty, second.PhoneE164);
        Assert.False(await database.Outbox.AnyAsync(message => message.QueueEntryId == secondId));
    }

    [PostgresFact]
    public async Task Entry_ids_are_random_not_sequential()
    {
        await OpenQueueAsync();
        using var client = _app!.CreateClient();

        var first = await JoinAsync(client, "One", null);
        var second = await JoinAsync(client, "Two", null);

        // Version 4: 122 random bits. A v1 or v7 id shares a timestamp prefix
        // with its neighbours and can be walked.
        Assert.All(new[] { first, second }, id => Assert.Equal(4, id.Version));
        Assert.NotEqual(first.ToString()[..8], second.ToString()[..8]);
    }

    [PostgresFact]
    public async Task Leaving_only_ever_withdraws_a_place_that_is_still_waiting()
    {
        // Somebody already seen is a record of what happened. The id outlives
        // the visit in the visitor's browser, and must not be able to rewrite
        // "done" into "cancelled" afterwards.
        var sessionId = await OpenQueueAsync();
        using var client = _app!.CreateClient();
        var id = await JoinAsync(client, "Private Snuffy", null);

        using (var scope = _app.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var entry = await database.QueueEntries.SingleAsync(candidate => candidate.Id == id);
            Assert.Equal(sessionId, entry.SessionId);
            entry.State = QueueEntryState.Done;
            await database.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/scheduling/queue/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _app.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var entry = await database.QueueEntries.SingleAsync(candidate => candidate.Id == id);
            Assert.Equal(QueueEntryState.Done, entry.State);
        }
    }

    private async Task<Guid> OpenQueueAsync()
    {
        using var scope = _app!.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var now = SystemClock.Instance.GetCurrentInstant();

        var session = new QueueSession
        {
            Id = Guid.NewGuid(),
            Name = "Test queue",
            OpensAt = now,
            ClosesAt = now + Duration.FromHours(8),
            DefaultDuration = Duration.FromMinutes(15),
            Open = true
        };

        database.QueueSessions.Add(session);
        await database.SaveChangesAsync();
        return session.Id;
    }

    private static Task<HttpResponseMessage> PostJoinAsync(HttpClient client, string name, string? phone) =>
        client.PostAsJsonAsync("/api/scheduling/queue", new
        {
            name,
            phone,
            zoneId = "America/New_York",
            smsConsent = phone is not null
        });

    private static async Task<Guid> JoinAsync(HttpClient client, string name, string? phone)
    {
        var response = await PostJoinAsync(client, name, phone);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
