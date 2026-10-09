using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace aberaTech.Server.Playbook;

/// <summary>Notion answered with an error status. The status only: Notion's message can quote the request.</summary>
public sealed class NotionException(HttpStatusCode status) : Exception($"Notion answered {(int)status}")
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>Notion asked for a longer wait than a page load can hold.</summary>
public sealed class NotionThrottledException(TimeSpan retryAfter) : Exception("Notion is rate limiting")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

/// <summary>
/// The four Notion calls the playbook needs: a page, a block, a block's
/// children and a data source's pages. Reads only.
/// </summary>
/// <remarks>
/// Answers are kept in memory for <see cref="NotionOptions.CacheSeconds"/>,
/// so reading the playbook costs Notion a few calls every few minutes rather
/// than one per click. A file's block is asked fresh every time, because the
/// signed address in it expires after an hour. A 429 is waited out as its
/// Retry-After says, up to <see cref="MaxWait"/>, at most
/// <see cref="Attempts"/> times. Lists follow <c>has_more</c> to the end.
/// </remarks>
public sealed class NotionClient(
    IHttpClientFactory clients,
    IMemoryCache cache,
    TimeProvider time,
    NotionOptions options)
{
    /// <summary>The API version this client is written against. developers.notion.com/reference/versioning.</summary>
    public const string Version = "2026-03-11";

    public const string ApiClient = "notion-api";

    /// <summary>For the signed file addresses. It sends no token: they are not Notion's API.</summary>
    public const string FilesClient = "notion-files";

    public const int Attempts = 3;

    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(1);

    private TimeSpan CacheFor => TimeSpan.FromSeconds(Math.Max(0, options.CacheSeconds));

    public Task<JsonElement> PageAsync(string id, CancellationToken cancellationToken) =>
        GetAsync($"/v1/pages/{id}", cached: true, cancellationToken);

    public Task<JsonElement> DatabaseAsync(string id, CancellationToken cancellationToken) =>
        GetAsync($"/v1/databases/{id}", cached: true, cancellationToken);

    public Task<JsonElement> BlockAsync(string id, bool fresh, CancellationToken cancellationToken) =>
        GetAsync($"/v1/blocks/{id}", cached: !fresh, cancellationToken);

    /// <summary>Every child of a block or page, across as many pages of results as Notion has.</summary>
    public async Task<IReadOnlyList<JsonElement>> ChildrenAsync(string id, CancellationToken cancellationToken)
    {
        var key = $"notion:children:{id}";
        if (cache.TryGetValue(key, out IReadOnlyList<JsonElement>? hit) && hit is not null) return hit;

        var all = new List<JsonElement>();
        string? cursor = null;
        do
        {
            var path = $"/v1/blocks/{id}/children?page_size=100"
                       + (cursor is null ? "" : "&start_cursor=" + Uri.EscapeDataString(cursor));
            var page = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
            cursor = Collect(page, all);
        } while (cursor is not null);

        cache.Set(key, (IReadOnlyList<JsonElement>)all, CacheFor);
        return all;
    }

    /// <summary>Every page in a data source, across as many pages of results as Notion has.</summary>
    public async Task<IReadOnlyList<JsonElement>> QueryAsync(string dataSourceId, CancellationToken cancellationToken)
    {
        var key = $"notion:query:{dataSourceId}";
        if (cache.TryGetValue(key, out IReadOnlyList<JsonElement>? hit) && hit is not null) return hit;

        var all = new List<JsonElement>();
        string? cursor = null;
        do
        {
            var body = cursor is null
                ? """{"page_size":100}"""
                : JsonSerializer.Serialize(new { page_size = 100, start_cursor = cursor });
            var page = await SendAsync(HttpMethod.Post, $"/v1/data_sources/{dataSourceId}/query", body, cancellationToken);
            cursor = Collect(page, all);
        } while (cursor is not null);

        cache.Set(key, (IReadOnlyList<JsonElement>)all, CacheFor);
        return all;
    }

    /// <summary>
    /// A signed file address, opened for streaming. Never cached and never
    /// sent the token. The caller disposes the response.
    /// </summary>
    public async Task<HttpResponseMessage> DownloadAsync(Uri address, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(FilesClient);
        var response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.IsSuccessStatusCode) return response;

        var status = response.StatusCode;
        response.Dispose();
        throw new NotionException(status);
    }

    private async Task<JsonElement> GetAsync(string path, bool cached, CancellationToken cancellationToken)
    {
        var key = "notion:get:" + path;
        if (cached && cache.TryGetValue(key, out JsonElement hit)) return hit;

        var answer = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        cache.Set(key, answer, CacheFor);
        return answer;
    }

    private static string? Collect(JsonElement page, List<JsonElement> into)
    {
        if (page.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            into.AddRange(results.EnumerateArray());
        }

        var more = page.TryGetProperty("has_more", out var hasMore) && hasMore.ValueKind == JsonValueKind.True;
        return more && page.TryGetProperty("next_cursor", out var next) && next.ValueKind == JsonValueKind.String
            ? next.GetString()
            : null;
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, string? body, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(ApiClient);
        var address = new Uri(new Uri(options.BaseUrl), path);

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, address);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            request.Headers.Add("Notion-Version", Version);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = RetryAfter(response);
                if (attempt >= Attempts || wait > MaxWait) throw new NotionThrottledException(wait);
                await Task.Delay(wait, time, cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode) throw new NotionException(response.StatusCode);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.Clone();
        }
    }

    private TimeSpan RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        if (header?.Date is { } date)
        {
            var left = date - time.GetUtcNow();
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        return DefaultWait;
    }
}
