using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using aberaTech.Fitness.Data;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Every fitness route, from the entry point inward, in production's shape:
/// Google sign-in configured and one address on the allowlist. A visitor with
/// no session is told to sign in, a visitor with a Google account that is not
/// the athlete's is refused, and the athlete gets the route's real answer.
/// </summary>
/// <remarks>
/// Health data, so the refusals are the point: the id in a route is never
/// enough, and neither is being signed in. The happy paths are here too, so
/// that a route cannot go dark — or start answering 500 — without a test
/// noticing, and so that every route in the table has at least one caller.
/// </remarks>
public sealed class FitnessRouteAuthorizationTests : IAsyncLifetime
{
    private const string Owner = "owner@example.com";
    private const string Stranger = "someone.else@example.com";

    private static readonly Guid Ruck = FitnessSeed.Id(1, 5);
    private static readonly Guid Run = FitnessSeed.Id(1, 7);
    private static readonly Guid Lift = FitnessSeed.Id(1, 1);
    private static readonly Guid OldAft = FitnessSeed.Id(8, 1);
    private static readonly Guid ScoredPrediction = FitnessSeed.Id(9, 1);
    private static readonly Guid PendingPrediction = FitnessSeed.Id(9, 2);

    private readonly FitnessApp _app = new(SignInRequired);

    public async ValueTask InitializeAsync() => await _app.SeedAsync(FitnessSeed.AthleteAsync);

    public ValueTask DisposeAsync()
    {
        _app.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Every route the fitness surface maps, with a real id where one is taken.</summary>
    public static IEnumerable<object[]> Routes =>
    [
        ["GET", "/api/fitness/summary"],
        ["GET", "/api/fitness/digest"],
        ["GET", "/api/fitness/citations"],
        ["GET", "/api/fitness/readiness/outlook?weeklyHours=6"],
        ["GET", "/api/fitness/predictions?weeklyHours=6&compliance=0.9"],
        ["GET", "/api/fitness/predictions/goal?distanceMeters=3218.688&targetSeconds=840&monthsAvailable=6"],
        ["POST", "/api/fitness/import"],
        ["GET", "/api/fitness/activities"],
        ["PUT", $"/api/fitness/activities/{FitnessSeed.Id(1, 5)}/load"],
        ["DELETE", $"/api/fitness/activities/{FitnessSeed.Id(1, 1)}"],
        ["POST", "/api/fitness/aft"],
        ["DELETE", $"/api/fitness/aft/{FitnessSeed.Id(8, 1)}"],
        ["PUT", "/api/fitness/settings"],
        ["POST", "/api/fitness/body-metrics"],
        ["PUT", "/api/fitness/goals"],
        ["DELETE", "/api/fitness/goals/two-mile"],
        ["GET", "/api/fitness/ingest"],
        ["POST", "/api/fitness/ingest/hevy/sync"],
        ["POST", "/api/fitness/ingest/intervals-icu/sync"],
        ["GET", "/api/fitness/model"],
        ["POST", "/api/fitness/solve"],
        ["POST", "/api/fitness/surface"],
        ["POST", "/api/fitness/measure"],
        ["GET", "/api/fitness/predictions/locked"],
        ["POST", "/api/fitness/predictions/locked"],
        ["POST", $"/api/fitness/predictions/locked/{FitnessSeed.Id(9, 2)}/actual"],
        ["DELETE", $"/api/fitness/predictions/locked/{FitnessSeed.Id(9, 1)}"],
        ["GET", "/api/progress/plan"],
        ["PUT", "/api/progress/plan"]
    ];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_visitor_with_no_session_is_told_to_sign_in(string method, string path)
    {
        using var visitor = _app.Factory.CreateClient();

        using var response = await Send(visitor, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_google_account_that_is_not_the_athlete_is_refused(string method, string path)
    {
        using var stranger = Stranger_();

        using var response = await Send(stranger, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_identity_route_is_the_one_a_visitor_may_read()
    {
        using var visitor = _app.Factory.CreateClient();

        var me = await visitor.GetFromJsonAsync<JsonElement>("/api/fitness/me");

        Assert.True(me.GetProperty("configured").GetBoolean());
        Assert.False(me.GetProperty("signedIn").GetBoolean());

        using var owner = Owner_();
        var self = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/me");
        Assert.True(self.GetProperty("signedIn").GetBoolean());
    }

    // ------------------------------------------------------------ user B and user A's objects

    [Fact]
    public async Task A_stranger_cannot_delete_the_athletes_activity()
    {
        using var stranger = Stranger_();
        using var owner = Owner_();
        var before = await ActivityCountAsync(owner);

        using var refused = await stranger.DeleteAsync($"/api/fitness/activities/{Lift}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(before, await ActivityCountAsync(owner));
    }

    [Fact]
    public async Task A_stranger_cannot_write_a_load_onto_the_athletes_ruck()
    {
        using var stranger = Stranger_();
        using var owner = Owner_();

        using var refused = await stranger.PutAsJsonAsync($"/api/fitness/activities/{Ruck}/load", new { loadKg = 20 });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Null((await ActivityAsync(owner, Ruck)).GetProperty("loadKg").Deserialize<double?>());
    }

    [Fact]
    public async Task A_stranger_cannot_delete_the_athletes_test_result()
    {
        using var stranger = Stranger_();

        using var refused = await stranger.DeleteAsync($"/api/fitness/aft/{OldAft}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(2, await CountAsync(database => database.AftResults.CountAsync()));
    }

    [Fact]
    public async Task A_stranger_can_neither_score_nor_delete_the_athletes_prediction()
    {
        using var stranger = Stranger_();
        using var owner = Owner_();

        using var scored = await stranger.PostAsJsonAsync(
            $"/api/fitness/predictions/locked/{PendingPrediction}/actual", new { actualSeconds = 2390 });
        using var deleted = await stranger.DeleteAsync($"/api/fitness/predictions/locked/{ScoredPrediction}");

        Assert.Equal(HttpStatusCode.Forbidden, scored.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);

        var ledger = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/predictions/locked");
        Assert.Equal(2, ledger.GetArrayLength());
        // Still unanswered: the ledger dates itself by the real clock, so the
        // seeded prediction reads as due rather than pending, but never scored.
        Assert.NotEqual("scored", Prediction(ledger, PendingPrediction).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, Prediction(ledger, PendingPrediction).GetProperty("actualSeconds").ValueKind);
    }

    [Fact]
    public async Task A_stranger_cannot_read_a_document_the_athlete_saved()
    {
        using var owner = Owner_();
        using var stranger = Stranger_();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync("/api/progress/plan", Json("{\"notes\":\"mine\"}"))).StatusCode);

        using var refused = await stranger.GetAsync("/api/progress/plan");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.DoesNotContain("mine", await refused.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------ the athlete's own answers

    [Fact]
    public async Task Citations_list_every_model_with_its_source()
    {
        using var owner = Owner_();

        var citations = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/citations");

        Assert.True(citations.GetArrayLength() >= 8);
        Assert.All(citations.EnumerateArray(), citation => Assert.False(string.IsNullOrWhiteSpace(citation.GetProperty("work").GetString())));
    }

    [Fact]
    public async Task Predictions_answer_a_named_week_and_refuse_an_impossible_one()
    {
        using var owner = Owner_();

        using var week = await owner.GetAsync("/api/fitness/predictions?weeklyHours=6&compliance=0.9");
        using var zones = await owner.GetAsync("/api/fitness/predictions?easyHours=4&thresholdHours=1&compliance=0.9");
        using var impossible = await owner.GetAsync("/api/fitness/predictions?weeklyHours=6&compliance=2");
        using var unnamed = await owner.GetAsync("/api/fitness/predictions?compliance=0.9");

        Assert.Equal(HttpStatusCode.OK, week.StatusCode);
        Assert.Equal(HttpStatusCode.OK, zones.StatusCode);
        Assert.Equal("application/json", week.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, impossible.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
    }

    [Fact]
    public async Task A_goal_is_answered_over_any_distance_and_refused_out_of_range()
    {
        using var owner = Owner_();

        using var goal = await owner.GetAsync("/api/fitness/predictions/goal?distanceMeters=3218.688&targetSeconds=840&monthsAvailable=6");
        using var noTime = await owner.GetAsync("/api/fitness/predictions/goal?distanceMeters=3218.688&targetSeconds=840&monthsAvailable=0");

        Assert.Equal(HttpStatusCode.OK, goal.StatusCode);
        Assert.Contains("verdict", await goal.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noTime.StatusCode);
    }

    [Fact]
    public async Task Activities_are_the_newest_fifty_and_say_how_many_there_are()
    {
        using var owner = Owner_();

        var page = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/activities");

        Assert.Equal(50, page.GetProperty("limit").GetInt32());
        Assert.Equal(50, page.GetProperty("activities").GetArrayLength());
        Assert.True(page.GetProperty("total").GetInt32() > 50);
    }

    [Fact]
    public async Task A_load_is_written_onto_a_ruck_and_only_a_ruck()
    {
        using var owner = Owner_();

        using var written = await owner.PutAsJsonAsync($"/api/fitness/activities/{Ruck}/load", new { loadKg = 20 });
        using var onARun = await owner.PutAsJsonAsync($"/api/fitness/activities/{Run}/load", new { loadKg = 20 });
        using var tooHeavy = await owner.PutAsJsonAsync($"/api/fitness/activities/{Ruck}/load", new { loadKg = 500 });
        using var unknown = await owner.PutAsJsonAsync($"/api/fitness/activities/{Guid.NewGuid()}/load", new { loadKg = 20 });

        Assert.Equal(HttpStatusCode.NoContent, written.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, onARun.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooHeavy.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(20, (await ActivityAsync(owner, Ruck)).GetProperty("loadKg").GetDouble());
    }

    [Fact]
    public async Task A_fitness_test_is_stored_and_scored_and_can_be_taken_back()
    {
        using var owner = Owner_();
        var result = new
        {
            date = "2026-06-01",
            deadliftKg = 140,
            handReleasePushUps = 40,
            sprintDragCarrySeconds = 100,
            plankSeconds = 180,
            twoMileSeconds = 900
        };

        using var scored = await owner.PostAsJsonAsync("/api/fitness/aft", result);
        using var undated = await owner.PostAsJsonAsync("/api/fitness/aft", new { deadliftKg = 140 });

        Assert.Equal(HttpStatusCode.OK, scored.StatusCode);
        Assert.Contains("events", await scored.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, undated.StatusCode);
        Assert.Equal(3, await CountAsync(database => database.AftResults.CountAsync()));

        using var deleted = await owner.DeleteAsync($"/api/fitness/aft/{OldAft}");
        using var gone = await owner.DeleteAsync($"/api/fitness/aft/{OldAft}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(2, await CountAsync(database => database.AftResults.CountAsync()));
    }

    [Fact]
    public async Task A_bad_import_is_undone_from_the_page()
    {
        using var owner = Owner_();
        var before = await ActivityCountAsync(owner);

        using var deleted = await owner.DeleteAsync($"/api/fitness/activities/{Lift}");
        using var gone = await owner.DeleteAsync($"/api/fitness/activities/{Lift}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(before - 1, await ActivityCountAsync(owner));
        Assert.Equal(0, await CountAsync(database => database.Set<StrengthSet>().CountAsync(set => set.ActivityId == Lift)));
    }

    [Fact]
    public async Task Settings_are_saved_and_a_heart_rate_nobody_has_is_refused()
    {
        using var owner = Owner_();

        using var saved = await owner.PutAsJsonAsync("/api/fitness/settings", new
        {
            referenceHr = 150, ltHr = 172, planMinutesPerWeek = 240, startVdot = 45, availableHoursPerWeek = 8
        });
        using var refused = await owner.PutAsJsonAsync("/api/fitness/settings", new
        {
            referenceHr = 30, planMinutesPerWeek = 240, startVdot = 45
        });

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        using var scope = _app.Factory.Services.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<FitnessDbContext>().Settings.SingleAsync();
        Assert.Equal(45, settings.StartVdot);
        Assert.Equal(8, settings.AvailableHoursPerWeek);
    }

    [Fact]
    public async Task A_goal_is_set_replaced_and_removed_by_its_metric()
    {
        using var owner = Owner_();

        using var set = await owner.PutAsJsonAsync("/api/fitness/goals", new { metric = "two-mile", targetValue = 840, targetDate = "2027-03-01", distanceMeters = 3218.688 });
        using var moved = await owner.PutAsJsonAsync("/api/fitness/goals", new { metric = "two-mile", targetValue = 820, targetDate = "2027-04-01" });
        using var nameless = await owner.PutAsJsonAsync("/api/fitness/goals", new { metric = "", targetValue = 820, targetDate = "2027-04-01" });

        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);
        Assert.Equal(1, await CountAsync(database => database.Goals.CountAsync()));

        using var removed = await owner.DeleteAsync("/api/fitness/goals/two-mile");
        using var gone = await owner.DeleteAsync("/api/fitness/goals/two-mile");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task The_sources_report_their_standing_and_each_can_be_run_by_hand()
    {
        using var owner = Owner_();

        var status = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/ingest");
        Assert.True(status.GetProperty("hevy").GetProperty("configured").GetBoolean());
        Assert.True(status.GetProperty("intervalsIcu").GetProperty("configured").GetBoolean());

        // Both bridges answer from the stub below: an empty account, so a run
        // fetches nothing and adds nothing, and says so with a 200.
        var hevy = await owner.PostAsync("/api/fitness/ingest/hevy/sync", null);
        var icu = await owner.PostAsync("/api/fitness/ingest/intervals-icu/sync", null);

        Assert.Equal(HttpStatusCode.OK, hevy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, icu.StatusCode);
        Assert.Equal(0, (await hevy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("added").GetInt32());
        Assert.Equal(0, (await icu.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("added").GetInt32());

        var after = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/ingest");
        Assert.NotNull(after.GetProperty("hevy").GetProperty("lastRunAt").GetString());
    }

    [Fact]
    public async Task The_solver_predicts_solves_sweeps_and_plans_a_measurement()
    {
        using var owner = Owner_();
        var scenario = new { distanceMeters = 8046.72, months = 12, weeklyHours = 6, compliance = 0.85 };

        var model = await owner.GetAsync("/api/fitness/model");
        var predicted = await owner.PostAsJsonAsync("/api/fitness/solve", new { scenario });
        var solved = await owner.PostAsJsonAsync("/api/fitness/solve", new { scenario, solveFor = "weeklyHours", targetSeconds = 2000 });
        var surface = await owner.PostAsJsonAsync("/api/fitness/surface", new { scenario, across = "WeeklyHours", down = "Months", resolution = 8 });
        var measure = await owner.PostAsJsonAsync("/api/fitness/measure", scenario);

        Assert.Equal(HttpStatusCode.OK, model.StatusCode);
        Assert.Contains("parameters", await model.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, predicted.StatusCode);
        Assert.Contains("predicted", await predicted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, solved.StatusCode);
        Assert.Contains("\"solved\":{", await solved.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, surface.StatusCode);
        Assert.Equal(8, (await surface.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("seconds").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, measure.StatusCode);
        Assert.Contains("options", await measure.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_solver_refuses_a_scenario_it_cannot_reason_about()
    {
        using var owner = Owner_();
        var scenario = new { distanceMeters = 8046.72, months = 12, weeklyHours = 6, compliance = 0.85 };

        var tooShort = await owner.PostAsJsonAsync("/api/fitness/solve", new { scenario = new { distanceMeters = 100, months = 12, weeklyHours = 6, compliance = 0.85 } });
        var noTarget = await owner.PostAsJsonAsync("/api/fitness/solve", new { scenario, solveFor = "weeklyHours" });
        var noSuchFactor = await owner.PostAsJsonAsync("/api/fitness/solve", new { scenario, solveFor = "luck", targetSeconds = 2000 });
        var sameAxis = await owner.PostAsJsonAsync("/api/fitness/surface", new { scenario, across = "Months", down = "Months" });

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noTarget.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noSuchFactor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sameAxis.StatusCode);
    }

    [Fact]
    public async Task A_document_is_saved_under_a_known_key_and_read_back_verbatim()
    {
        using var owner = Owner_();

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/progress/plan")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/progress/diary")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsync("/api/progress/diary", Json("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsync("/api/progress/plan", Json("[1,2]"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync("/api/progress/plan", Json("{\"notes\":\"first\"}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync("/api/progress/plan", Json("{\"notes\":\"second\"}"))).StatusCode);

        using var read = await owner.GetAsync("/api/progress/plan");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("{\"notes\":\"second\"}", await read.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_prediction_is_locked_scored_and_struck_from_the_ledger()
    {
        using var owner = Owner_();
        var target = DateTime.UtcNow.AddDays(60).ToString("yyyy-MM-dd");

        using var locked = await owner.PostAsJsonAsync("/api/fitness/predictions/locked", new
        {
            targetDate = target, distanceMeters = 8046.72, predictedSeconds = 2400,
            predictedFastSeconds = 2330, predictedSlowSeconds = 2470, weeklyHours = 6, compliance = 0.85
        });
        using var backwards = await owner.PostAsJsonAsync("/api/fitness/predictions/locked", new
        {
            targetDate = target, distanceMeters = 8046.72, predictedSeconds = 2400,
            predictedFastSeconds = 2470, predictedSlowSeconds = 2330, weeklyHours = 6, compliance = 0.85
        });
        using var hindsight = await owner.PostAsJsonAsync("/api/fitness/predictions/locked", new
        {
            targetDate = "2020-01-01", distanceMeters = 8046.72, predictedSeconds = 2400,
            predictedFastSeconds = 2330, predictedSlowSeconds = 2470, weeklyHours = 6, compliance = 0.85
        });

        Assert.Equal(HttpStatusCode.NoContent, locked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, hindsight.StatusCode);
        Assert.Equal(3, (await owner.GetFromJsonAsync<JsonElement>("/api/fitness/predictions/locked")).GetArrayLength());

        using var scored = await owner.PostAsJsonAsync($"/api/fitness/predictions/locked/{PendingPrediction}/actual", new { actualSeconds = 2390 });
        using var noTime = await owner.PostAsJsonAsync($"/api/fitness/predictions/locked/{PendingPrediction}/actual", new { actualSeconds = 0 });
        using var unknown = await owner.PostAsJsonAsync($"/api/fitness/predictions/locked/{Guid.NewGuid()}/actual", new { actualSeconds = 2390 });

        Assert.Equal(HttpStatusCode.NoContent, scored.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noTime.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var ledger = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/predictions/locked");
        var answered = Prediction(ledger, PendingPrediction);
        Assert.Equal("scored", answered.GetProperty("status").GetString());
        Assert.Equal(-10, answered.GetProperty("errorSeconds").GetDouble());
        Assert.True(answered.GetProperty("insideInterval").GetBoolean());

        using var struck = await owner.DeleteAsync($"/api/fitness/predictions/locked/{ScoredPrediction}");
        using var gone = await owner.DeleteAsync($"/api/fitness/predictions/locked/{ScoredPrediction}");

        Assert.Equal(HttpStatusCode.NoContent, struck.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(2, (await owner.GetFromJsonAsync<JsonElement>("/api/fitness/predictions/locked")).GetArrayLength());
    }

    // ------------------------------------------------------------ plumbing

    /// <summary>Production's shape, plus both sync bridges so their routes are mapped.</summary>
    private static void SignInRequired(IWebHostBuilder builder)
    {
        builder.UseSetting("Fitness:DevelopmentOwner", "false");
        builder.UseSetting("Fitness:AllowedEmails:0", Owner);
        builder.UseSetting("Fitness:HevyApiKey", "hevy-test-key");
        builder.UseSetting("IntervalsIcu:ApiKey", "icu-test-key");
        builder.UseSetting("Admin:GoogleClientId", "test-client");
        builder.UseSetting("Admin:GoogleClientSecret", "test-secret");
        builder.UseSetting("Admin:AllowedEmails:0", Owner);

        // Neither bridge is dialled for real: every outbound HTTP client in
        // the host answers as an account with nothing in it.
        builder.ConfigureTestServices(services => services.ConfigureHttpClientDefaults(http =>
            http.ConfigurePrimaryHttpMessageHandler(() => new EmptyAccounts())));
    }

    private sealed class EmptyAccounts : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri?.Host == "api.hevyapp.com"
                ? "{\"page\":1,\"page_count\":1,\"workouts\":[]}"
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private HttpClient Owner_() => _app.Factory.CreateClient().SignedInAs(_app.Factory.Services, Owner);

    private HttpClient Stranger_() => _app.Factory.CreateClient().SignedInAs(_app.Factory.Services, Stranger);

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Parse(method), path);
        if (method is "POST" or "PUT")
        {
            // Authorization runs before the body is read; an empty object is
            // enough to show that, whatever the route would have done with it.
            request.Content = Json("{}");
        }

        return client.SendAsync(request);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<int> ActivityCountAsync(HttpClient owner) =>
        (await owner.GetFromJsonAsync<JsonElement>("/api/fitness/activities")).GetProperty("total").GetInt32();

    private static async Task<JsonElement> ActivityAsync(HttpClient owner, Guid id)
    {
        var page = await owner.GetFromJsonAsync<JsonElement>("/api/fitness/activities");
        return page.GetProperty("activities").EnumerateArray().Single(activity => activity.GetProperty("id").GetGuid() == id);
    }

    private static JsonElement Prediction(JsonElement ledger, Guid id) =>
        ledger.EnumerateArray().Single(prediction => prediction.GetProperty("id").GetString() == id.ToString());

    private async Task<int> CountAsync(Func<FitnessDbContext, Task<int>> count)
    {
        using var scope = _app.Factory.Services.CreateScope();
        return await count(scope.ServiceProvider.GetRequiredService<FitnessDbContext>());
    }
}
