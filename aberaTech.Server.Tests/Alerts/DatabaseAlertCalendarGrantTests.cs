using System.Net;
using aberaTech.Scheduling.Admin;
using aberaTech.Scheduling.Alerts;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The stored connection the alerts write through: the row
/// /schedule/admin keeps, read for its calendar, its address and whether
/// the grant carries the events scope. Nothing here reaches Google.
/// </summary>
public sealed class DatabaseAlertCalendarGrantTests : IDisposable
{
    private readonly TestDatabase? _database;
    private readonly RecordingHandler _google = new(() => RecordingHandler.Text(HttpStatusCode.BadRequest, "{}"));

    public DatabaseAlertCalendarGrantTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("alerts_grant");
        using var context = Context();
        context.Database.Migrate();
    }

    private SchedulingDbContext Context() =>
        new(new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(_database!.ConnectionString, npgsql => npgsql.UseNodaTime())
            .Options);

    private DatabaseAlertCalendarGrant Grant(SchedulingDbContext context) =>
        new(context, new GoogleAccessTokens(
            new HttpClient(_google),
            new AdminOptions(),
            new EphemeralDataProtectionProvider(),
            new FakeClock(Instant.FromUtc(2026, 10, 28, 12, 0)),
            NullLogger<GoogleAccessTokens>.Instance));

    [PostgresFact]
    public async Task No_row_is_no_connection_and_no_token()
    {
        await using var context = Context();

        Assert.Null(await Grant(context).CurrentAsync(CancellationToken.None));
        Assert.Null(await Grant(context).AccessTokenAsync(CancellationToken.None));
        Assert.Empty(_google.Requests);
    }

    [PostgresFact]
    public async Task The_row_gives_the_calendar_the_address_and_whether_it_can_edit()
    {
        await using (var context = Context())
        {
            context.HostCalendarCredentials.Add(new HostCalendarCredential
            {
                Id = Guid.NewGuid(),
                ProtectedRefreshToken = "not protected by this key ring",
                CalendarId = "primary",
                ConnectedEmail = "owner@example.test",
                ConnectedAt = Instant.FromUtc(2026, 9, 1, 0, 0),
                GrantedScopes = $"openid email {CalendarAdminEndpoints.ReadOnlyScope}"
            });
            await context.SaveChangesAsync();
        }

        await using (var check = Context())
        {
            Assert.Equal(new CalendarGrant("primary", "owner@example.test", false), await Grant(check).CurrentAsync(CancellationToken.None));
            // A token this key ring cannot read is no token, and Google is never asked.
            Assert.Null(await Grant(check).AccessTokenAsync(CancellationToken.None));
            Assert.Empty(_google.Requests);
        }

        await using (var context = Context())
        {
            var row = await context.HostCalendarCredentials.SingleAsync();
            row.GrantedScopes += $" {CalendarAdminEndpoints.EventsScope}";
            await context.SaveChangesAsync();
        }

        await using var after = Context();
        Assert.True((await Grant(after).CurrentAsync(CancellationToken.None))!.CanEdit);
    }

    public void Dispose() => _database?.Dispose();
}
