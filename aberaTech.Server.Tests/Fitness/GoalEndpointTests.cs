using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The Goals tab's data, end to end: the workbook read from a file the way
/// production reads it from OneDrive, scored against the log.
/// </summary>
public sealed class GoalEndpointTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("goals-workbook").FullName;

    private string WorkbookPath => Path.Combine(_directory, "physical fitness goals.xlsx");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private FitnessApp App(bool withWorkbook = true)
    {
        if (withWorkbook) File.WriteAllBytes(WorkbookPath, WorkbookFixture.Build());
        return new FitnessApp(builder => builder.UseSetting("OneDrive:LocalWorkbookPath", WorkbookPath));
    }

    private static async Task SeedAsync(FitnessApp app)
    {
        await app.SeedAsync(FitnessSeed.AthleteAsync);
        await app.SeedAsync(database =>
        {
            database.AftResults.Add(new AftResult
            {
                Id = Guid.NewGuid(),
                Date = new LocalDate(2026, 4, 3),
                DeadliftKg = 300 / BodyMass.PoundsPerKg,
                HandReleasePushUps = 44,
                SprintDragCarrySeconds = 104,
                PlankSeconds = 120,
                TwoMileSeconds = 16 * 60 + 49
            });

            // A lift the workbook does not track: it must not surface.
            var session = new Activity
            {
                Id = Guid.NewGuid(),
                Source = "hevy",
                Sport = "strength",
                Name = "Arms",
                StartedAt = FitnessApp.Now - Duration.FromDays(3),
                DurationSeconds = 1800
            };
            database.Activities.Add(session);
            database.StrengthSets.Add(new StrengthSet { Id = Guid.NewGuid(), ActivityId = session.Id, Exercise = "Concentration Curl", SetIndex = 0, WeightKg = 9, Reps = 10 });
            return Task.CompletedTask;
        });
    }

    private static async Task<JsonElement> GoalsAsync(FitnessApp app)
    {
        var client = app.Factory.CreateClient();
        var response = await client.GetAsync("/api/fitness/goals", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sections_follow_the_workbook_with_strength_split_out()
    {
        using var app = App();
        await SeedAsync(app);
        var goals = await GoalsAsync(app);

        Assert.Equal("file", goals.GetProperty("source").GetProperty("origin").GetString());
        Assert.Equal("2027-12-31", goals.GetProperty("dueOn").GetString());

        var names = goals.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToArray();
        Assert.Equal(["Run calculator", "Army Fitness Test", "FBI Physical Fitness Test", "Body composition", "Strength"], names);

        var rows = goals.GetProperty("sections").EnumerateArray().Sum(s => s.GetProperty("rows").GetArrayLength());
        Assert.Equal(19, rows);
    }

    [Fact]
    public async Task The_aft_is_scored_on_its_tables_now_and_at_the_goals()
    {
        using var app = App();

        // The card alone, so today's total is the card's: 91 + 87 + 93 + 74 + 83.
        await app.SeedAsync(database =>
        {
            database.Settings.Add(new AthleteSettings { Id = 1, BirthYear = 1992, Female = false, StartVdot = 35.4, ReferenceHr = 152 });
            database.AftResults.Add(new AftResult
            {
                Id = Guid.NewGuid(),
                Date = new LocalDate(2026, 4, 3),
                DeadliftKg = 300 / BodyMass.PoundsPerKg,
                HandReleasePushUps = 44,
                SprintDragCarrySeconds = 104,
                PlankSeconds = 120,
                TwoMileSeconds = 16 * 60 + 49
            });
            return Task.CompletedTask;
        });
        var goals = await GoalsAsync(app);

        var aft = goals.GetProperty("sections").EnumerateArray().Single(s => s.GetProperty("test").GetString() == "aft");
        Assert.Equal(428, aft.GetProperty("pointsNow").GetInt32());
        Assert.Equal(500, aft.GetProperty("pointsGoal").GetInt32());
        Assert.Equal(500, aft.GetProperty("pointsMax").GetInt32());

        var labels = aft.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("label").GetString()).ToArray();
        Assert.Contains("HR pushups", labels);
        Assert.DoesNotContain("Push-ups", labels);
    }

    [Fact]
    public async Task The_fbi_push_ups_are_their_own_event_with_their_own_points()
    {
        using var app = App();
        await SeedAsync(app);
        var goals = await GoalsAsync(app);

        var fbi = goals.GetProperty("sections").EnumerateArray().Single(s => s.GetProperty("test").GetString() == "fbi");
        var rows = fbi.GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("label").GetString()!);

        Assert.Equal(["Pull-ups", "300m sprint", "Push-ups", "1.5m run"], rows.Keys);
        Assert.Equal(10, rows["Push-ups"].GetProperty("pointsGoal").GetInt32());
        Assert.Equal(7, rows["Pull-ups"].GetProperty("pointsGoal").GetInt32());
        Assert.Equal(10, rows["300m sprint"].GetProperty("pointsGoal").GetInt32());
        Assert.Equal(10, rows["1.5m run"].GetProperty("pointsGoal").GetInt32());
        Assert.Equal(37, fbi.GetProperty("pointsGoal").GetInt32());
    }

    [Fact]
    public async Task A_lift_the_workbook_does_not_name_appears_nowhere()
    {
        using var app = App();
        await SeedAsync(app);
        var goals = await GoalsAsync(app);

        Assert.DoesNotContain("Curl", goals.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_mapped_row_carries_a_forecast_for_the_due_date()
    {
        using var app = App();
        await SeedAsync(app);
        var goals = await GoalsAsync(app);

        var rows = goals.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("rows").EnumerateArray()).ToArray();
        Assert.All(rows, r => Assert.NotEqual(JsonValueKind.Null, r.GetProperty("forecast").ValueKind));

        var twoMile = rows.Single(r => r.GetProperty("label").GetString() == "2 mile run");
        Assert.Equal("trajectory", twoMile.GetProperty("forecast").GetProperty("method").GetString());
        Assert.Equal(735, twoMile.GetProperty("goal").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task A_missing_workbook_says_why_and_shows_no_invented_goals()
    {
        using var app = App(withWorkbook: false);
        await SeedAsync(app);
        var goals = await GoalsAsync(app);

        Assert.False(string.IsNullOrWhiteSpace(goals.GetProperty("source").GetProperty("error").GetString()));
        Assert.Equal(0, goals.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task Out_of_range_weeks_are_refused()
    {
        using var app = App();
        await SeedAsync(app);
        var response = await app.Factory.CreateClient().GetAsync("/api/fitness/goals?weeklyHours=99", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// Microsoft's redirect back to the callback carries no owner cookie: the
/// cookie is SameSite=Strict and the navigation is cross-site. On 2026-11-03
/// the callback answered 401 there, and iPhone Safari offered the empty body
/// as a download.
/// </summary>
public sealed class OneDriveCallbackTests
{
    private static FitnessApp OwnerOnlyApp() => new(builder =>
    {
        builder.UseSetting("Fitness:DevelopmentOwner", "false");
        builder.UseSetting("Fitness:AllowedEmails:0", "owner@example.com");
        builder.UseSetting("Admin:GoogleClientId", "test-client");
        builder.UseSetting("Admin:GoogleClientSecret", "test-secret");
        builder.UseSetting("Admin:AllowedEmails:0", "owner@example.com");
        builder.UseSetting("OneDrive:ClientId", "client");
        builder.UseSetting("OneDrive:ClientSecret", "secret");
    });

    private static HttpClient NoRedirects(FitnessApp app) =>
        app.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task The_connect_route_stays_the_owners()
    {
        using var app = OwnerOnlyApp();
        var response = await NoRedirects(app).GetAsync(new Uri("/api/fitness/onedrive/connect", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/fitness/onedrive/callback?code=abc&state=forged")]
    [InlineData("/api/fitness/onedrive/callback?error=access_denied")]
    public async Task Without_the_owner_cookie_the_callback_answers_with_a_redirect_to_the_page(string path)
    {
        using var app = OwnerOnlyApp();
        var response = await NoRedirects(app).GetAsync(new Uri(path, UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/fitness?onedrive=refused", response.Headers.Location?.OriginalString);
    }
}
