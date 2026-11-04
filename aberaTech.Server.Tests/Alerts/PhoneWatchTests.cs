using System.Net;
using aberaTech.Scheduling.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// A paired phone quiet for more than 12 hours gets one Pushover warning per
/// silence, on every replica together, and a check-in starts the count again.
/// </summary>
public sealed class PhoneWatchTests : IDisposable
{
    private static readonly Instant Paired = Instant.FromUtc(2026, 11, 3, 6, 0);
    private static readonly DateTimeZone Amman = DateTimeZoneProviders.Tzdb["Asia/Amman"];

    private readonly InMemoryAlertStore _store = new();
    private readonly InMemoryAlertDeviceStore _devices = new();
    private readonly RecordingHandler _pushover =
        new(() => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1,\"request\":\"r\"}", "application/json"));
    private readonly ServiceProvider _provider;
    private readonly AlertsOptions _options = new() { PushoverAppToken = "app", PushoverUserKey = "user", PushoverRetrySeconds = 0 };

    public PhoneWatchTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_options);
        services.AddSingleton<IAlertStore>(_store);
        services.AddSingleton<IAlertDeviceStore>(_devices);
        services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover);
        _provider = services.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    private Task Check(Instant now) => PhoneWatch.CheckAsync(
        _provider.GetRequiredService<IServiceScopeFactory>(), AlertSettings.Defaults(_options), Amman, now,
        NullLogger.Instance, CancellationToken.None);

    private async Task<AlertDevice> Phone(string name, Instant seen)
    {
        var device = (await _devices.CreateAsync(Guid.NewGuid(), name, [1, 2, Convert.ToByte(name.Length)], Paired, CancellationToken.None))!;
        await _devices.TouchAsync(device.Id, seen, seen, CancellationToken.None);
        return device;
    }

    [Fact]
    public async Task A_phone_quiet_for_twelve_hours_or_less_gets_nothing()
    {
        await Phone("iPhone", Paired);
        await Check(Paired + PhoneWatch.QuietAfter);
        Assert.Empty(_pushover.Requests);
    }

    [Fact]
    public async Task A_phone_quiet_longer_gets_one_high_priority_warning_that_names_it_and_when()
    {
        await Phone("iphone 17 pro", Paired);
        var now = Paired + Duration.FromHours(13);

        await Check(now);
        await Check(now + Duration.FromMinutes(5));
        await Check(now + Duration.FromHours(6));

        var sent = Assert.Single(_pushover.Requests);
        Assert.Equal("1", sent.Form["priority"]);
        Assert.Equal("Your phone is not checking in", sent.Form["title"]);
        Assert.Equal(
            "iphone 17 pro last checked in 13 hours ago, Tue 3 Nov 9:00 AM. Open the Alarms app on it so new and moved alarms are set. Alarms already on it still ring.",
            sent.Form["message"]);
    }

    [Fact]
    public async Task A_check_in_starts_a_new_silence_that_warns_again()
    {
        var phone = await Phone("iPhone", Paired);
        await Check(Paired + Duration.FromHours(13));
        var back = Paired + Duration.FromHours(14);
        await _devices.TouchAsync(phone.Id, back, back, CancellationToken.None);

        await Check(back + Duration.FromHours(1));
        Assert.Single(_pushover.Requests);

        await Check(back + Duration.FromHours(13));
        Assert.Equal(2, _pushover.Requests.Count);
    }

    [Fact]
    public async Task Each_quiet_phone_is_named_and_a_busy_one_is_not()
    {
        var now = Paired + Duration.FromHours(20);
        await Phone("Old phone", Paired);
        await Phone("New phone", now - Duration.FromMinutes(3));

        await Check(now);

        var sent = Assert.Single(_pushover.Requests);
        Assert.StartsWith("Old phone ", sent.Form["message"]);
    }

    [Fact]
    public async Task Two_replicas_send_one_warning()
    {
        await Phone("iPhone", Paired);
        var now = Paired + Duration.FromHours(13);
        await Task.WhenAll(Check(now), Check(now));
        Assert.Single(_pushover.Requests);
    }
}
