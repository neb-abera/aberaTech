using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using aberaTech.Scheduling.Outbox;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Scheduling;

/// <summary>
/// Booking links and calendar invites, from the routes inward: the host makes
/// a link, a visitor holding it books under its rules, and every booking's
/// invite goes through the outbox to Google.
/// </summary>
public sealed class BookingLinkRouteTests : IDisposable
{
    private const string HostPhone = "+12024561111";

    private readonly TestDatabase? _database;
    private readonly TestApp? _app;
    private readonly RecordingInvites _invites = new();

    public BookingLinkRouteTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("links");
        _app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = _database.ConnectionString,
                ["ClientAddress:ForwardedHops"] = "0",
                ["Admin:GoogleClientId"] = "test-client",
                ["Admin:GoogleClientSecret"] = "test-secret",
                ["Admin:AllowedEmails:0"] = AdminRouteTests.Owner,
                ["Scheduling:HostPhoneE164"] = HostPhone
            },
            services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddScoped<ICalendarInvites>(_ => _invites);
            });
    }

    private static LocalDate Today => SystemClock.Instance.GetCurrentInstant().InUtc().Date;

    [PostgresFact]
    public async Task A_link_with_an_earliest_date_offers_nothing_before_it_and_refuses_a_booking_before_it()
    {
        await OpenEveryDayAsync();
        using var host = Host();
        var earliest = Today.PlusDays(3);
        var (linkId, key) = await CreateLinkAsync(host, "LinkedIn interviews", earliest, minutes: 30, showsQueue: false);

        using var interviewer = WithKey(key);
        using var soldier = _app!.CreateClient();

        var linked = await StateAsync(interviewer);
        var plain = await StateAsync(soldier);

        Assert.All(Dates(linked), date => Assert.True(string.CompareOrdinal(date, Iso(earliest)) >= 0, date));
        Assert.Equal(Iso(earliest), Dates(linked)[0]);
        Assert.Equal(30, linked.GetProperty("slots")[0].GetProperty("minutes").GetInt32());
        Assert.True(string.CompareOrdinal(Dates(plain)[0], Iso(earliest)) < 0);
        Assert.Equal(15, plain.GetProperty("slots")[0].GetProperty("minutes").GetInt32());

        // A time the plain page offers, sent with the link's key: the server
        // applies the link's rules, whatever the page showed.
        var early = plain.GetProperty("slots")[0].GetProperty("startsAt").GetString();
        using var refused = await interviewer.PostAsJsonAsync("/api/scheduling/book", Booking(early));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var allowed = linked.GetProperty("slots")[0].GetProperty("startsAt").GetString();
        using var booked = await interviewer.PostAsJsonAsync("/api/scheduling/book", Booking(allowed));
        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var confirmation = await booked.Content.ReadFromJsonAsync<JsonElement>();
        var id = confirmation.GetProperty("id").GetGuid();
        Assert.Equal(
            Instant.FromDateTimeOffset(DateTimeOffset.Parse(allowed!)) + Duration.FromMinutes(30),
            Instant.FromDateTimeOffset(DateTimeOffset.Parse(confirmation.GetProperty("endsAt").GetString()!)));

        await using var scope = Scope(out var database);
        var appointment = await database.Appointments.SingleAsync(candidate => candidate.Id == id);
        Assert.Equal(linkId, appointment.BookingLinkId);
        Assert.Equal("ada@example.com", appointment.Email);
    }

    [PostgresFact]
    public async Task An_unknown_key_and_a_link_that_is_off_get_the_default_rules()
    {
        await OpenEveryDayAsync();
        using var host = Host();
        var earliest = Today.PlusDays(3);
        var (linkId, key) = await CreateLinkAsync(host, "LinkedIn interviews", earliest);

        using var guesser = WithKey("not-a-real-key-at-all");
        using var garbage = WithKey("<script>");
        Assert.True(string.CompareOrdinal(Dates(await StateAsync(guesser))[0], Iso(earliest)) < 0);
        Assert.True(string.CompareOrdinal(Dates(await StateAsync(garbage))[0], Iso(earliest)) < 0);

        using var off = await host.PutAsJsonAsync($"/api/scheduling/admin/links/{linkId}", Rules("LinkedIn interviews", earliest, active: false));
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);

        using var holder = WithKey(key);
        Assert.True(string.CompareOrdinal(Dates(await StateAsync(holder))[0], Iso(earliest)) < 0);
    }

    [PostgresFact]
    public async Task The_default_rules_are_saved_on_the_page_and_apply_to_plain_schedule()
    {
        await OpenEveryDayAsync();
        using var host = Host();
        var earliest = Today.PlusDays(4);

        using var saved = await host.PutAsJsonAsync("/api/scheduling/admin/links/default", Rules("ignored", earliest));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        using var visitor = _app!.CreateClient();
        Assert.Equal(Iso(earliest), Dates(await StateAsync(visitor))[0]);

        var list = await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/links");
        Assert.Equal(Iso(earliest), list.GetProperty("default").GetProperty("earliestDate").GetString());
        Assert.True(list.GetProperty("default").GetProperty("isDefault").GetBoolean());
    }

    [PostgresFact]
    public async Task The_owner_sees_every_link_and_never_a_hash_and_a_new_key_stops_the_old_one()
    {
        await OpenEveryDayAsync();
        using var host = Host();
        var earliest = Today.PlusDays(3);
        var (linkId, key) = await CreateLinkAsync(host, "Army", earliest);

        var listed = await host.GetStringAsync("/api/scheduling/admin/links");
        var row = Row(JsonDocument.Parse(listed).RootElement, linkId);
        Assert.Equal(key, row.GetProperty("key").GetString());
        Assert.NotNull(row.GetProperty("issuedAt").GetString());
        Assert.DoesNotContain(BookingLinkKeys.Hash(key), listed);

        // Stored encrypted: the column holds neither the key nor its hash.
        await using (var scope = Scope(out var database))
        {
            var stored = await database.BookingLinks.SingleAsync(link => link.Id == linkId);
            Assert.NotNull(stored.ProtectedKey);
            Assert.DoesNotContain(key, stored.ProtectedKey);
        }

        using var rotated = await host.PostAsync($"/api/scheduling/admin/links/{linkId}/key", null);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var newKey = (await rotated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
        Assert.NotEqual(key, newKey);
        Assert.Equal(newKey, Row(await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/links"), linkId).GetProperty("key").GetString());

        using var oldHolder = WithKey(key);
        using var newHolder = WithKey(newKey);
        Assert.True(string.CompareOrdinal(Dates(await StateAsync(oldHolder))[0], Iso(earliest)) < 0);
        Assert.Equal(Iso(earliest), Dates(await StateAsync(newHolder))[0]);
    }

    [PostgresFact]
    public async Task A_revoked_link_stops_for_good_until_a_new_key_is_issued()
    {
        await OpenEveryDayAsync();
        using var host = Host();
        var earliest = Today.PlusDays(3);
        var (linkId, key) = await CreateLinkAsync(host, "Recruiter", earliest);

        using var revoked = await host.PostAsync($"/api/scheduling/admin/links/{linkId}/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        var row = Row(await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/links"), linkId);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("key").ValueKind);
        Assert.False(row.GetProperty("active").GetBoolean());
        Assert.NotNull(row.GetProperty("revokedAt").GetString());

        using var holder = WithKey(key);
        Assert.True(string.CompareOrdinal(Dates(await StateAsync(holder))[0], Iso(earliest)) < 0);

        // On cannot bring back an address that no longer exists.
        using var on = await host.PutAsJsonAsync($"/api/scheduling/admin/links/{linkId}", Rules("Recruiter", earliest));
        Assert.Equal(HttpStatusCode.Conflict, on.StatusCode);

        await using (var scope = Scope(out var database))
        {
            var stored = await database.BookingLinks.SingleAsync(link => link.Id == linkId);
            Assert.Null(stored.KeyHash);
            Assert.Null(stored.ProtectedKey);
        }

        using var reissued = await host.PostAsync($"/api/scheduling/admin/links/{linkId}/key", null);
        var newKey = (await reissued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
        using var newHolder = WithKey(newKey);
        Assert.Equal(Iso(earliest), Dates(await StateAsync(newHolder))[0]);
        Assert.True(Row(await host.GetFromJsonAsync<JsonElement>("/api/scheduling/admin/links"), linkId).GetProperty("active").GetBoolean());

        using var missing = await host.PostAsync($"/api/scheduling/admin/links/{Guid.NewGuid()}/revoke", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static JsonElement Row(JsonElement list, Guid id) =>
        list.GetProperty("links").EnumerateArray().Single(link => link.GetProperty("id").GetGuid() == id);

    [PostgresFact]
    public async Task The_host_cannot_save_rules_that_make_no_sense()
    {
        using var host = Host();

        using var noName = await host.PostAsJsonAsync("/api/scheduling/admin/links", Rules("", null));
        using var badDate = await host.PostAsJsonAsync("/api/scheduling/admin/links", Rules("Army", null) with { EarliestDate = "16 Nov" });
        using var tooShort = await host.PostAsJsonAsync("/api/scheduling/admin/links", Rules("Army", null) with { AppointmentMinutes = 1 });
        using var noDays = await host.PostAsJsonAsync("/api/scheduling/admin/links", Rules("Army", null) with { HorizonDays = 0 });
        using var missing = await host.PutAsJsonAsync($"/api/scheduling/admin/links/{Guid.NewGuid()}", Rules("Army", null));

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDays.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [PostgresFact]
    public async Task A_link_that_does_not_see_the_queue_keeps_its_slots_while_one_runs()
    {
        await OpenEveryDayAsync();
        await OpenQueueAsync();
        using var host = Host();
        var (_, key) = await CreateLinkAsync(host, "LinkedIn interviews", null, showsQueue: false);

        using var interviewer = WithKey(key);
        using var soldier = _app!.CreateClient();

        Assert.Equal("slots", (await StateAsync(interviewer)).GetProperty("mode").GetString());
        Assert.Equal("queue", (await StateAsync(soldier)).GetProperty("mode").GetString());
    }

    [PostgresFact]
    public async Task Every_booking_needs_an_email_for_its_invite()
    {
        await OpenEveryDayAsync();
        using var visitor = _app!.CreateClient();
        var startsAt = (await StateAsync(visitor)).GetProperty("slots")[0].GetProperty("startsAt").GetString();

        using var blank = await visitor.PostAsJsonAsync("/api/scheduling/book", new { startsAt, name = "Ada", zoneId = "Etc/UTC" });
        using var wrong = await visitor.PostAsJsonAsync("/api/scheduling/book", new { startsAt, name = "Ada", zoneId = "Etc/UTC", email = "ada at example" });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Contains("email", await blank.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        await using var scope = Scope(out var database);
        Assert.False(await database.Appointments.AnyAsync());
    }

    [PostgresFact]
    public async Task A_booking_sends_its_invite_through_the_outbox_and_a_cancel_withdraws_it()
    {
        await OpenEveryDayAsync();
        using var visitor = _app!.CreateClient();
        var startsAt = (await StateAsync(visitor)).GetProperty("slots")[0].GetProperty("startsAt").GetString();

        using var booked = await visitor.PostAsJsonAsync("/api/scheduling/book", Booking(startsAt));
        var id = (await booked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var invite = await WaitForAsync(id, NotificationKind.CalendarInvite, DeliveryState.Delivered);
        Assert.Equal(id.ToString("N"), invite.ProviderMessageId);
        Assert.Contains(id, _invites.Invited);

        await using (var scope = Scope(out var database))
        {
            Assert.Equal(id.ToString("N"), (await database.Appointments.SingleAsync(a => a.Id == id)).GoogleEventId);
        }

        using var canceled = await visitor.DeleteAsync($"/api/scheduling/book/{id}");
        Assert.Equal(HttpStatusCode.NoContent, canceled.StatusCode);

        var withdrawal = await WaitForAsync(id, NotificationKind.CalendarWithdraw, DeliveryState.Delivered, seconds: 60);
        Assert.Equal(GoogleCalendarInvites.WithdrawalId(id.ToString("N")), withdrawal.ProviderMessageId);
        Assert.Contains(id, _invites.Withdrawn);
    }

    [PostgresFact]
    public async Task An_invite_that_cannot_go_tells_the_host()
    {
        await OpenEveryDayAsync();
        _invites.Refuse = true;
        using var visitor = _app!.CreateClient();
        var startsAt = (await StateAsync(visitor)).GetProperty("slots")[0].GetProperty("startsAt").GetString();

        using var booked = await visitor.PostAsJsonAsync("/api/scheduling/book", Booking(startsAt));
        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var id = (await booked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var invite = await WaitForAsync(id, NotificationKind.CalendarInvite, DeliveryState.DeadLettered);
        Assert.Equal(RecordingInvites.Refusal, invite.LastError);

        await using var scope = Scope(out var database);
        var alert = await database.Outbox.SingleAsync(m => m.AppointmentId == id && m.Kind == NotificationKind.HostInviteFailed);
        Assert.Equal(HostPhone, alert.ToPhoneE164);
        Assert.Contains("Calendar invite failed", alert.Body);
    }

    // ------------------------------------------------------------ plumbing

    private HttpClient Host() => _app!.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private HttpClient WithKey(string key)
    {
        var client = _app!.CreateClient();
        client.DefaultRequestHeaders.Add(BookingLinkResolver.Header, key);
        return client;
    }

    private AsyncServiceScope Scope(out SchedulingDbContext database)
    {
        var scope = _app!.Factory.Services.CreateAsyncScope();
        database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        return scope;
    }

    private static string Iso(LocalDate date) => date.ToString("uuuu-MM-dd", null);

    private static IReadOnlyList<string> Dates(JsonElement state) =>
        [.. state.GetProperty("availableDates").EnumerateArray().Select(date => date.GetString()!)];

    private static Task<JsonElement> StateAsync(HttpClient client) =>
        client.GetFromJsonAsync<JsonElement>("/api/scheduling/state?zone=Etc/UTC");

    private static object Booking(string? startsAt) =>
        new { startsAt, name = "Ada", zoneId = "Etc/UTC", email = "ada@example.com" };

    private sealed record LinkRules(
        string Name,
        string? EarliestDate,
        int HorizonDays,
        int LeadMinutes,
        int AppointmentMinutes,
        bool ShowsQueue,
        bool Active);

    private static LinkRules Rules(string name, LocalDate? earliest, int minutes = 15, bool showsQueue = true, bool active = true) =>
        new(name, earliest is { } date ? Iso(date) : null, 21, 30, minutes, showsQueue, active);

    private static async Task<(Guid Id, string Key)> CreateLinkAsync(
        HttpClient host, string name, LocalDate? earliest, int minutes = 15, bool showsQueue = true)
    {
        using var created = await host.PostAsJsonAsync("/api/scheduling/admin/links", Rules(name, earliest, minutes, showsQueue));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var key = body.GetProperty("key").GetString()!;
        Assert.Equal(22, key.Length);
        return (body.GetProperty("link").GetProperty("id").GetGuid(), key);
    }

    /// <summary>Waits for the running dispatcher to bring a job to a state.</summary>
    private async Task<OutboxMessage> WaitForAsync(Guid appointmentId, NotificationKind kind, DeliveryState state, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            await using (var scope = Scope(out var database))
            {
                var message = await database.Outbox.AsNoTracking()
                    .SingleOrDefaultAsync(m => m.AppointmentId == appointmentId && m.Kind == kind);
                if (message?.State == state) return message;
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"{kind} for {appointmentId} is {message?.State.ToString() ?? "missing"}, not {state}.");
                }
            }

            await Task.Delay(500);
        }
    }

    private async Task OpenEveryDayAsync()
    {
        await using var scope = Scope(out var database);
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
        await using var scope = Scope(out var database);
        var now = SystemClock.Instance.GetCurrentInstant();
        database.QueueSessions.Add(new QueueSession
        {
            Id = Guid.NewGuid(),
            Name = "Counseling",
            OpensAt = now,
            ClosesAt = now + Duration.FromHours(8),
            DefaultDuration = Duration.FromMinutes(15),
            Open = true
        });
        await database.SaveChangesAsync();
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }

    /// <summary>Google, as far as the outbox can tell: records what it was asked and answers as told.</summary>
    private sealed class RecordingInvites : ICalendarInvites
    {
        public const string Refusal = "Google refused the event as malformed (400).";

        public bool Refuse { get; set; }

        public List<Guid> Invited { get; } = [];

        public List<Guid> Withdrawn { get; } = [];

        public Task<SendResult> InviteAsync(Appointment appointment, CancellationToken cancellationToken)
        {
            lock (Invited) Invited.Add(appointment.Id);
            return Task.FromResult(Refuse
                ? SendResult.RejectedPermanently(Refusal)
                : SendResult.Confirmed(GoogleCalendarInvites.EventId(appointment)));
        }

        public Task<SendResult> WithdrawAsync(Appointment appointment, CancellationToken cancellationToken)
        {
            lock (Withdrawn) Withdrawn.Add(appointment.Id);
            return Task.FromResult(SendResult.Confirmed(GoogleCalendarInvites.WithdrawalId(GoogleCalendarInvites.EventId(appointment))));
        }
    }
}
