using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The cached fitness pages, from the outside: a repeat visit costs the
/// database nothing, a write is never hidden behind an old page, and the
/// cache is never a way around the sign-in.
/// </summary>
public sealed class FitnessOutputCacheTests
{
    private const string Owner = "owner@example.com";

    private const string GarminCsv =
        "Activity Type,Date,Title,Distance,Time,Avg HR,Max HR\n" +
        "Running,2026-06-14 06:57:36,Treadmill Running,3.02,0:20:00,153,161\n";

    [Theory]
    [InlineData("/api/fitness/summary")]
    [InlineData("/api/fitness/digest")]
    [InlineData("/api/fitness/readiness/outlook?weeklyHours=6")]
    public async Task A_repeat_visit_is_answered_without_the_database(string path)
    {
        using var app = new FitnessApp();
        await app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = app.Factory.CreateClient();

        var first = await client.GetStringAsync(path);

        app.Commands.Reset();
        var second = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(first, await second.Content.ReadAsStringAsync());
        Assert.Equal(0, app.Commands.Count);
    }

    [Fact]
    public async Task A_different_question_is_not_answered_from_the_last_one()
    {
        using var app = new FitnessApp();
        await app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = app.Factory.CreateClient();

        var six = await client.GetStringAsync("/api/fitness/readiness/outlook?weeklyHours=6");
        var twelve = await client.GetStringAsync("/api/fitness/readiness/outlook?weeklyHours=12");

        Assert.NotEqual(six, twelve);
    }

    [Fact]
    public async Task The_cache_adds_nothing_a_browser_or_a_cdn_would_act_on()
    {
        using var app = new FitnessApp();
        using var client = app.Factory.CreateClient();

        await client.GetAsync("/api/fitness/summary");
        var cached = await client.GetAsync("/api/fitness/summary");

        // Age is the proof this answer came from the cache. Server-side only:
        // /api is no-store to a browser and the edge, so nothing downstream
        // starts holding the owner's data.
        Assert.Equal("no-store", cached.Headers.CacheControl?.ToString());
        Assert.NotNull(cached.Headers.Age);
        Assert.Null(cached.Content.Headers.Expires);
        Assert.False(cached.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task An_import_is_on_the_page_at_once()
    {
        using var app = new FitnessApp();
        await app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = app.Factory.CreateClient();

        var before = await client.GetFromJsonAsync<Summary>("/api/fitness/summary");

        var import = await client.PostAsync(
            "/api/fitness/import", new StringContent(GarminCsv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        var after = await client.GetFromJsonAsync<Summary>("/api/fitness/summary");

        Assert.Equal(before!.ActivityCount + 1, after!.ActivityCount);
    }

    [Fact]
    public async Task So_is_every_other_write_the_pages_read()
    {
        using var app = new FitnessApp();
        await app.SeedAsync(FitnessSeed.AthleteAsync);
        using var client = app.Factory.CreateClient();

        var before = await client.GetStringAsync("/api/fitness/summary");

        // A weigh-in is no import and never touched the posterior cache, but
        // the summary prints it.
        var weighIn = await client.PostAsJsonAsync(
            "/api/fitness/body-metrics", new { date = "2026-06-15", weightKg = 79.5, bodyFatPercent = 14.0 });
        Assert.Equal(HttpStatusCode.NoContent, weighIn.StatusCode);

        var after = await client.GetStringAsync("/api/fitness/summary");

        Assert.NotEqual(before, after);
        Assert.Contains("79.5", after);
    }

    [Fact]
    public async Task A_cached_page_is_still_behind_the_sign_in()
    {
        using var app = new FitnessApp(SignInRequired);
        await app.SeedAsync(FitnessSeed.AthleteAsync);

        using var owner = app.Factory.CreateClient();
        owner.DefaultRequestHeaders.Add("Cookie", SessionCookieFor(app, Owner));

        // The owner fills the cache, and is then served from it.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/fitness/summary")).StatusCode);
        app.Commands.Reset();
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/fitness/summary")).StatusCode);
        Assert.Equal(0, app.Commands.Count);

        // Nobody else is.
        using var stranger = app.Factory.CreateClient();
        var anonymous = await stranger.GetAsync("/api/fitness/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.DoesNotContain("activityCount", await anonymous.Content.ReadAsStringAsync());

        using var other = app.Factory.CreateClient();
        other.DefaultRequestHeaders.Add("Cookie", SessionCookieFor(app, "someone.else@example.com"));
        var signedInButNotTheOwner = await other.GetAsync("/api/fitness/summary");
        Assert.Equal(HttpStatusCode.Forbidden, signedInButNotTheOwner.StatusCode);
        Assert.DoesNotContain("activityCount", await signedInButNotTheOwner.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_refusal_is_never_what_gets_cached()
    {
        using var app = new FitnessApp(SignInRequired);
        await app.SeedAsync(FitnessSeed.AthleteAsync);

        // The stranger asks first this time; the owner must still get the page.
        using var stranger = app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/fitness/summary")).StatusCode);

        using var owner = app.Factory.CreateClient();
        owner.DefaultRequestHeaders.Add("Cookie", SessionCookieFor(app, Owner));
        var page = await owner.GetAsync("/api/fitness/summary");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("activityCount", await page.Content.ReadAsStringAsync());
    }

    /// <summary>Production's shape: Google sign-in configured, one owner on the allowlist.</summary>
    private static void SignInRequired(IWebHostBuilder builder)
    {
        builder.UseSetting("Fitness:DevelopmentOwner", "false");
        builder.UseSetting("Fitness:AllowedEmails:0", Owner);
        builder.UseSetting("Admin:GoogleClientId", "test-client");
        builder.UseSetting("Admin:GoogleClientSecret", "test-secret");
        builder.UseSetting("Admin:AllowedEmails:0", Owner);
    }

    /// <summary>
    /// The cookie the sign-in would have issued, made with the application's
    /// own ticket format, so the request goes through the real cookie scheme
    /// and the real policy rather than a test double of either.
    /// </summary>
    private static string SessionCookieFor(FitnessApp app, string email) =>
        Support.AdminSession.CookieFor(app.Factory.Services, email);

    private sealed record Summary(int ActivityCount);
}
