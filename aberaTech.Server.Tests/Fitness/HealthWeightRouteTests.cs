using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Scheduling.Alerts;
using aberaTech.Server.Health;
using aberaTech.Server.Tests.Alerts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Apple Health weigh-ins from the paired phone: the phone's token stores
/// them as the Log tab's weigh-ins, one a day, and nothing else may.
/// </summary>
public sealed class HealthWeightRouteTests : IDisposable
{
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly FitnessApp _app;
    private readonly string _token = AlertDeviceTokens.New();

    public HealthWeightRouteTests()
    {
        _app = new FitnessApp(builder =>
        {
            builder.UseSetting("Admin:GoogleClientId", "test-client");
            builder.UseSetting("Admin:GoogleClientSecret", "test-secret");
            builder.UseSetting("Admin:AllowedEmails:0", "owner@example.test");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.RemoveAll<IAlertDeviceStore>();
                services.AddSingleton<IAlertDeviceStore>(_devices);
            });
        });
        _devices.CreateAsync(Guid.NewGuid(), "iPhone", AlertDeviceTokens.Hash(_token), FitnessApp.Now, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    public void Dispose() => _app.Dispose();

    private HttpClient Phone(string? token)
    {
        var client = _app.Factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HealthWeightsUpload Upload(params (string Date, double Kg)[] weights) =>
        new([.. weights.Select(w => new HealthWeight(w.Date, w.Kg))]);

    private async Task<Dictionary<LocalDate, BodyMetric>> StoredAsync()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        return await database.BodyMetrics.AsNoTracking().ToDictionaryAsync(m => m.Date);
    }

    [Fact]
    public async Task The_paired_phone_stores_its_weigh_ins_and_a_repeat_changes_nothing()
    {
        // 170.6 lb and 170.0 lb, his Lose It entries of 19 and 21 November.
        using var phone = Phone(_token);
        var upload = Upload(("2026-06-13", 77.38), ("2026-06-15", 77.11));

        var first = await phone.PostAsJsonAsync(HealthWeightEndpoints.Path, upload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(new HealthWeightsResult(2, 0), await first.Content.ReadFromJsonAsync<HealthWeightsResult>());

        var again = await phone.PostAsJsonAsync(HealthWeightEndpoints.Path, upload);
        Assert.Equal(new HealthWeightsResult(0, 2), await again.Content.ReadFromJsonAsync<HealthWeightsResult>());

        var stored = await StoredAsync();
        Assert.Equal(77.11, stored[new LocalDate(2026, 6, 15)].WeightKg, precision: 6);
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task A_day_already_held_takes_the_phones_weight_and_keeps_its_body_fat()
    {
        await _app.SeedAsync(database =>
        {
            database.BodyMetrics.Add(new BodyMetric { Id = Guid.NewGuid(), Date = new LocalDate(2026, 6, 14), WeightKg = 79.4, BodyFatPercent = 20 });
            return Task.CompletedTask;
        });

        using var phone = Phone(_token);
        var response = await phone.PostAsJsonAsync(HealthWeightEndpoints.Path, Upload(("2026-06-14", 78.0)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var day = (await StoredAsync())[new LocalDate(2026, 6, 14)];
        Assert.Equal(78.0, day.WeightKg, precision: 6);
        Assert.Equal(20, day.BodyFatPercent);
    }

    [Theory]
    [InlineData("2026-06-14", 29.0)]
    [InlineData("2026-06-14", 251.0)]
    [InlineData("2026-06-20", 77.0)]
    [InlineData("14 June", 77.0)]
    public async Task An_implausible_weigh_in_is_refused_and_nothing_is_stored(string date, double kg)
    {
        using var phone = Phone(_token);
        var response = await phone.PostAsJsonAsync(HealthWeightEndpoints.Path, Upload(("2026-06-13", 77.0), (date, kg)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task Without_the_phones_token_nothing_is_stored()
    {
        using var stranger = Phone(null);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await stranger.PostAsJsonAsync(HealthWeightEndpoints.Path, Upload(("2026-06-13", 77.0)))).StatusCode);

        using var revoked = Phone(AlertDeviceTokens.New());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await revoked.PostAsJsonAsync(HealthWeightEndpoints.Path, Upload(("2026-06-13", 77.0)))).StatusCode);

        Assert.Empty(await StoredAsync());
    }
}
