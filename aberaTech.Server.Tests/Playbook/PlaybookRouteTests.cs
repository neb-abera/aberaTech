using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using aberaTech.Server.Playbook;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Playbook;

/// <summary>
/// The playbook from three chairs, against a Notion in memory
/// (<see cref="FakeNotion"/>) that pages every list two results at a time.
/// A visitor is told to sign in, a stranger is refused, and the owner reads
/// the tree, a page and a file. Nothing outside the root page is served,
/// whatever the token could reach.
/// </summary>
public sealed class PlaybookRouteTests : IDisposable
{
    private const string Token = "notion-token-for-tests";

    private readonly FakeNotion _notion = FakeNotion.Sample();
    private readonly RecordingTime _time = new();
    private readonly TestApp _app;
    private readonly string _outside;
    private readonly string _outsideFile;
    private readonly string _extra;

    public PlaybookRouteTests()
    {
        // A page the token reaches that is not under the root, with a file.
        _outside = _notion.Page("Somewhere else");
        _outsideFile = _notion.File(_outside, "file", "other.txt", "text/plain", "not yours"u8.ToArray());
        // A page under the root, and a link to it the way Notion writes one.
        _extra = _notion.Page("Extra", FakeNotion.SampleRoot);
        _notion.Text(FakeNotion.SampleRoot, "paragraph",
            FakeNotion.Run("Open extra", href: "https://www.notion.so/Extra-" + _extra.Replace("-", "")),
            FakeNotion.Run(" or elsewhere", href: "https://www.notion.so/" + _outside.Replace("-", "")));

        _app = App(Settings(), _notion, _time);
    }

    private static Dictionary<string, string?> Settings(string? token = Token, string? root = FakeNotion.SampleRoot)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["Notion:Token"] = token;
        settings["Notion:PlaybookPageId"] = root;
        return settings;
    }

    private static TestApp App(Dictionary<string, string?> settings, FakeNotion notion, TimeProvider? time = null, string environment = "Production") =>
        new(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(NotionClient.ApiClient).ConfigurePrimaryHttpMessageHandler(() => notion);
            services.AddHttpClient(NotionClient.FilesClient).ConfigurePrimaryHttpMessageHandler(() => notion);
            if (time is not null) services.AddSingleton(time);
        }, environment);

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    public static IEnumerable<object[]> Routes =>
    [
        ["GET", "/api/playbook"],
        ["GET", $"/api/playbook/pages/{FakeNotion.SampleRoot}"],
        ["GET", "/api/playbook/files/00000002-0000-0000-0000-000000000000"]
    ];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_visitor_with_no_session_is_told_to_sign_in(string method, string path)
    {
        using var visitor = _app.CreateClient();

        using var response = await AdminRouteTests.Send(visitor, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_notion.Requests);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_google_account_that_is_not_the_owner_is_refused(string method, string path)
    {
        using var stranger = _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Stranger);

        using var response = await AdminRouteTests.Send(stranger, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_notion.Requests);
    }

    [Fact]
    public async Task The_owner_reads_the_tree_across_every_page_of_results()
    {
        using var owner = Owner();

        var body = await owner.GetFromJsonAsync<JsonElement>("/api/playbook");

        Assert.True(body.GetProperty("configured").GetBoolean());
        var root = body.GetProperty("root");
        Assert.Equal(FakeNotion.SampleRoot, root.GetProperty("id").GetString());
        Assert.Equal("Field notes", root.GetProperty("title").GetString());
        var children = root.GetProperty("children").EnumerateArray().ToList();
        Assert.Equal(["Checklists", "Templates", "Extra"], children.Select(child => child.GetProperty("title").GetString()));
        var templates = children[1];
        Assert.Equal("database", templates.GetProperty("kind").GetString());
        Assert.Equal(["Letter template", "Meeting template"],
            templates.GetProperty("children").EnumerateArray().Select(page => page.GetProperty("title").GetString()));
        Assert.DoesNotContain("Somewhere else", body.GetRawText());

        // The root has more than two blocks, so its list took several calls.
        var requests = _notion.Requests.ToList();
        Assert.Contains(requests, seen => seen.Request.StartsWith($"GET /v1/blocks/{FakeNotion.SampleRoot}/children", StringComparison.Ordinal)
                                         && seen.Request.Contains("start_cursor=", StringComparison.Ordinal));
        Assert.Contains(requests, seen => seen.Request.StartsWith("POST /v1/data_sources/", StringComparison.Ordinal));
        Assert.All(requests, seen =>
        {
            Assert.Equal("Bearer " + Token, seen.Authorization);
            Assert.Equal(NotionClient.Version, seen.Version);
        });
    }

    [Fact]
    public async Task Answers_are_reused_rather_than_asked_again()
    {
        using var owner = Owner();

        await owner.GetFromJsonAsync<JsonElement>("/api/playbook");
        await owner.GetFromJsonAsync<JsonElement>($"/api/playbook/pages/{FakeNotion.SampleRoot}");
        var asked = _notion.Requests.Count;
        await owner.GetFromJsonAsync<JsonElement>("/api/playbook");
        await owner.GetFromJsonAsync<JsonElement>($"/api/playbook/pages/{FakeNotion.SampleRoot}");

        Assert.Equal(asked, _notion.Requests.Count);
    }

    [Fact]
    public async Task The_owner_reads_a_page_as_blocks()
    {
        using var owner = Owner();

        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/playbook/pages/{FakeNotion.SampleRoot}");

        Assert.Equal("Field notes", page.GetProperty("title").GetString());
        var blocks = page.GetProperty("blocks").EnumerateArray().ToList();
        Assert.Equal(
            ["heading", "paragraph", "heading", "numbered", "numbered", "numbered", "todo", "todo", "toggle", "quote",
             "callout", "code", "divider", "table", "file", "page", "database", "page", "paragraph"],
            blocks.Select(block => block.GetProperty("type").GetString()));

        Assert.Equal(1, blocks[0].GetProperty("level").GetInt32());
        var runs = blocks[1].GetProperty("text").EnumerateArray().ToList();
        Assert.True(runs[1].GetProperty("bold").GetBoolean());
        Assert.True(runs[3].GetProperty("italic").GetBoolean());
        Assert.True(runs[5].GetProperty("code").GetBoolean());
        Assert.Equal("https://example.com/", runs[7].GetProperty("href").GetString());
        Assert.False(runs[0].TryGetProperty("bold", out _));

        var nested = blocks[3].GetProperty("children").EnumerateArray().ToList();
        Assert.Equal(["Dates", "Names"], nested.Select(item => item.GetProperty("text")[0].GetProperty("text").GetString()));
        Assert.True(blocks[6].GetProperty("checked").GetBoolean());
        Assert.False(blocks[7].GetProperty("checked").GetBoolean());
        Assert.Equal("paragraph", blocks[8].GetProperty("children")[0].GetProperty("type").GetString());
        Assert.Equal("💡", blocks[10].GetProperty("icon").GetString());
        Assert.Equal("shell", blocks[11].GetProperty("language").GetString());

        var table = blocks[13];
        Assert.True(table.GetProperty("headerRow").GetBoolean());
        Assert.Equal(3, table.GetProperty("rows").GetArrayLength());
        Assert.Equal("Owner", table.GetProperty("rows")[0][1][0].GetProperty("text").GetString());

        var file = blocks[14];
        Assert.Equal("example.txt", file.GetProperty("name").GetString());
        Assert.Equal("file", file.GetProperty("kind").GetString());
        Assert.False(file.TryGetProperty("url", out _));

        Assert.Equal("Checklists", blocks[15].GetProperty("title").GetString());
        Assert.Equal(2, blocks[16].GetProperty("pages").GetArrayLength());

        // A link to a playbook page opens it here. A link to a page outside
        // the root goes nowhere.
        var links = blocks[18].GetProperty("text").EnumerateArray().ToList();
        Assert.Equal("/playbook?page=" + _extra, links[0].GetProperty("href").GetString());
        Assert.False(links[1].TryGetProperty("href", out _));
    }

    [Fact]
    public async Task A_page_in_a_database_reads_like_any_other()
    {
        using var owner = Owner();
        var tree = await owner.GetFromJsonAsync<JsonElement>("/api/playbook");
        var letter = tree.GetProperty("root").GetProperty("children")[1].GetProperty("children")[0].GetProperty("id").GetString();

        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/playbook/pages/{letter}");

        Assert.Equal("Letter template", page.GetProperty("title").GetString());
        Assert.Equal("Dear reader,", page.GetProperty("blocks")[0].GetProperty("text")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("not-a-notion-id")]
    [InlineData("00000000-0000-0000-0000-0000000000ff")]
    public async Task An_id_that_is_no_page_is_not_found(string id)
    {
        using var owner = Owner();

        using var response = await owner.GetAsync($"/api/playbook/pages/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_page_the_token_reaches_outside_the_root_is_not_found()
    {
        using var owner = Owner();

        using var page = await owner.GetAsync($"/api/playbook/pages/{_outside}");
        // Without dashes too: the same page by Notion's other spelling.
        using var bare = await owner.GetAsync($"/api/playbook/pages/{_outside.Replace("-", "")}");

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bare.StatusCode);
        Assert.DoesNotContain(_notion.Requests, seen => seen.Request.Contains($"/v1/blocks/{_outside}/children", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_file_under_the_root_downloads_with_its_name_fetched_fresh_each_time()
    {
        using var owner = Owner();
        var checklists = (await owner.GetFromJsonAsync<JsonElement>("/api/playbook"))
            .GetProperty("root").GetProperty("children")[0].GetProperty("id").GetString();
        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/playbook/pages/{checklists}");
        var pdf = page.GetProperty("blocks").EnumerateArray().Single(block => block.GetProperty("type").GetString() == "file");
        Assert.Equal("pdf", pdf.GetProperty("kind").GetString());
        var id = pdf.GetProperty("id").GetString();

        for (var download = 0; download < 2; download++)
        {
            using var response = await owner.GetAsync($"/api/playbook/files/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal("packing-list.pdf", response.Content.Headers.ContentDisposition?.FileName);
            Assert.StartsWith("%PDF-1.4", await response.Content.ReadAsStringAsync());
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }

        // The block was asked of Notion for each download, and the signed
        // address was never sent the token.
        Assert.Equal(2, _notion.Requests.Count(seen => seen.Request == $"GET /v1/blocks/{id}"));
        Assert.All(_notion.Requests.Where(seen => seen.Request.StartsWith("GET /secure/", StringComparison.Ordinal)),
            seen => Assert.Null(seen.Authorization));
    }

    [Fact]
    public async Task A_file_outside_the_root_is_not_found_and_never_fetched()
    {
        using var owner = Owner();

        using var response = await owner.GetAsync($"/api/playbook/files/{_outsideFile}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(_notion.Requests, seen => seen.Request.StartsWith("GET /secure/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_block_under_the_root_that_is_not_a_file_is_not_found()
    {
        using var owner = Owner();
        var paragraph = _notion.Text(FakeNotion.SampleRoot, "paragraph", FakeNotion.Run("Just text"));

        using var response = await owner.GetAsync($"/api/playbook/files/{paragraph}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_rate_limit_is_waited_out_as_notion_asks()
    {
        _notion.Throttle(TimeSpan.FromSeconds(2));
        using var owner = Owner();

        using var response = await owner.GetAsync("/api/playbook");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(TimeSpan.FromSeconds(2), _time.Waits);
        Assert.Equal(2, _notion.Requests.Count(seen => seen.Request == $"GET /v1/pages/{FakeNotion.SampleRoot}"));
    }

    [Fact]
    public async Task A_rate_limit_longer_than_a_page_load_is_passed_on()
    {
        _notion.Throttle(TimeSpan.FromSeconds(60));
        using var owner = Owner();

        using var response = await owner.GetAsync("/api/playbook");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
        Assert.DoesNotContain(TimeSpan.FromSeconds(60), _time.Waits);
    }

    [Fact]
    public async Task Notion_still_refusing_after_the_last_try_is_a_503_that_names_no_token()
    {
        for (var i = 0; i < NotionClient.Attempts; i++) _notion.Throttle(TimeSpan.FromSeconds(1));
        using var owner = Owner();

        using var response = await owner.GetAsync("/api/playbook");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(Token, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null, FakeNotion.SampleRoot)]
    [InlineData(Token, null)]
    [InlineData(Token, "not-a-page-id")]
    public async Task Without_a_token_and_a_root_every_route_says_not_connected(string? token, string? root)
    {
        var notion = new FakeNotion();
        using var app = App(Settings(token, root), notion);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        foreach (var path in new[] { "/api/playbook", $"/api/playbook/pages/{FakeNotion.SampleRoot}", $"/api/playbook/files/{FakeNotion.SampleRoot}" })
        {
            var body = await owner.GetFromJsonAsync<JsonElement>(path);
            Assert.False(body.GetProperty("configured").GetBoolean());
        }

        Assert.Empty(notion.Requests);
    }

    [Fact]
    public async Task Without_sign_in_configured_the_page_is_told_so_and_nothing_else_is_mapped()
    {
        using var app = new TestApp(new Dictionary<string, string?>
        {
            ["ClientAddress:ForwardedHops"] = "0",
            ["Notion:Token"] = Token,
            ["Notion:PlaybookPageId"] = FakeNotion.SampleRoot
        });
        using var visitor = app.CreateClient();

        var body = await visitor.GetFromJsonAsync<JsonElement>("/api/playbook");
        var endpoints = app.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        Assert.False(body.GetProperty("configured").GetBoolean());
        Assert.DoesNotContain(endpoints, endpoint =>
            (endpoint as RouteEndpoint)?.RoutePattern.RawText?.StartsWith("/api/playbook/", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Outside_development_the_fake_flag_changes_nothing()
    {
        var settings = Settings(token: null, root: null);
        settings["Notion:Fake"] = "true";
        using var app = new TestApp(settings, services =>
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        var body = await owner.GetFromJsonAsync<JsonElement>("/api/playbook");

        Assert.False(body.GetProperty("configured").GetBoolean());
        Assert.Null(app.Factory.Services.GetService<FakeNotion>());
    }

    [Fact]
    public async Task In_development_the_fake_flag_serves_the_sample()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ClientAddress:ForwardedHops"] = "0",
            ["Admin:GoogleClientId"] = "test-client",
            ["Admin:GoogleClientSecret"] = "test-secret",
            ["Admin:AllowedEmails:0"] = AdminRouteTests.Owner,
            ["Notion:Token"] = "development-placeholder",
            ["Notion:PlaybookPageId"] = FakeNotion.SampleRoot,
            ["Notion:Fake"] = "true"
        };
        using var app = new TestApp(settings, services =>
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()), "Development");
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        var body = await owner.GetFromJsonAsync<JsonElement>("/api/playbook");

        Assert.Equal("Field notes", body.GetProperty("root").GetProperty("title").GetString());
        Assert.NotEmpty(app.Factory.Services.GetRequiredService<FakeNotion>().Requests);
    }

    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 40).Select(seed => new object[] { seed });

    /// <summary>
    /// Notion's answer is input. Blocks of every type with payloads of the
    /// wrong shape are drawn, refused or passed over, never a 500.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task A_page_of_malformed_blocks_never_answers_500(int seed)
    {
        var random = new Random(seed);
        var notion = new FakeNotion();
        var root = notion.Page("Root");
        string[] types =
        [
            "paragraph", "heading_1", "heading_3", "to_do", "callout", "code", "table", "table_row", "child_page",
            "child_database", "file", "pdf", "image", "bookmark", "column_list", "equation", "made_up"
        ];
        var parents = new List<(string Id, string Type)> { (root, "page") };
        for (var i = 0; i < 16; i++)
        {
            var parent = parents[random.Next(parents.Count)];
            // A table's children are its rows, as in Notion.
            var type = parent.Type == "table" ? "table_row" : types[random.Next(types.Length)];
            parents.Add((notion.Block(parent.Id, type, RandomObject(random)), type));
        }

        using var app = App(Settings(Token, root), notion, _time);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        foreach (var path in new[] { "/api/playbook", $"/api/playbook/pages/{root}" }
                     .Concat(parents.Select(parent => $"/api/playbook/files/{parent.Id}")))
        {
            using var response = await owner.GetAsync(path);
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadGateway,
                $"seed {seed}: {(int)response.StatusCode} for {path}");
        }
    }

    private static readonly string[] Names = ["rich_text", "title", "checked", "icon", "language", "type", "file", "external",
        "url", "name", "caption", "cells", "has_column_header", "expression", "emoji", "plain_text", "annotations", "href"];

    /// <summary>Every field a block payload may have, each present half the time with a value of any shape.</summary>
    private static JsonObject RandomObject(Random random)
    {
        var obj = new JsonObject();
        foreach (var name in Names)
        {
            if (random.Next(2) == 0) obj[name] = RandomPayload(random, 1);
        }

        return obj;
    }

    private static JsonNode? RandomPayload(Random random, int depth)
    {
        var names = Names;
        if (depth > 2 || random.Next(4) == 0)
        {
            return random.Next(6) switch
            {
                0 => null,
                1 => JsonValue.Create(random.Next(2) == 0),
                2 => JsonValue.Create(random.Next()),
                3 => JsonValue.Create("https://" + FakeNotion.FileHost + "/x?" + random.Next()),
                4 => JsonValue.Create("file"),
                _ => JsonValue.Create(new string('x', random.Next(0, 40)))
            };
        }

        if (random.Next(3) == 0)
        {
            var array = new JsonArray();
            for (var i = random.Next(0, 4); i > 0; i--) array.Add(RandomPayload(random, depth + 1));
            return array;
        }

        var obj = new JsonObject();
        for (var i = random.Next(0, 6); i > 0; i--) obj[names[random.Next(names.Length)]] = RandomPayload(random, depth + 1);
        return obj;
    }

    public void Dispose() => _app.Dispose();

    /// <summary>Every wait asked of it, ended at once, so a test of Retry-After takes no time.</summary>
    private sealed class RecordingTime : TimeProvider
    {
        public List<TimeSpan> Waits { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            // One-shot timers are waits. Periodic ones (the rate limiter's) pass.
            if (dueTime != Timeout.InfiniteTimeSpan && period == Timeout.InfiniteTimeSpan)
            {
                lock (Waits) Waits.Add(dueTime);
                dueTime = TimeSpan.FromMilliseconds(1);
            }

            return base.CreateTimer(callback, state, dueTime, period);
        }
    }
}
