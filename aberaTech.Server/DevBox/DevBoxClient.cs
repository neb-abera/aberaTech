using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;

namespace aberaTech.Server.DevBox;

/// <summary>
/// The two calls the owner page makes to Azure Resource Manager: read the
/// power state of the dev box, and start it.
/// </summary>
/// <remarks>
/// Plain REST rather than the resource manager SDK: two requests do not earn
/// a package, its transitive graph and a regenerated lock file. The token
/// comes from the container app's managed identity through the same
/// <see cref="TokenCredential"/> the Postgres connection uses, so locally it
/// is the developer's az login and in production it is the app itself. The
/// identity holds one custom role, "Dev box start", scoped to the one VM:
/// start and read. It cannot deallocate, resize or touch anything else.
/// </remarks>
public sealed class DevBoxClient(HttpClient http, TokenCredential credential, DevBoxOptions options) : IDevBoxClient
{
    public const string ManagementScope = "https://management.azure.com/.default";

    private const string ApiVersion = "2024-07-01";

    /// <summary>The power state as Azure spells it, without its prefix: running, deallocated, starting…</summary>
    public async Task<string> GetPowerStateAsync(CancellationToken cancellationToken)
    {
        using var request = await RequestAsync(HttpMethod.Get, "instanceView", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        if (body.RootElement.TryGetProperty("statuses", out var statuses))
        {
            foreach (var status in statuses.EnumerateArray())
            {
                var code = status.TryGetProperty("code", out var value) ? value.GetString() : null;
                if (code is not null && code.StartsWith("PowerState/", StringComparison.Ordinal))
                {
                    return code["PowerState/".Length..];
                }
            }
        }

        return "unknown";
    }

    /// <summary>Asks Azure to start the VM. Azure answers 202 and does the rest; a VM already running is a no-op.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var request = await RequestAsync(HttpMethod.Post, "start", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string action, CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([ManagementScope]), cancellationToken);

        var url = "https://management.azure.com"
                  + $"/subscriptions/{Uri.EscapeDataString(options.SubscriptionId!)}"
                  + $"/resourceGroups/{Uri.EscapeDataString(options.ResourceGroup)}"
                  + $"/providers/Microsoft.Compute/virtualMachines/{Uri.EscapeDataString(options.VmName)}"
                  + $"/{action}?api-version={ApiVersion}";

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        }

        return request;
    }
}
