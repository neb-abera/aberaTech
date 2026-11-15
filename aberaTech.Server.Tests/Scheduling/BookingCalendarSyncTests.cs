using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using aberaTech.Scheduling.Outbox;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Scheduling;

/// <summary>
/// A booking follows its Google Calendar event. A visitor books through the
/// route, the running outbox sends the invite, and then the event changes in
/// the calendar the way a visitor or the host changes it: declined, deleted,
/// moved. The running sync has to bring the booking, the free times on the
/// page and the reminder texts along.
/// </summary>
public sealed class BookingCalendarSyncTests : IDisposable
{
    private const string HostPhone = "+12024561111";
    private const string GuestPhone = "+12025550143";

    private readonly TestDatabase? _database;
    private readonly TestApp? _app;
    private readonly FakeGoogle _google = new();

    public BookingCalendarSyncTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("calsync");
        _app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = _database.ConnectionString,
                ["ClientAddress:ForwardedHops"] = "0",
                ["Scheduling:HostPhoneE164"] = HostPhone,
                ["Scheduling:CalendarSyncSeconds"] = "1"
            },
            services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddScoped<ICalendarInvites>(_ => _google);
            });
    }

    [PostgresFact]
    public async Task A_visitor_who_declines_the_invite_cancels_the_booking()
    {
        var (visitor, date, slots) = await OpenAsync();
        using var _ = visitor;
        var id = await BookAsync(visitor, slots[0]);

        _google.Decline(id);

        var appointment = await WaitForAsync(id, booking => booking.Cancelled);
        Assert.Equal(Parse(slots[0]), appointment.StartsAt);

        await using var scope = Scope(out var database);
        var messages = await database.Outbox.AsNoTracking().Where(m => m.AppointmentId == id).ToListAsync();

        // The reminders for a meeting that is off are gone, not waiting.
        Assert.DoesNotContain(messages, m => m.Kind is NotificationKind.Reminder or NotificationKind.ReminderDayBefore);
        Assert.Equal(GuestPhone, Assert.Single(messages, m => m.Kind == NotificationKind.Cancelled).ToPhoneE164);
        Assert.Equal(HostPhone, Assert.Single(messages, m => m.Kind == NotificationKind.HostCancelled).ToPhoneE164);

        // The event comes off the host's calendar.
        Assert.Single(messages, m => m.Kind == NotificationKind.CalendarWithdraw);

        // And the time is back on the page.
        Assert.Contains(slots[0], await SlotsAsync(visitor, date));
    }

    [PostgresFact]
    public async Task A_host_who_deletes_the_event_cancels_the_booking()
    {
        var (visitor, date, slots) = await OpenAsync();
        using var _ = visitor;
        var id = await BookAsync(visitor, slots[0]);

        _google.Delete(id);

        await WaitForAsync(id, booking => booking.Cancelled);

        await using var scope = Scope(out var database);
        var messages = await database.Outbox.AsNoTracking().Where(m => m.AppointmentId == id).ToListAsync();

        Assert.DoesNotContain(messages, m => m.Kind is NotificationKind.Reminder or NotificationKind.ReminderDayBefore);
        Assert.Single(messages, m => m.Kind == NotificationKind.Cancelled);
        Assert.Single(messages, m => m.Kind == NotificationKind.HostCancelled);

        // The event is gone already, so nothing is sent to Google to delete it.
        Assert.DoesNotContain(messages, m => m.Kind == NotificationKind.CalendarWithdraw);
        Assert.Contains(slots[0], await SlotsAsync(visitor, date));
    }

    [PostgresFact]
    public async Task A_host_who_moves_the_event_moves_the_booking_and_its_reminders()
    {
        var (visitor, date, slots) = await OpenAsync();
        using var _ = visitor;
        var id = await BookAsync(visitor, slots[0]);

        // An hour later the same day, and twice as long.
        var movedTo = Parse(slots[4]);
        var movedEnd = movedTo + Duration.FromMinutes(30);
        _google.Move(id, movedTo, movedEnd);

        var appointment = await WaitForAsync(id, booking => booking.StartsAt == movedTo);
        Assert.Equal(movedEnd, appointment.EndsAt);
        Assert.False(appointment.Cancelled);

        await using (var scope = Scope(out var database))
        {
            var messages = await database.Outbox.AsNoTracking().Where(m => m.AppointmentId == id).ToListAsync();

            // One of each reminder, due from the new time and saying it.
            var early = Assert.Single(messages, m => m.Kind == NotificationKind.ReminderDayBefore);
            var final = Assert.Single(messages, m => m.Kind == NotificationKind.Reminder);
            Assert.Equal(movedTo - Duration.FromHours(24), early.NextAttemptAt);
            Assert.Equal(movedTo - Duration.FromMinutes(60), final.NextAttemptAt);
            Assert.Equal(
                MessageComposer.Compose(NotificationKind.Reminder, "Neb Abera", movedTo, DateTimeZone.Utc, "aberaTech"),
                final.Body);

            var moved = Assert.Single(messages, m => m.Kind == NotificationKind.Moved);
            Assert.Equal(GuestPhone, moved.ToPhoneE164);
            Assert.Equal(
                MessageComposer.Compose(NotificationKind.Moved, "Neb Abera", movedTo, DateTimeZone.Utc, "aberaTech"),
                moved.Body);
            Assert.Equal(HostPhone, Assert.Single(messages, m => m.Kind == NotificationKind.HostMoved).ToPhoneE164);

            Assert.DoesNotContain(messages, m => m.Kind is NotificationKind.Cancelled or NotificationKind.CalendarWithdraw);
        }

        // The old time is free again. The new one, and the slot the longer
        // meeting now runs into, are taken.
        var offered = await SlotsAsync(visitor, date);
        Assert.Contains(slots[0], offered);
        Assert.DoesNotContain(slots[4], offered);
        Assert.DoesNotContain(slots[5], offered);
    }

    [PostgresFact]
    public async Task A_booking_moved_away_and_back_follows_both_moves()
    {
        var (visitor, date, slots) = await OpenAsync();
        using var _ = visitor;
        var id = await BookAsync(visitor, slots[0]);
        var first = Parse(slots[0]);
        var second = Parse(slots[4]);
        var length = Duration.FromMinutes(15);

        _google.Move(id, second, second + length);
        await WaitForAsync(id, booking => booking.StartsAt == second);

        _google.Move(id, first, first + length);
        await WaitForAsync(id, booking => booking.StartsAt == first);

        await using var scope = Scope(out var database);
        var messages = await database.Outbox.AsNoTracking().Where(m => m.AppointmentId == id).ToListAsync();

        // Two moves, two texts each way, and still one of each reminder.
        Assert.Equal(2, messages.Count(m => m.Kind == NotificationKind.Moved));
        Assert.Equal(2, messages.Count(m => m.Kind == NotificationKind.HostMoved));
        Assert.Equal(first - Duration.FromMinutes(60), Assert.Single(messages, m => m.Kind == NotificationKind.Reminder).NextAttemptAt);
        Assert.Single(messages, m => m.Kind == NotificationKind.ReminderDayBefore);
        Assert.DoesNotContain(slots[0], await SlotsAsync(visitor, date));
    }

    [PostgresFact]
    public async Task A_calendar_that_gives_no_answer_changes_nothing()
    {
        var (visitor, date, slots) = await OpenAsync();
        using var _ = visitor;
        var id = await BookAsync(visitor, slots[0]);

        // What a reconnected, different calendar or an outage looks like.
        _google.Forget(id);
        var reads = _google.Reads;
        await WaitUntilAsync(() => _google.Reads >= reads + 3, "three more reads of the event");

        await using var scope = Scope(out var database);
        var appointment = await database.Appointments.AsNoTracking().SingleAsync(booking => booking.Id == id);
        Assert.False(appointment.Cancelled);
        Assert.Equal(Parse(slots[0]), appointment.StartsAt);
        Assert.Equal(2, await database.Outbox.CountAsync(m =>
            m.AppointmentId == id
            && (m.Kind == NotificationKind.Reminder || m.Kind == NotificationKind.ReminderDayBefore)
            && m.State == DeliveryState.Pending));
        Assert.DoesNotContain(slots[0], await SlotsAsync(visitor, date));
    }

    [PostgresFact]
    public async Task A_move_onto_another_booking_is_refused_and_the_rest_still_sync()
    {
        var (visitor, date, slots) = await OpenAsync();
        using var _ = visitor;
        var blocked = await BookAsync(visitor, slots[0]);
        var other = await BookAsync(visitor, slots[4]);
        var third = await BookAsync(visitor, slots[8]);

        // The first is dragged onto the second's time. The third is declined.
        _google.Move(blocked, Parse(slots[4]), Parse(slots[4]) + Duration.FromMinutes(15));
        _google.Decline(third);

        await WaitForAsync(third, booking => booking.Cancelled);

        await using var scope = Scope(out var database);
        var kept = await database.Appointments.AsNoTracking().SingleAsync(booking => booking.Id == blocked);
        Assert.Equal(Parse(slots[0]), kept.StartsAt);
        Assert.False(kept.Cancelled);
        Assert.False((await database.Appointments.AsNoTracking().SingleAsync(booking => booking.Id == other)).Cancelled);
        Assert.False(await database.Outbox.AnyAsync(m => m.AppointmentId == blocked && m.Kind == NotificationKind.Moved));
    }

    // ------------------------------------------------------------ plumbing

    private static Instant Parse(string startsAt) => Instant.FromDateTimeOffset(DateTimeOffset.Parse(startsAt));

    /// <summary>Opens every day, and picks a day far enough out that both reminders are planned.</summary>
    private async Task<(HttpClient Visitor, string Date, IReadOnlyList<string> Slots)> OpenAsync()
    {
        await using (var scope = Scope(out var database))
        {
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

        var visitor = _app!.CreateClient();
        var date = SystemClock.Instance.GetCurrentInstant().InUtc().Date.PlusDays(3).ToString("uuuu-MM-dd", null);
        var slots = await SlotsAsync(visitor, date);
        Assert.True(slots.Count > 12, $"{date} offers {slots.Count} times.");
        return (visitor, date, slots);
    }

    private static async Task<IReadOnlyList<string>> SlotsAsync(HttpClient visitor, string date)
    {
        var state = await visitor.GetFromJsonAsync<JsonElement>($"/api/scheduling/state?zone=Etc/UTC&date={date}");
        Assert.Equal(date, state.GetProperty("selectedDate").GetString());
        return [.. state.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("startsAt").GetString()!)];
    }

    /// <summary>Books with texts on, and waits until Google holds the event.</summary>
    private async Task<Guid> BookAsync(HttpClient visitor, string startsAt)
    {
        using var booked = await visitor.PostAsJsonAsync(
            "/api/scheduling/book",
            new { startsAt, name = "Ada", zoneId = "Etc/UTC", email = "ada@example.com", phone = "(202) 555-0143", smsConsent = true });
        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var id = (await booked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await WaitForAsync(id, booking => booking.GoogleEventId != null);
        return id;
    }

    private async Task<Appointment> WaitForAsync(Guid id, Func<Appointment, bool> reached, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            await using (var scope = Scope(out var database))
            {
                var appointment = await database.Appointments.AsNoTracking().SingleAsync(booking => booking.Id == id);
                if (reached(appointment)) return appointment;
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail(
                        $"Booking {id} did not follow its event in {seconds} s: "
                        + $"starts {appointment.StartsAt}, canceled {appointment.Cancelled}, event {appointment.GoogleEventId ?? "none"}.");
                }
            }

            await Task.Delay(250);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> reached, string what, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!reached())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail($"Waited {seconds} s for {what}.");
            await Task.Delay(250);
        }
    }

    private AsyncServiceScope Scope(out SchedulingDbContext database)
    {
        var scope = _app!.Factory.Services.CreateAsyncScope();
        database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        return scope;
    }

    public void Dispose()
    {
        _app?.Dispose();
        _database?.Dispose();
    }

    /// <summary>
    /// Google Calendar as the server meets it: it holds the event an invite
    /// created, and the test changes that event as a person would.
    /// </summary>
    private sealed class FakeGoogle : ICalendarInvites
    {
        private readonly ConcurrentDictionary<Guid, BookingEvent> _events = new();
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public void Decline(Guid id) =>
            _events.AddOrUpdate(id, _ => throw new InvalidOperationException("No event."), (_, held) => held with { Declined = true });

        public void Delete(Guid id) => _events[id] = BookingEvent.Deleted;

        public void Move(Guid id, Instant startsAt, Instant endsAt) =>
            _events[id] = BookingEvent.Scheduled(startsAt, endsAt, declined: false);

        public void Forget(Guid id) => _events.TryRemove(id, out _);

        public Task<SendResult> InviteAsync(Appointment appointment, CancellationToken cancellationToken)
        {
            _events[appointment.Id] = BookingEvent.Scheduled(appointment.StartsAt, appointment.EndsAt, declined: false);
            return Task.FromResult(SendResult.Confirmed(GoogleCalendarInvites.EventId(appointment)));
        }

        public Task<SendResult> WithdrawAsync(Appointment appointment, CancellationToken cancellationToken)
        {
            _events[appointment.Id] = BookingEvent.Deleted;
            return Task.FromResult(SendResult.Confirmed(GoogleCalendarInvites.WithdrawalId(GoogleCalendarInvites.EventId(appointment))));
        }

        public Task<BookingEvent> ReadAsync(Appointment appointment, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            return Task.FromResult(_events.TryGetValue(appointment.Id, out var held)
                ? held
                : BookingEvent.Unknown("Google answered 404."));
        }
    }
}
