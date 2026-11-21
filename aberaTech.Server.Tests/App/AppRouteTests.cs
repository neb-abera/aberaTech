using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Scheduling.AppAccess;
using aberaTech.Server.App;
using aberaTech.Server.Playbook;
using aberaTech.Server.Tests.Security;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.App;

/// <summary>
/// The aberaTech app's server side: a household member signs in with
/// either Google address and gets one view, sees the requests that name
/// them with their status, adds, edits, and removes their own, and reads
/// the wish list. Neb asked for this on 2026-11-21.
/// </summary>
public sealed class AppRouteTests : IDisposable
{
    private const string MemberGmail = "member.first@example.test";
    private const string MemberOtherGmail = "member.second@example.test";

    private readonly TasksNotion _notion = new();
    private readonly MemoryAppAccessStore _store = new();
    private readonly TestApp _app;

    public AppRouteTests()
    {
        var settings = ProbeSurfaceLimitsTests.Configured(DatabaseMigrationsTests.Unreachable);
        settings["Notion:Token"] = "notion-token-for-tests";
        settings["Notion:PlaybookPageId"] = "00000000-0000-0000-0000-0000000000aa";
        settings["App:Household:0:Name"] = "Sam";
        settings["App:ToldProperty"] = "Requester told";
        settings["App:Household:0:Emails:0"] = MemberGmail;
        settings["App:Household:0:Emails:1"] = MemberOtherGmail;
        settings["App:TasksDataSourceId"] = TasksNotion.Tasks;
        settings["App:WishListDataSourceId"] = TasksNotion.WishList;
        _app = new TestApp(settings, services =>
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<IAppAccessStore>();
            services.AddSingleton<IAppAccessStore>(_store);
            services.AddHttpClient(NotionClient.ApiClient).ConfigurePrimaryHttpMessageHandler(() => _notion);
        });
    }

    public void Dispose() => _app.Dispose();

    private HttpClient Client(string? token = null)
    {
        var client = _app.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> TokenFor(string person)
    {
        var token = AppTokens.New();
        await _store.AddSessionAsync(Guid.NewGuid(), AppTokens.Hash(token), person, "iPhone", Instant.FromUtc(2026, 11, 21, 12, 0), CancellationToken.None);
        return token;
    }

    /// <summary>The Google half done, as the handoff does it, then the app's exchange.</summary>
    private async Task<(string Redirect, HttpResponseMessage Exchange)> SignInAs(string email, string? verifierSent = null)
    {
        var verifier = AppTokens.NewCode() + "-verifier";
        using var scope = _app.Factory.Services.CreateScope();
        var handoff = scope.ServiceProvider.GetRequiredService<IAppSignInHandoff>();
        var redirect = await handoff.CompleteAsync(email, AppTokens.Challenge(verifier), "s1", CancellationToken.None);
        var code = new Uri(redirect).Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2)).FirstOrDefault(kv => kv[0] == "code")?[1];
        var exchange = await Client().PostAsJsonAsync("/api/app/session",
            new { code = Uri.UnescapeDataString(code ?? "none"), verifier = verifierSent ?? verifier, device = "Sam's iPhone" });
        return (redirect, exchange);
    }

    [Fact]
    public async Task Sign_in_starts_at_google_and_needs_a_pkce_challenge()
    {
        using var client = _app.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var challenge = AppTokens.Challenge(AppTokens.NewCode() + "-verifier");

        var start = await client.GetAsync($"/api/app/sign-in?state=s1&challenge={challenge}");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.StartsWith("https://accounts.google.com/", start.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/app/sign-in?state=s1&challenge=short")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/app/sign-in?challenge={challenge}")).StatusCode);
    }

    [Theory]
    [InlineData(MemberGmail)]
    [InlineData(MemberOtherGmail)]
    public async Task Member_signs_in_with_either_gmail_and_gets_the_same_view(string email)
    {
        var (redirect, exchange) = await SignInAs(email);

        Assert.StartsWith("aberatech://signin?state=s1&code=", redirect);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var session = await exchange.Content.ReadFromJsonAsync<AppSignIn.SessionResponse>();
        Assert.Equal("Sam", session!.Name);
        Assert.Equal("household", session.Role);

        var me = await Client(session.Token).GetFromJsonAsync<AppSignIn.MeResponse>("/api/app/me");
        Assert.Equal(new AppSignIn.MeResponse("Sam", "household"), me);
    }

    [Fact]
    public async Task The_owner_signs_in_as_the_owner_and_a_stranger_is_turned_away()
    {
        var (_, owner) = await SignInAs("owner@example.test");
        Assert.Equal("owner", (await owner.Content.ReadFromJsonAsync<AppSignIn.SessionResponse>())!.Role);

        var (redirect, stranger) = await SignInAs("someone@example.test");
        Assert.Equal("aberatech://signin?state=s1&error=not-allowed", redirect);
        Assert.Equal(HttpStatusCode.Unauthorized, stranger.StatusCode);
    }

    [Fact]
    public async Task A_code_without_its_verifier_or_used_twice_signs_nobody_in()
    {
        var (_, wrong) = await SignInAs(MemberGmail, verifierSent: AppTokens.NewCode() + "-another");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        // The failed exchange used the code up.
        Assert.Equal(0, _store.Sessions);
    }

    [Fact]
    public async Task Signing_out_ends_the_session_and_nothing_answers_without_a_token()
    {
        var token = await TokenFor("Sam");
        Assert.Equal(HttpStatusCode.NoContent, (await Client(token).DeleteAsync("/api/app/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(token).GetAsync("/api/app/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/app/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("abt_" + new string('A', 43)).GetAsync("/api/app/requests")).StatusCode);
    }

    [Fact]
    public async Task Member_sees_her_own_requests_with_their_status_and_nothing_else()
    {
        var now = DateTimeOffset.UtcNow;
        string Ago(int days) => now.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        _notion.AddTask("Rake leaves", "Sam", gmailThread: "18000000000000001", created: Ago(6));
        _notion.AddTask("Fix the faucet", "Sam", status: "In progress", created: Ago(5));
        _notion.AddTask("Book the flight", "Sam", status: "Done", created: Ago(9), edited: Ago(2));
        _notion.AddTask("Old done", "Sam", status: "Done", created: Ago(90), edited: Ago(60));
        _notion.AddTask("Paint the fence", "Sam", status: "Archived", notes: "Reason: Hiring it out", edited: Ago(1));
        _notion.AddTask("Changed my mind", "Sam", status: "Archived", notes: "Reason: Removed by Sam", edited: Ago(1));
        _notion.AddTask("DEMOB validation brief", null);

        var list = await Client(await TokenFor("Sam")).GetFromJsonAsync<List<AppRequestDto>>("/api/app/requests");

        Assert.Equal(
            new[] { "Book the flight", "Fix the faucet", "Paint the fence", "Rake leaves" },
            list!.Select(r => r.Title).Order(StringComparer.Ordinal));
        var byTitle = list!.ToDictionary(r => r.Title);
        Assert.Equal("received", byTitle["Rake leaves"].Status);
        Assert.True(byTitle["Rake leaves"].FromEmail);
        Assert.Equal("in-progress", byTitle["Fix the faucet"].Status);
        Assert.Equal("done", byTitle["Book the flight"].Status);
        Assert.False(byTitle["Book the flight"].Editable);
        Assert.Equal("not-doing", byTitle["Paint the fence"].Status);
        Assert.Equal("Hiring it out", byTitle["Paint the fence"].Reason);
        Assert.Null(byTitle["Paint the fence"].Details);
    }

    [Fact]
    public async Task A_request_a_member_adds_lands_in_Tasks_as_hers()
    {
        var token = await TokenFor("Sam");
        var response = await Client(token).PostAsJsonAsync("/api/app/requests",
            new AppRequestInput("Buy leaf bags", "The paper ones, 10 of them", "2026-11-29", "https://dpw.dc.gov/leaf"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AppRequestDto>();
        Assert.Equal("Buy leaf bags", created!.Title);
        Assert.Equal("The paper ones, 10 of them", created.Details);
        Assert.Equal("2026-11-29", created.Due);
        Assert.Equal("received", created.Status);

        var sent = _notion.LastWrite!;
        Assert.Equal(TasksNotion.Tasks, sent["parent"]!["data_source_id"]!.GetValue<string>());
        Assert.Equal("Sam", sent["properties"]!["Requested by"]!["select"]!["name"]!.GetValue<string>());
        Assert.Equal("Got it", sent["properties"]!["Requester told"]!["select"]!["name"]!.GetValue<string>());
        Assert.Equal("Not started", sent["properties"]!["Status"]!["status"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Member_edits_and_removes_her_own_requests_and_cannot_touch_anyone_elses()
    {
        var token = await TokenFor("Sam");
        var hers = _notion.AddTask("Rake leaves", "Sam");
        var his = _notion.AddTask("DEMOB validation brief", null);
        var done = _notion.AddTask("Book the flight", "Sam", status: "Done");

        var edit = await Client(token).PutAsJsonAsync($"/api/app/requests/{hers}", new AppRequestInput("Rake the front yard", "Before Sunday", null, null));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("Rake the front yard", (await edit.Content.ReadFromJsonAsync<AppRequestDto>())!.Title);

        Assert.Equal(HttpStatusCode.NotFound, (await Client(token).PutAsJsonAsync($"/api/app/requests/{his}", new AppRequestInput("Mine now", null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(token).PostAsync($"/api/app/requests/{his}/remove", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client(token).PutAsJsonAsync($"/api/app/requests/{done}", new AppRequestInput("Again", null, null, null))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Client(token).PostAsync($"/api/app/requests/{hers}/remove", null)).StatusCode);
        Assert.Equal("Archived", _notion.Status(hers));
        Assert.Equal("Not started", _notion.Status(his));
        var list = await Client(token).GetFromJsonAsync<List<AppRequestDto>>("/api/app/requests");
        Assert.DoesNotContain(list!, r => r.Id == hers);
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("Title", "2026-13-40", null)]
    [InlineData("Title", null, "javascript:alert(1)")]
    public async Task A_request_without_a_title_or_with_a_bad_date_or_link_is_refused(string title, string? due, string? link)
    {
        var response = await Client(await TokenFor("Sam")).PostAsJsonAsync("/api/app/requests", new AppRequestInput(title, null, due, link));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(_notion.LastWrite);
    }

    [Fact]
    public async Task The_wish_list_shows_what_is_still_wanted_best_first()
    {
        _notion.AddWish("Lifting belt", "Bought", "Now", 9);
        _notion.AddWish("AI camera", "Recommended", "Q1 2027", 7, 299);
        _notion.AddWish("Running shoes", "Waiting", "Now", 8, 160);
        _notion.AddWish("Massage gun", "Skip", "2027", 6);
        _notion.AddWish("Boat", "Researching", "Do not buy", 1);

        var items = await Client(await TokenFor("Sam")).GetFromJsonAsync<List<AppWishItemDto>>("/api/app/wishlist");

        Assert.Equal(["Running shoes", "AI camera"], items!.Select(i => i.Item));
        Assert.Equal(160, items![0].Price);
    }

    [Fact]
    public async Task A_session_for_someone_taken_out_of_the_household_is_refused()
    {
        var token = await TokenFor("Former");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(token).GetAsync("/api/app/me")).StatusCode);
    }
}
