using aberaTech.Scheduling.Alerts;
using aberaTech.Scheduling.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The paired phones in Postgres: the hash is the row and the token never
/// is, five at most however many pairings race, and a revoked phone is gone.
/// </summary>
public sealed class DatabaseAlertDeviceStoreTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly TestDatabase? _database;

    public DatabaseAlertDeviceStoreTests()
    {
        if (TestPostgres.AdminConnectionString is null && !TestPostgres.Required) return;

        _database = new TestDatabase("alertdevices");
        using var context = Context();
        context.Database.Migrate();
    }

    private SchedulingDbContext Context() =>
        new(new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseNpgsql(_database!.ConnectionString, npgsql => npgsql.UseNodaTime())
            .Options);

    [PostgresFact]
    public async Task The_row_holds_the_hash_the_token_finds_it_and_revoking_removes_it()
    {
        var token = AlertDeviceTokens.New();
        var id = Guid.NewGuid();
        await using (var context = Context())
        {
            Assert.NotNull(await new DatabaseAlertDeviceStore(context)
                .CreateAsync(id, "Neb's iPhone", AlertDeviceTokens.Hash(token), Now, CancellationToken.None));
        }

        await using (var check = Context())
        {
            var row = await check.AlertDevices.SingleAsync();
            Assert.Equal(AlertDeviceTokens.Hash(token), row.TokenHash);
            Assert.Equal(32, row.TokenHash.Length);
            // The token itself is in no column.
            var text = await check.Database.SqlQuery<string>($"SELECT row_to_json(d)::text AS \"Value\" FROM \"AlertDevices\" d").SingleAsync();
            Assert.DoesNotContain(token[4..], text);

            var store = new DatabaseAlertDeviceStore(check);
            Assert.Equal(id, (await store.FindAsync(AlertDeviceTokens.Hash(token), CancellationToken.None))?.Id);
            Assert.Null(await store.FindAsync(AlertDeviceTokens.Hash(AlertDeviceTokens.New()), CancellationToken.None));
            Assert.True(await store.RevokeAsync(id, CancellationToken.None));
            Assert.False(await store.RevokeAsync(id, CancellationToken.None));
            Assert.Null(await store.FindAsync(AlertDeviceTokens.Hash(token), CancellationToken.None));
        }
    }

    [PostgresFact]
    public async Task The_zone_is_the_most_recently_seen_phones_that_reported_one()
    {
        var amman = Guid.NewGuid();
        var york = Guid.NewGuid();
        var silent = Guid.NewGuid();
        await using (var context = Context())
        {
            var store = new DatabaseAlertDeviceStore(context);
            Assert.Null(await store.LatestTimeZoneAsync(CancellationToken.None));
            foreach (var id in new[] { amman, york, silent })
            {
                await store.CreateAsync(id, id.ToString()[..8], AlertDeviceTokens.Hash(AlertDeviceTokens.New()), Now, CancellationToken.None);
            }

            await store.SetTimeZoneAsync(amman, "Asia/Amman", Now, CancellationToken.None);
            Assert.Equal("Asia/Amman", await store.LatestTimeZoneAsync(CancellationToken.None));

            await store.SetTimeZoneAsync(york, "America/New_York", Now + Duration.FromMinutes(1), CancellationToken.None);
            // A phone that never reported a zone is seen later and changes nothing.
            await store.TouchAsync(silent, Now + Duration.FromMinutes(5), Now + Duration.FromMinutes(5), CancellationToken.None);
            Assert.Equal("America/New_York", await store.LatestTimeZoneAsync(CancellationToken.None));

            // The Amman phone makes a request after it: its zone is the one now.
            await store.TouchAsync(amman, Now + Duration.FromMinutes(10), Now + Duration.FromMinutes(10), CancellationToken.None);
            Assert.Equal("Asia/Amman", await store.LatestTimeZoneAsync(CancellationToken.None));
        }

        await using var check = Context();
        var row = await check.AlertDevices.SingleAsync(device => device.Id == amman);
        Assert.Equal("Asia/Amman", row.TimeZone);
        Assert.Equal(Now + Duration.FromMinutes(10), row.LastSeenAt);
        Assert.Equal("Asia/Amman", (await new DatabaseAlertDeviceStore(check).FindAsync(row.TokenHash, CancellationToken.None))?.TimeZone);
    }

    [PostgresFact]
    public async Task Ten_pairings_at_once_make_five_phones()
    {
        var contexts = Enumerable.Range(0, 10).Select(_ => Context()).ToList();
        try
        {
            var made = await Task.WhenAll(contexts.Select((context, n) => new DatabaseAlertDeviceStore(context)
                .CreateAsync(Guid.NewGuid(), $"Phone {n}", AlertDeviceTokens.Hash(AlertDeviceTokens.New()), Now, CancellationToken.None)));

            Assert.Equal(AlertDeviceTokens.MaxDevices, made.Count(device => device is not null));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }

        await using var check = Context();
        Assert.Equal(AlertDeviceTokens.MaxDevices, await check.AlertDevices.CountAsync());
    }

    [PostgresFact]
    public async Task Last_seen_is_written_only_when_the_last_write_is_older_than_the_limit()
    {
        var id = Guid.NewGuid();
        await using var context = Context();
        var store = new DatabaseAlertDeviceStore(context);
        await store.CreateAsync(id, "Phone", AlertDeviceTokens.Hash(AlertDeviceTokens.New()), Now, CancellationToken.None);

        await store.TouchAsync(id, Now, Now - Duration.FromMinutes(1), CancellationToken.None);
        await store.TouchAsync(id, Now + Duration.FromSeconds(30), Now - Duration.FromSeconds(30), CancellationToken.None);
        Assert.Equal(Now, (await store.ListAsync(CancellationToken.None)).Single().LastSeenAt);

        await store.TouchAsync(id, Now + Duration.FromMinutes(2), Now + Duration.FromMinutes(1), CancellationToken.None);
        Assert.Equal(Now + Duration.FromMinutes(2), (await store.ListAsync(CancellationToken.None)).Single().LastSeenAt);
    }

    private static readonly string PushToken = new('a', 64);

    private async Task<Guid> PairedAsync(DatabaseAlertDeviceStore store, string name = "Phone", int secondsLater = 0)
    {
        var id = Guid.NewGuid();
        await store.CreateAsync(
            id, name, AlertDeviceTokens.Hash(AlertDeviceTokens.New()), Now + Duration.FromSeconds(secondsLater), CancellationToken.None);
        return id;
    }

    [PostgresFact]
    public async Task A_push_token_is_stored_replaced_listed_as_a_bool_and_cleared()
    {
        await using var context = Context();
        var store = new DatabaseAlertDeviceStore(context);
        var id = await PairedAsync(store);
        await PairedAsync(store, "No push", secondsLater: 1);

        await store.SetPushAsync(id, PushToken, "sandbox", CancellationToken.None);
        await store.SetPushAsync(id, new string('b', 64), "production", CancellationToken.None);

        var target = Assert.Single(await store.PushTargetsAsync(CancellationToken.None));
        Assert.Equal((id, new string('b', 64), "production"), (target.DeviceId, target.Token, target.Environment));
        Assert.Equal([true, false], (await store.ListAsync(CancellationToken.None)).Select(device => device.Push));

        // Apple said a token that is no longer stored is dead: nothing changes.
        await store.ClearPushIfAsync(id, PushToken, CancellationToken.None);
        Assert.Single(await store.PushTargetsAsync(CancellationToken.None));

        await store.SetPushAsync(id, null, null, CancellationToken.None);
        Assert.Empty(await store.PushTargetsAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task The_plan_version_moves_on_a_change_and_not_on_the_same_alarms()
    {
        await using var context = Context();
        var store = new DatabaseAlertDeviceStore(context);
        var alarms = SHA256Of("standup");

        Assert.Equal(0, await store.PlanVersionAsync(CancellationToken.None));
        Assert.True(await store.BumpPlanAsync(alarms, force: false, Now, CancellationToken.None));
        Assert.False(await store.BumpPlanAsync(alarms, force: false, Now, CancellationToken.None));
        Assert.Equal(1, await store.PlanVersionAsync(CancellationToken.None));

        Assert.True(await store.BumpPlanAsync(SHA256Of("standup moved"), force: false, Now, CancellationToken.None));
        // A forced change with no fingerprint keeps the stored one.
        Assert.True(await store.BumpPlanAsync(null, force: true, Now, CancellationToken.None));
        Assert.False(await store.BumpPlanAsync(SHA256Of("standup moved"), force: false, Now, CancellationToken.None));
        Assert.Equal(3, await store.PlanVersionAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task Ten_replicas_reading_the_same_change_bump_the_version_once()
    {
        await using (var first = Context())
        {
            await new DatabaseAlertDeviceStore(first).BumpPlanAsync(SHA256Of("before"), force: false, Now, CancellationToken.None);
        }

        var contexts = Enumerable.Range(0, 10).Select(_ => Context()).ToList();
        try
        {
            var bumped = await Task.WhenAll(contexts.Select(context =>
                new DatabaseAlertDeviceStore(context).BumpPlanAsync(SHA256Of("after"), force: false, Now, CancellationToken.None)));

            Assert.Equal(1, bumped.Count(moved => moved));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }

        await using var check = Context();
        Assert.Equal(2, await new DatabaseAlertDeviceStore(check).PlanVersionAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task Ten_replicas_claiming_one_push_at_once_get_one_claim_and_the_window_holds_the_next()
    {
        long version;
        Guid id;
        await using (var setup = Context())
        {
            var store = new DatabaseAlertDeviceStore(setup);
            id = await PairedAsync(store);
            await store.SetPushAsync(id, PushToken, "production", CancellationToken.None);
            await store.BumpPlanAsync(null, force: true, Now, CancellationToken.None);
            version = await store.PlanVersionAsync(CancellationToken.None);
        }

        var contexts = Enumerable.Range(0, 10).Select(_ => Context()).ToList();
        try
        {
            var claims = await Task.WhenAll(contexts.Select(context =>
                new DatabaseAlertDeviceStore(context).TryClaimPushAsync(id, version, Now, Now - Duration.FromSeconds(60), CancellationToken.None)));

            Assert.Equal(1, claims.Count(claimed => claimed));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }

        await using var check = Context();
        var again = new DatabaseAlertDeviceStore(check);
        await again.BumpPlanAsync(null, force: true, Now, CancellationToken.None);
        var next = await again.PlanVersionAsync(CancellationToken.None);
        var later = Now + Duration.FromSeconds(30);
        Assert.False(await again.TryClaimPushAsync(id, next, later, later - Duration.FromSeconds(60), CancellationToken.None));
        later = Now + Duration.FromSeconds(60);
        Assert.True(await again.TryClaimPushAsync(id, next, later, later - Duration.FromSeconds(60), CancellationToken.None));
        var target = Assert.Single(await again.PushTargetsAsync(CancellationToken.None));
        Assert.Equal((next, later), (target.PushedVersion, target.PushedAt!.Value));
    }

    [PostgresFact]
    public async Task A_new_token_holds_the_current_version_so_registering_pushes_nothing()
    {
        await using var context = Context();
        var store = new DatabaseAlertDeviceStore(context);
        var id = await PairedAsync(store);
        await store.BumpPlanAsync(null, force: true, Now, CancellationToken.None);
        await store.BumpPlanAsync(null, force: true, Now, CancellationToken.None);

        await store.SetPushAsync(id, PushToken, "production", CancellationToken.None);

        Assert.Equal(2, Assert.Single(await store.PushTargetsAsync(CancellationToken.None)).PushedVersion);
        Assert.False(await store.TryClaimPushAsync(id, 2, Now, Now, CancellationToken.None));
    }

    private static byte[] SHA256Of(string text) => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));

    public void Dispose() => _database?.Dispose();
}
