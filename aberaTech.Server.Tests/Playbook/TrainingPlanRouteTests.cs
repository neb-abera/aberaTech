using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using aberaTech.Server.Playbook;
using aberaTech.Server.Tests.Admin;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Playbook;

/// <summary>
/// /training-plan from three chairs, against the sample plan in
/// <see cref="FakeNotion"/>, which pages every list two results at a time.
/// A visitor is told to sign in, a stranger is refused, and the owner reads
/// the plan's page, workouts and notes by column and downloads its files.
/// No file is served from outside the plan page and its notes database.
/// </summary>
public sealed class TrainingPlanRouteTests : IDisposable
{
    private const string Token = "notion-token-for-tests";

    private readonly FakeNotion _notion = FakeNotion.Sample();
    private readonly TestApp _app;

    public TrainingPlanRouteTests() => _app = App(Settings(), _notion);

    private static Dictionary<string, string?> Settings(string? token = Token, string? plan = FakeNotion.SamplePlan)
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["Notion:Token"] = token;
        settings["Notion:PlaybookPageId"] = FakeNotion.SampleRoot;
        settings["Notion:TrainingPlanPageId"] = plan;
        return settings;
    }

    private static TestApp App(Dictionary<string, string?> settings, FakeNotion notion) =>
        new(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddHttpClient(NotionClient.ApiClient).ConfigurePrimaryHttpMessageHandler(() => notion);
            services.AddHttpClient(NotionClient.FilesClient).ConfigurePrimaryHttpMessageHandler(() => notion);
        }, "Production");

    private HttpClient Owner() => _app.CreateClient().SignedInAs(_app.Factory.Services, AdminRouteTests.Owner);

    private async Task<JsonElement> PlanAsync(HttpClient owner)
    {
        var body = await owner.GetFromJsonAsync<JsonElement>("/api/training-plan");
        Assert.True(body.GetProperty("configured").GetBoolean());
        return body.GetProperty("plan");
    }

    public static IEnumerable<object[]> Routes =>
    [
        ["GET", "/api/training-plan"],
        ["GET", $"/api/training-plan/files/{FakeNotion.SamplePlan}"],
        ["GET", $"/api/training-plan/files/{FakeNotion.SamplePlan}:0"]
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
    public async Task The_owner_reads_the_page_its_files_and_every_row_by_column()
    {
        using var owner = Owner();

        var plan = await PlanAsync(owner);

        Assert.Equal(FakeNotion.SamplePlan, plan.GetProperty("id").GetString());
        Assert.Equal("Sample training plan", plan.GetProperty("title").GetString());
        var section = plan.GetProperty("sections").EnumerateArray().Single();
        Assert.Equal("About the plan", section.GetProperty("heading").GetString());
        Assert.Equal(["Two made-up weeks of base training."], section.GetProperty("paragraphs").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(["plan-guide.pdf"], plan.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("name").GetString()));

        // Five workouts across three pages of results, in date and day order.
        var workouts = plan.GetProperty("workouts").EnumerateArray().ToList();
        Assert.Equal(["Dynamic warmup", "Aerobic base run", "Push strength", "Rest day", "Heart rate drift test"],
            workouts.Select(w => w.GetProperty("title").GetString()));
        var run = workouts[1];
        Assert.Equal("2026-11-02", run.GetProperty("place").GetProperty("date").GetString());
        Assert.Equal(1, run.GetProperty("place").GetProperty("week").GetInt32());
        Assert.Equal(1, run.GetProperty("place").GetProperty("planDay").GetInt32());
        Assert.Equal("Mon", run.GetProperty("place").GetProperty("weekday").GetString());
        Assert.Equal("Block 1", run.GetProperty("place").GetProperty("block").GetString());
        Assert.Equal("Block 1 Week 1 Notes", run.GetProperty("place").GetProperty("weekLabel").GetString());
        Assert.Equal("Run", run.GetProperty("sport").GetString());
        Assert.Equal(45, run.GetProperty("minutes").GetDouble());
        Assert.Equal(33, run.GetProperty("tss").GetDouble());
        Assert.Contains("https://example.com/maf", run.GetProperty("description").GetString());
        Assert.Equal("Nose breathe the whole run.", run.GetProperty("comments").GetString());
        Assert.False(run.GetProperty("changed").GetBoolean());
        Assert.StartsWith("https://www.notion.so/", run.GetProperty("url").GetString());
        Assert.Equal("Warm up: 15:00 at 1-3 RPE\nActive: 1:00:00 at 3-4 RPE", workouts[4].GetProperty("structure").GetString());
        // A column a row leaves empty is absent, never zero.
        Assert.False(workouts[3].TryGetProperty("minutes", out _));

        var notes = plan.GetProperty("notes").EnumerateArray().ToList();
        Assert.Equal(["Intro to block one", "Week two recap"], notes.Select(n => n.GetProperty("title").GetString()));
        var file = notes[0].GetProperty("files").EnumerateArray().Single();
        Assert.Equal("intro.pdf", file.GetProperty("name").GetString());
        Assert.Equal($"{notes[0].GetProperty("id").GetString()}:0", file.GetProperty("id").GetString());
        // A note with no Block column set reads without one.
        Assert.False(notes[1].GetProperty("place").TryGetProperty("block", out _));
    }

    [Fact]
    public async Task The_plan_is_read_from_its_columns_without_opening_a_single_row()
    {
        using var owner = Owner();

        var plan = await PlanAsync(owner);

        var rows = plan.GetProperty("workouts").EnumerateArray().Concat(plan.GetProperty("notes").EnumerateArray())
            .Select(row => row.GetProperty("id").GetString()!).ToList();
        var requests = _notion.Requests.Select(seen => seen.Request).ToList();
        Assert.DoesNotContain(requests, request => rows.Any(row => request.Contains(row, StringComparison.Ordinal)));
        // The page, its blocks (three lists of two), two databases, and the
        // queries (three pages of workouts, one of notes).
        Assert.Equal(1 + 3 + 2 + 3 + 1, requests.Count);

        // A second read within the cache window asks Notion nothing.
        await PlanAsync(owner);
        Assert.Equal(requests.Count, _notion.Requests.Count);
    }

    [Fact]
    public async Task The_plan_file_and_a_note_attachment_download_with_their_names()
    {
        using var owner = Owner();
        var plan = await PlanAsync(owner);
        var planFile = plan.GetProperty("files")[0].GetProperty("id").GetString();
        var noteFile = plan.GetProperty("notes")[0].GetProperty("files")[0].GetProperty("id").GetString();

        foreach (var (id, name, start) in new[] { (planFile, "plan-guide.pdf", "%PDF-1.4\n%guide"), (noteFile, "intro.pdf", "%PDF-1.4\n%intro") })
        {
            using var response = await owner.GetAsync($"/api/training-plan/files/{Uri.EscapeDataString(id!)}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(name, response.Content.Headers.ContentDisposition?.FileName);
            Assert.StartsWith(start, await response.Content.ReadAsStringAsync());
        }

        // Each address was asked fresh, and the signed address never got the token.
        var noteRow = noteFile!.Split(':')[0];
        Assert.Contains(_notion.Requests, seen => seen.Request == $"GET /v1/blocks/{planFile}");
        Assert.Contains(_notion.Requests, seen => seen.Request == $"GET /v1/pages/{noteRow}");
        Assert.All(_notion.Requests.Where(seen => seen.Request.StartsWith("GET /secure/", StringComparison.Ordinal)),
            seen => Assert.Null(seen.Authorization));
    }

    [Fact]
    public async Task A_file_outside_the_plan_is_not_found_and_never_fetched()
    {
        using var owner = Owner();
        var plan = await PlanAsync(owner);
        var workout = plan.GetProperty("workouts")[0].GetProperty("id").GetString();
        // A file block on another page the token reaches.
        var elsewhere = _notion.Page("Somewhere else");
        var outsideBlock = _notion.File(elsewhere, "file", "other.txt", "text/plain", "not yours"u8.ToArray());
        // A row of another database, with a file in a column named Attachments.
        var (database, source) = _notion.Database(elsewhere, TrainingPlanReader.NotesTitle);
        var outsideRow = _notion.Row(database, source, "Not the plan's", new JsonObject
        {
            ["Attachments"] = FakeNotion.Column.Files(_notion.FileEntry("other.pdf", "application/pdf", "%PDF"u8.ToArray()))
        });
        var paragraph = _notion.Text(FakeNotion.SamplePlan, "paragraph", FakeNotion.Run("Just text"));
        var note = plan.GetProperty("notes")[0].GetProperty("id").GetString();

        foreach (var id in new[] { outsideBlock, $"{outsideRow}:0", $"{workout}:0", $"{note}:1", $"{note}:-1", paragraph, "not-an-id", $"{note}:0:0" })
        {
            using var response = await owner.GetAsync($"/api/training-plan/files/{Uri.EscapeDataString(id)}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.DoesNotContain(_notion.Requests, seen => seen.Request.StartsWith("GET /secure/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_external_link_in_attachments_is_never_fetched()
    {
        using var owner = Owner();
        var plan = await PlanAsync(owner);
        var note = plan.GetProperty("notes")[1].GetProperty("id").GetString()!;
        _notion.SetProperty(note, "Attachments", new JsonObject
        {
            ["type"] = "files",
            ["files"] = new JsonArray(new JsonObject
            {
                ["name"] = "link.pdf",
                ["type"] = "external",
                ["external"] = new JsonObject { ["url"] = "https://attacker.example/link.pdf" }
            })
        });

        using var response = await owner.GetAsync($"/api/training-plan/files/{note}:0");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(_notion.Requests, seen => seen.Request.Contains("attacker", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, FakeNotion.SamplePlan)]
    [InlineData(Token, null)]
    [InlineData(Token, "not-a-page-id")]
    public async Task Without_a_token_and_a_plan_page_every_route_says_not_connected(string? token, string? plan)
    {
        var notion = new FakeNotion();
        using var app = App(Settings(token, plan), notion);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        foreach (var path in new[] { "/api/training-plan", $"/api/training-plan/files/{FakeNotion.SamplePlan}" })
        {
            var body = await owner.GetFromJsonAsync<JsonElement>(path);
            Assert.False(body.GetProperty("configured").GetBoolean());
            Assert.Contains("Notion__TrainingPlanPageId", body.GetProperty("message").GetString());
        }

        Assert.Empty(notion.Requests);
    }

    [Fact]
    public async Task Notion_refusing_is_a_502_naming_only_the_status()
    {
        var notion = new FakeNotion();
        using var app = App(Settings(plan: "0000dead-0000-0000-0000-000000000000"), notion);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        using var response = await owner.GetAsync("/api/training-plan");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Notion answered 404", await response.Content.ReadAsStringAsync());
    }

    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 40).Select(seed => new object[] { seed });

    private static readonly string[] Columns = ["Name", "Date", "Week", "Plan day", "Weekday", "Block", "Week label", "Sport",
        "Order on day", "Duration (min)", "Distance (km)", "TSS", "IF", "Description", "Pre-activity comments", "Structure",
        "Changed", "Attachments"];

    private static readonly string[] Keys = ["type", "title", "rich_text", "number", "select", "date", "start", "name",
        "checkbox", "files", "file", "external", "url", "plain_text", "text", "content"];

    /// <summary>
    /// Notion's answer is input, and the owner can retype any column. Rows
    /// whose every column holds a value of any shape read as 200, and every
    /// file id they yield answers 200, 404 or 502, never a 500.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task Rows_with_malformed_columns_never_answer_500(int seed)
    {
        var random = new Random(seed);
        var notion = FakeNotion.Sample();
        using var first = Owner();
        var plan = await PlanAsync(first);
        var rows = plan.GetProperty("workouts").EnumerateArray().Concat(plan.GetProperty("notes").EnumerateArray())
            .Select(row => row.GetProperty("id").GetString()!).ToList();
        foreach (var row in rows)
        {
            foreach (var column in Columns)
            {
                if (random.Next(2) == 0) notion.SetProperty(row, column, (JsonObject)RandomValue(random, 0, forceObject: true)!);
            }
        }

        using var app = App(Settings(), notion);
        using var owner = app.CreateClient().SignedInAs(app.Factory.Services, AdminRouteTests.Owner);

        using var read = await owner.GetAsync("/api/training-plan");
        Assert.True(read.StatusCode == HttpStatusCode.OK, $"seed {seed}: {(int)read.StatusCode}");
        foreach (var id in rows.SelectMany(row => new[] { $"{row}:0", $"{row}:1" }))
        {
            using var response = await owner.GetAsync($"/api/training-plan/files/{id}");
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadGateway,
                $"seed {seed}: {(int)response.StatusCode} for {id}");
        }
    }

    private static JsonNode? RandomValue(Random random, int depth, bool forceObject = false)
    {
        if (!forceObject && (depth > 2 || random.Next(4) == 0))
        {
            return random.Next(6) switch
            {
                0 => null,
                1 => JsonValue.Create(random.Next(2) == 0),
                2 => JsonValue.Create(random.NextDouble() * 1e9 - 5e8),
                3 => JsonValue.Create("https://" + FakeNotion.FileHost + "/x?" + random.Next()),
                4 => JsonValue.Create("2026-11-0" + random.Next(10)),
                _ => JsonValue.Create(new string('x', random.Next(0, 40)))
            };
        }

        if (!forceObject && random.Next(3) == 0)
        {
            var array = new JsonArray();
            for (var i = random.Next(0, 4); i > 0; i--) array.Add(RandomValue(random, depth + 1));
            return array;
        }

        var obj = new JsonObject();
        for (var i = random.Next(0, 6); i > 0; i--) obj[Keys[random.Next(Keys.Length)]] = RandomValue(random, depth + 1);
        return obj;
    }

    public void Dispose() => _app.Dispose();
}
