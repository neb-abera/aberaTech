using System.Net;
using aberaTech.Scheduling.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// Pushover's users/validate.json read as the devices it delivers to: a
/// list, empty for "no active devices", and null when it cannot be read.
/// </summary>
public sealed class PushoverDevicesTests
{
    private static async Task<(IReadOnlyList<string>? Devices, RecordingHandler Handler)> Read(HttpStatusCode status, string body)
    {
        var handler = new RecordingHandler(() => RecordingHandler.Text(status, body, "application/json"));
        var services = new ServiceCollection();
        services.AddSingleton(new AlertsOptions { PushoverAppToken = "app", PushoverUserKey = "user" });
        services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        return (await provider.GetRequiredService<PushoverClient>().DevicesAsync(CancellationToken.None), handler);
    }

    [Fact]
    public async Task A_valid_user_lists_its_devices_and_the_keys_go_in_the_body()
    {
        var (devices, handler) = await Read(HttpStatusCode.OK, "{\"status\":1,\"group\":0,\"devices\":[\"iphone17pro\"],\"request\":\"r\"}");
        Assert.Equal(["iphone17pro"], devices);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(PushoverClient.ValidateEndpoint, sent.Url.ToString());
        Assert.Equal("user", sent.Form["user"]);
    }

    [Fact]
    public async Task No_active_devices_is_an_empty_list()
    {
        var (devices, _) = await Read(
            HttpStatusCode.BadRequest,
            "{\"user\":\"valid, but has no active devices\",\"devices\":[],\"errors\":[\"user is valid but has no active devices\"],\"status\":0,\"request\":\"r\"}");
        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    [Fact]
    public async Task Any_other_answer_is_unknown()
    {
        Assert.Null((await Read(HttpStatusCode.BadRequest, "{\"status\":0,\"errors\":[\"user key is invalid\"]}")).Devices);
        Assert.Null((await Read(HttpStatusCode.InternalServerError, "oops")).Devices);
    }
}
