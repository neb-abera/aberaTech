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

    public void Dispose() => _database?.Dispose();
}
