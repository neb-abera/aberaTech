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
    public async Task Training_push_ups_never_stand_for_a_test_and_a_training_pull_up_to_standard_counts()
    {
        using var app = App();
        await SeedAsync(app);
        await app.SeedAsync(database =>
        {
            var session = new Activity
            {
                Id = Guid.NewGuid(),
                Source = "hevy",
                Sport = "strength",
                Name = "Upper",
                StartedAt = FitnessApp.Now - Duration.FromDays(1),
                DurationSeconds = 1800
            };
            database.Activities.Add(session);
            // Metronome push-ups to failure, then on the knees: training, not the test.
            database.StrengthSets.Add(new StrengthSet { Id = Guid.NewGuid(), ActivityId = session.Id, Exercise = "Push Up", SetIndex = 0, WeightKg = 0, Reps = 12 });
            database.StrengthSets.Add(new StrengthSet { Id = Guid.NewGuid(), ActivityId = session.Id, Exercise = "Pull Up", SetIndex = 1, WeightKg = 0, Reps = 13 });
            return Task.CompletedTask;
        });
        var goals = await GoalsAsync(app);

        var fbi = goals.GetProperty("sections").EnumerateArray().Single(s => s.GetProperty("test").GetString() == "fbi");
        var rows = fbi.GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("label").GetString()!);

        Assert.Equal(JsonValueKind.Null, rows["Push-ups"].GetProperty("current").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows["Push-ups"].GetProperty("pointsNow").ValueKind);

        var pullUps = rows["Pull-ups"].GetProperty("current");
        Assert.Equal("Measured", pullUps.GetProperty("basis").GetString());
        Assert.True(pullUps.GetProperty("value").GetDouble() >= 13);
    }

    [Fact]
    public async Task The_goals_tab_sets_the_running_hours_alone()
    {
        using var app = App();
        await SeedAsync(app);
        var client = app.Factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/fitness/settings/available-hours", new { hours = 8.5 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var goals = await GoalsAsync(app);
        Assert.Equal(8.5, goals.GetProperty("weeklyHours").GetDouble());

        var refused = await client.PutAsJsonAsync("/api/fitness/settings/available-hours", new { hours = 41 });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task An_fbi_test_sets_the_fbi_push_ups_and_scores_the_events_run()
    {
        using var app = App();
        await SeedAsync(app);
        var client = app.Factory.CreateClient();

        var saved = await client.PostAsJsonAsync("/api/fitness/fbi",
            new { date = "2026-06-10", pullUps = (int?)null, sprint300mSeconds = 44.1, pushUps = 52, run1_5MileSeconds = (double?)null });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var result = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("eventsRun").GetInt32());
        Assert.Equal(5, result.GetProperty("pushUpPoints").GetInt32());

        var goals = await GoalsAsync(app);
        var fbi = goals.GetProperty("sections").EnumerateArray().Single(s => s.GetProperty("test").GetString() == "fbi");
        var pushUps = fbi.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("label").GetString() == "Push-ups");
        Assert.Equal(52, pushUps.GetProperty("current").GetProperty("value").GetDouble());
        Assert.Equal("Measured", pushUps.GetProperty("current").GetProperty("basis").GetString());
        Assert.Equal(5, pushUps.GetProperty("pointsNow").GetInt32());

        var empty = await client.PostAsJsonAsync("/api/fitness/fbi", new { date = "2026-06-11" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task A_fast_stretch_in_training_counts_toward_a_run_goal_and_a_slow_one_does_not()
    {
        using var app = App();
        await SeedAsync(app);

        // An easy run whose fastest 2 miles is slower than the reading already held.
        await app.SeedAsync(database =>
        {
            database.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "Easy",
                StartedAt = FitnessApp.Now - Duration.FromDays(4), DurationSeconds = 3600,
                DistanceMeters = 9000, Best2MileSeconds = 20 * 60, RecordsVersion = 2
            });
            return Task.CompletedTask;
        });
        var before = await GoalsAsync(app);
        string Basis(JsonElement goals) => goals.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("rows").EnumerateArray())
            .Single(r => r.GetProperty("label").GetString() == "2 mile run").GetProperty("current").GetProperty("evidence").GetString()!;
        Assert.DoesNotContain("inside a training run", Basis(before));

        // A track session with a 2-mile stretch at 12:00 beats it.
        await app.SeedAsync(database =>
        {
            database.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(), Source = "test", Sport = "run", Name = "Track",
                StartedAt = FitnessApp.Now - Duration.FromDays(2), DurationSeconds = 2400,
                DistanceMeters = 8000, Best2MileSeconds = 12 * 60, RecordsVersion = 2
            });
            return Task.CompletedTask;
        });
        var after = await GoalsAsync(app);
        Assert.Contains("fastest 2 mi inside a training run", Basis(after));
    }

    [Fact]
    public async Task Each_event_shows_every_published_standard_with_its_source_and_level()
    {
        using var app = App();
        await SeedAsync(app);
        var goals = await GoalsAsync(app);

        var rows = goals.GetProperty("sections").EnumerateArray()
            .SelectMany(s => s.GetProperty("rows").EnumerateArray())
            .ToDictionary(r => r.GetProperty("label").GetString()!);

        // The pull-up goal is Evoke's 15; the FBI's 10-point row is 20 for men.
        var pullUps = Assert.Single(rows["Pull-ups"].GetProperty("standards").EnumerateArray());
        Assert.Equal("FBI", pullUps.GetProperty("source").GetString());
        Assert.Equal("max (10 pts)", pullUps.GetProperty("level").GetString());
        Assert.Equal(20, pullUps.GetProperty("value").GetDouble());

        // An AFT event carries the Army's 100-point standard for the due-date age band.
        var pushUps = Assert.Single(rows["HR pushups"].GetProperty("standards").EnumerateArray());
        Assert.Equal("AFT", pushUps.GetProperty("source").GetString());
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
