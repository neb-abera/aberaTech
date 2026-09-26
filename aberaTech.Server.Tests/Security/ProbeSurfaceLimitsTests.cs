using System.Net;
using System.Net.Http.Headers;
using System.Text;
using aberaTech.Scheduling.Sms;
using aberaTech.Server.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace aberaTech.Server.Tests.Security;

/// <summary>
/// The routes a stranger can guess against or lean on: a bearer key, the
/// sign-in redirect, the Twilio webhook, the readiness probe. Each has a
/// ceiling, and none of the ceilings is ever in the way of the real caller.
/// </summary>
public sealed class ProbeSurfaceLimitsTests
{
    private const string DigestKey = "0123456789abcdef0123456789abcdef-test";
    private const string TwilioToken = "test-auth-token";
    private const string CallbackUrl = "https://abera.test/api/scheduling/sms-status";

    internal static Dictionary<string, string?> Configured(string scheduling, string? fitness = null) => new()
    {
        ["ConnectionStrings:Scheduling"] = scheduling,
        ["ConnectionStrings:Fitness"] = fitness ?? scheduling,
        ["Database:MigrateOnStart"] = "false",
        ["ClientAddress:ForwardedHops"] = "0",
        ["Admin:GoogleClientId"] = "test-client",
        ["Admin:GoogleClientSecret"] = "test-secret",
        ["Admin:AllowedEmails:0"] = "owner@example.test",
        ["Fitness:AllowedEmails:0"] = "owner@example.test",
        ["Fitness:DigestKey"] = DigestKey,
        ["Twilio:AccountSid"] = "ACtest",
        ["Twilio:AuthToken"] = TwilioToken,
        ["Twilio:FromNumber"] = "+12025550100",
        ["Twilio:StatusCallbackUrl"] = CallbackUrl
    };

    private static TestApp App() => new(Configured(DatabaseMigrationsTests.Unreachable));

    [Fact]
    public async Task Guessing_the_digest_key_runs_out_of_guesses()
    {
        using var app = App();
        using var client = app.CreateClient();

        for (var guess = 0; guess < 10; guess++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await DigestAsync(client, "203.0.113.7", $"wrong-{guess}"));
        }

        // Out of guesses — and that includes the right one, or the limit
        // would only slow a search down rather than end it.
        Assert.Equal(HttpStatusCode.TooManyRequests, await DigestAsync(client, "203.0.113.7", "wrong-again"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await DigestAsync(client, "203.0.113.7", DigestKey));

        // Somebody else's failures are not the morning brief's problem.
        Assert.Equal(HttpStatusCode.Unauthorized, await DigestAsync(client, "203.0.113.8", "wrong"));
    }

    [Fact]
    public async Task The_sign_in_redirect_cannot_be_hammered()
    {
        // The challenge encrypts its state with a key ring the app keeps in
        // the database, and this test has no database; keys held in memory
        // change nothing about what is being counted.
        using var app = new TestApp(
            Configured(DatabaseMigrationsTests.Unreachable),
            services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        using var client = app.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Redirect, await SignInAsync(client, "203.0.113.7"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await SignInAsync(client, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.Redirect, await SignInAsync(client, "203.0.113.8"));
    }

    [Fact]
    public async Task A_configured_sign_in_budget_replaces_the_ten()
    {
        // compose.yaml raises it for `make e2e`, where each owner spec
        // signs in from the suite's one container on three engines.
        var settings = Configured(DatabaseMigrationsTests.Unreachable);
        settings["RateLimits:SignInPerMinute"] = "12";
        using var app = new TestApp(
            settings,
            services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        using var client = app.CreateClient();

        for (var attempt = 0; attempt < 12; attempt++)
        {
            Assert.Equal(HttpStatusCode.Redirect, await SignInAsync(client, "203.0.113.7"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await SignInAsync(client, "203.0.113.7"));
    }

    [Fact]
    public async Task Forged_receipts_run_out_but_only_for_whoever_forged_them()
    {
        using var app = App();
        using var client = app.CreateClient();

        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, await ReceiptAsync(client, "203.0.113.7", signature: "forged"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await ReceiptAsync(client, "203.0.113.7", signature: "forged"));
        Assert.Equal(HttpStatusCode.Forbidden, await ReceiptAsync(client, "203.0.113.8", signature: "forged"));
    }

    [Fact]
    public async Task A_receipt_with_more_fields_than_twilio_sends_is_a_bad_request_not_a_crash()
    {
        using var app = App();
        using var client = app.CreateClient();

        var fields = Enumerable.Range(0, 200).ToDictionary(index => $"Field{index}", _ => "x");

        Assert.Equal(HttpStatusCode.BadRequest, await ReceiptAsync(client, "203.0.113.7", "forged", fields));
    }

    [Fact]
    public async Task A_receipt_with_a_field_longer_than_twilio_sends_is_a_bad_request()
    {
        using var app = App();
        using var client = app.CreateClient();

        var fields = new Dictionary<string, string> { ["MessageSid"] = new string('x', 8 * 1024) };

        Assert.Equal(HttpStatusCode.BadRequest, await ReceiptAsync(client, "203.0.113.7", "forged", fields));
    }

    [Fact]
    public async Task A_body_nobody_has_a_reason_to_send_is_refused_by_size()
    {
        // The largest legitimate public body is a booking: a name, a number,
        // an address. Kestrel's inherited ceiling was 30 MB for all of them.
        using var app = new TestApp(Configured(DatabaseMigrationsTests.Unreachable), kestrel: true);
        using var client = app.CreateClient();

        using var content = new StringContent(
            "{\"name\":\"" + new string('a', 100 * 1024) + "\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/scheduling/queue", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [PostgresFact]
    public async Task Real_receipts_are_never_limited()
    {
        // The limit counts failures. A burst of genuine receipts — every
        // reminder for a full day landing at once — spends none of it, even
        // from an address that has also been forging.
        using var database = new TestDatabase("receipts");
        var settings = Configured(database.ConnectionString, fitness: "");
        settings["Database:MigrateOnStart"] = "true";
        using var app = new TestApp(settings);
        using var client = app.CreateClient();

        for (var attempt = 0; attempt < 29; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, await ReceiptAsync(client, "203.0.113.7", signature: "forged"));
        }

        for (var receipt = 0; receipt < 100; receipt++)
        {
            Assert.Equal(HttpStatusCode.OK, await ReceiptAsync(client, "203.0.113.7", signature: null));
        }
    }

    [PostgresFact]
    public async Task The_owners_documents_and_uploads_keep_the_room_they_need()
    {
        // The global ceiling is low; the two routes with a reason to exceed
        // it say so themselves. Development, where the owner bypass stands in
        // for the Google sign-in these routes otherwise sit behind.
        using var scheduling = new TestDatabase("limits_s");
        using var fitness = new TestDatabase("limits_f");
        using var app = new TestApp(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scheduling"] = scheduling.ConnectionString,
                ["ConnectionStrings:Fitness"] = fitness.ConnectionString,
                ["Fitness:DevelopmentOwner"] = "true"
            },
            environment: "Development",
            kestrel: true);
        using var client = app.CreateClient();

        var document = "{\"notes\":\"" + new string('a', 200 * 1024) + "\"}";
        var saved = await client.PutAsync("/api/progress/plan", new StringContent(document, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        var tooBig = "{\"notes\":\"" + new string('a', 300 * 1024) + "\"}";
        var refused = await client.PutAsync("/api/progress/plan", new StringContent(tooBig, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);

        // Two megabytes of nothing: not a file the importer can read, but
        // past the global ceiling, so reaching the importer's own answer
        // proves the route raised it.
        var upload = await client.PostAsync("/api/fitness/import", new ByteArrayContent(new byte[2 * 1024 * 1024]));
        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, upload.StatusCode);
        Assert.True((int)upload.StatusCode < 500, $"import answered {(int)upload.StatusCode}");
    }

    private static async Task<HttpStatusCode> DigestAsync(HttpClient client, string peer, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/fitness/digest.txt");
        request.Headers.Add(RemoteAddressStartupFilter.Header, peer);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return (await client.SendAsync(request)).StatusCode;
    }

    private static async Task<HttpStatusCode> SignInAsync(HttpClient client, string peer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scheduling/admin/sign-in");
        request.Headers.Add(RemoteAddressStartupFilter.Header, peer);
        return (await client.SendAsync(request)).StatusCode;
    }

    /// <summary>A delivery receipt; <paramref name="signature"/> null means sign it properly.</summary>
    private static async Task<HttpStatusCode> ReceiptAsync(
        HttpClient client,
        string peer,
        string? signature,
        Dictionary<string, string>? fields = null)
    {
        fields ??= new Dictionary<string, string>
        {
            ["MessageSid"] = $"SM{Guid.NewGuid():N}",
            ["MessageStatus"] = "delivered"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, SmsReceiptEndpoint.Path)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Add(RemoteAddressStartupFilter.Header, peer);
        request.Headers.Add("X-Twilio-Signature", signature ?? TwilioSignature.Compute(TwilioToken, CallbackUrl, fields));
        return (await client.SendAsync(request)).StatusCode;
    }
}
