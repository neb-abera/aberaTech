using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Admin;

/// <summary>
/// The host's routes, from the entry point inward. Three callers meet each
/// one: a visitor with no session, who is told to sign in; a Google account
/// the allowlist does not name, who is refused; and the host, who gets the
/// route's real answer. The first two need no database — the refusal comes
/// before the handler — so they run in the hermetic stage; the host's own
/// answers need the compose Postgres and live in <see cref="AdminRouteDatabaseTests"/>.
/// </summary>
public sealed class AdminRouteTests : IDisposable
{
    internal const string Owner = "owner@example.test";
    internal const string Stranger = "stranger@example.test";

    private readonly TestApp _app = new(
        ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable),
        services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));

    /// <summary>Every route behind the admin policy, with an id where one is taken.</summary>
    public static IEnumerable<object[]> Routes =>
    [
        ["POST", "/api/scheduling/admin/sign-out"],
        ["GET", "/api/scheduling/admin/queue"],
        ["GET", "/api/scheduling/admin/messages"],
        ["POST", "/api/scheduling/admin/session"],
        ["POST", "/api/scheduling/admin/session/close"],
        ["POST", "/api/scheduling/admin/queue/0f1d2c3b-4a59-4687-9a0b-1c2d3e4f5a6b/start"],
        ["POST", "/api/scheduling/admin/queue/0f1d2c3b-4a59-4687-9a0b-1c2d3e4f5a6b/done"],
        ["POST", "/api/scheduling/admin/queue/0f1d2c3b-4a59-4687-9a0b-1c2d3e4f5a6b/no-show"],
        ["POST", "/api/scheduling/admin/queue/0f1d2c3b-4a59-4687-9a0b-1c2d3e4f5a6b/duration"],
        ["GET", "/api/scheduling/admin/availability"],
        ["PUT", "/api/scheduling/admin/availability"],
        ["GET", "/api/scheduling/admin/calendar"],
        ["POST", "/api/scheduling/admin/calendar/disconnect"],
        ["GET", "/api/scheduling/admin/calendar/connect"]
    ];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_visitor_with_no_session_is_told_to_sign_in(string method, string path)
    {
        using var visitor = _app.CreateClient();

        using var response = await Send(visitor, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_google_account_that_is_not_the_host_is_refused(string method, string path)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, Stranger);

        using var response = await Send(stranger, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Who_am_i_answers_everybody_and_names_only_the_host()
    {
        using var visitor = _app.CreateClient();
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, Stranger);
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, Owner);

        var nobody = await visitor.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/me");
        var somebody = await stranger.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/me");
        var host = await owner.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/me");

        Assert.True(nobody.GetProperty("configured").GetBoolean());
        Assert.False(nobody.GetProperty("signedIn").GetBoolean());
        Assert.False(somebody.GetProperty("signedIn").GetBoolean());
        Assert.Equal(JsonValueKind.Null, somebody.GetProperty("email").ValueKind);
        Assert.True(host.GetProperty("signedIn").GetBoolean());
        Assert.Equal(Owner, host.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Signing_out_ends_the_session_cookie()
    {
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, Owner);

        using var response = await owner.PostAsync("/api/scheduling/admin/sign-out", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-abera.admin=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Connecting_a_calendar_sends_the_host_to_google_for_the_wider_grant()
    {
        using var owner = _app.CreateClient().SignedInAs(_app.Factory.Services, Owner);

        using var response = await owner.GetAsync("/api/scheduling/admin/calendar/connect");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);
        Assert.Contains("access_type=offline", location);
        Assert.Contains("prompt=consent", location);
        Assert.Contains(Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.readonly"), location);
    }

    internal static Task<HttpResponseMessage> Send(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Parse(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        return client.SendAsync(request);
    }

    public void Dispose() => _app.Dispose();
}

/// <summary>The host's own answers, against the compose Postgres.</summary>
public sealed class AdminRouteDatabaseTests : IDisposable
{
    private readonly TestDatabase? _database;
    private readonly TestApp? _app;

    public AdminRouteDatabaseTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("admin");
        _app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = _database.ConnectionString,
                ["ClientAddress:ForwardedHops"] = "0",
                ["Admin:GoogleClientId"] = "test-client",
                ["Admin:GoogleClientSecret"] = "test-secret",
                ["Admin:AllowedEmails:0"] = AdminRouteTests.Owner
            },
            services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
    }

    [PostgresFact]
    public async Task The_host_opens_a_queue_runs_the_line_and_closes_it()
    {
        using var host = Host();
        using var visitor = _app!.CreateClient();

        using var opened = await host.PostAsJsonAsync("/api/scheduling/admin/session", new { name = "Office hours", defaultMinutes = 15, hoursOpen = 4 });
        using var twice = await host.PostAsJsonAsync("/api/scheduling/admin/session", new { name = "Another" });
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);

        var snuffy = await JoinAsync(visitor, "Private Snuffy");
        var jones = await JoinAsync(visitor, "Specialist Jones");

        // The host's projection is the one that carries names, and only the host reads it.
        var queue = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/queue");
        Assert.True(queue.GetProperty("open").GetBoolean());
        Assert.Equal("Office hours", queue.GetProperty("name").GetString());
        Assert.Equal("Private Snuffy", Entry(queue, snuffy).GetProperty("displayName").GetString());
        Assert.Equal("Waiting", Entry(queue, snuffy).GetProperty("state").GetString());
        Assert.Equal(15, Entry(queue, snuffy).GetProperty("expectedMinutes").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsJsonAsync($"/api/scheduling/admin/queue/{snuffy}/duration", new { minutes = 20 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.PostAsJsonAsync($"/api/scheduling/admin/queue/{snuffy}/duration", new { minutes = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsJsonAsync($"/api/scheduling/admin/queue/{Guid.NewGuid()}/duration", new { minutes = 20 })).StatusCode);
        Assert.Equal(20, Entry(await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/queue"), snuffy).GetProperty("expectedMinutes").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync($"/api/scheduling/admin/queue/{snuffy}/start", null)).StatusCode);
        Assert.Equal("Serving", await StateOfAsync(host, snuffy));
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync($"/api/scheduling/admin/queue/{snuffy}/done", null)).StatusCode);
        Assert.Equal("Done", await StateOfAsync(host, snuffy));
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync($"/api/scheduling/admin/queue/{jones}/no-show", null)).StatusCode);
        Assert.Equal("NoShow", await StateOfAsync(host, jones));
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync($"/api/scheduling/admin/queue/{Guid.NewGuid()}/start", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/scheduling/admin/session/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PostAsync("/api/scheduling/admin/session/close", null)).StatusCode);
        Assert.False((await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/queue")).GetProperty("open").GetBoolean());
    }

    [PostgresFact]
    public async Task A_google_account_that_is_not_the_host_moves_nobody_in_the_line()
    {
        using var host = Host();
        using var visitor = _app!.CreateClient();
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsJsonAsync("/api/scheduling/admin/session", new { name = "Office hours" })).StatusCode);
        var snuffy = await JoinAsync(visitor, "Private Snuffy");

        foreach (var action in new[] { "start", "done", "no-show" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync($"/api/scheduling/admin/queue/{snuffy}/{action}", null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsJsonAsync($"/api/scheduling/admin/queue/{snuffy}/duration", new { minutes = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync("/api/scheduling/admin/session/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/scheduling/admin/queue")).StatusCode);

        var queue = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/queue");
        Assert.True(queue.GetProperty("open").GetBoolean());
        Assert.Equal("Waiting", Entry(queue, snuffy).GetProperty("state").GetString());
        Assert.Equal(15, Entry(queue, snuffy).GetProperty("expectedMinutes").GetInt32());
    }

    [PostgresFact]
    public async Task The_messages_view_shows_what_is_queued_and_what_went()
    {
        using var host = Host();

        var messages = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/messages");

        Assert.Equal(JsonValueKind.Array, messages.GetProperty("upcoming").ValueKind);
        Assert.Equal(JsonValueKind.Array, messages.GetProperty("recent").ValueKind);
    }

    [PostgresFact]
    public async Task The_week_is_read_replaced_whole_and_refused_when_a_day_is_wrong()
    {
        using var host = Host();

        var before = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/availability");
        Assert.Equal(7, before.GetProperty("days").GetArrayLength());

        var week = new
        {
            zoneId = "America/New_York",
            days = Enumerable.Range(1, 7).Select(day => new { day, startsAt = "09:00", endsAt = "17:00", active = day <= 5 }).ToArray()
        };
        using var replaced = await host.PutAsJsonAsync("/api/scheduling/admin/availability", week);
        using var eighthDay = await host.PutAsJsonAsync("/api/scheduling/admin/availability", new
        {
            zoneId = "America/New_York", days = new[] { new { day = 8, startsAt = "09:00", endsAt = "17:00", active = true } }
        });
        using var backwards = await host.PutAsJsonAsync("/api/scheduling/admin/availability", new
        {
            zoneId = "America/New_York", days = new[] { new { day = 1, startsAt = "17:00", endsAt = "09:00", active = true } }
        });

        Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, eighthDay.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);

        var after = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/availability");
        Assert.Equal("America/New_York", after.GetProperty("zoneId").GetString());
        Assert.Equal("09:00", after.GetProperty("days")[0].GetProperty("startsAt").GetString());
        Assert.False(after.GetProperty("days")[6].GetProperty("active").GetBoolean());
    }

    [PostgresFact]
    public async Task The_calendar_reports_no_connection_and_disconnecting_is_harmless()
    {
        using var host = Host();

        var status = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/calendar");
        Assert.False(status.GetProperty("connected").GetBoolean());
        Assert.False(status.GetProperty("invitesEnabled").GetBoolean());

        using var disconnected = await host.PostAsync("/api/scheduling/admin/calendar/disconnect", null);
        Assert.Equal(HttpStatusCode.NoContent, disconnected.StatusCode);
        Assert.False((await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/calendar")).GetProperty("connected").GetBoolean());
    }

    private HttpClient Host() => _app!.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private static async Task<Guid> JoinAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/scheduling/queue", new { name, zoneId = "Etc/UTC" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static JsonElement Entry(JsonElement queue, Guid id) =>
        queue.GetProperty("entries").EnumerateArray().Single(entry => entry.GetProperty("id").GetGuid() == id);

    private static async Task<string> StateOfAsync(HttpClient host, Guid id) =>
        Entry(await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/queue"), id).GetProperty("state").GetString()!;

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }
}
