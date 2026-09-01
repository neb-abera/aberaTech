using Azure.Monitor.OpenTelemetry.AspNetCore;
using System.Threading.RateLimiting;
using aberaTech.Server;
using aberaTech.Scheduling;
using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using aberaTech.Scheduling.Outbox;
using aberaTech.Scheduling.Admin;
using aberaTech.Scheduling.Calendar;
using aberaTech.Scheduling.Compliance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using aberaTech.Scheduling.Sms;
using Microsoft.AspNetCore.RateLimiting;
using aberaTech.Fitness;
using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using aberaTech.Postgres;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Observability: OpenTelemetry traces, metrics and logs shipped to Azure
// Monitor when APPLICATIONINSIGHTS_CONNECTION_STRING is set (the container
// app sets it; local runs stay silent). The availability alerts tell us
// *that* something broke; this is how we see *why*.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ---------------------------------------------------------------- scheduling

var schedulingOptions = builder.Configuration.GetSection(SchedulingOptions.Section).Get<SchedulingOptions>()
                        ?? new SchedulingOptions();
builder.Services.AddSingleton(schedulingOptions);

var twilioOptions = builder.Configuration.GetSection(TwilioOptions.Section).Get<TwilioOptions>() ?? new TwilioOptions();
builder.Services.AddSingleton(twilioOptions);

var adminOptions = builder.Configuration.GetSection(AdminOptions.Section).Get<AdminOptions>() ?? new AdminOptions();
builder.Services.AddSingleton(adminOptions);

// Without Google credentials and an allowlist there is no admin surface at all:
// the endpoints are never mapped. Failing closed rather than falling back to
// something weaker means a half-finished configuration cannot quietly leave the
// queue open to whoever finds it.
if (adminOptions.IsConfigured)
{
    builder.Services.AddSchedulingAdminAuth(adminOptions);
}

// One clock, injected everywhere, so that "now" is a dependency rather than an
// ambient fact. It is what lets the domain tests assert behaviour at a daylight
// saving transition without waiting for March.
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

var connectionString = builder.Configuration.GetConnectionString("Scheduling");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>()
                          ?? new DatabaseOptions();
    builder.Services.AddSingleton(databaseOptions);

    // One data source for the process. It owns the connection pool and, with
    // Entra auth, the token refresh — so the token is fetched on a timer for the
    // whole application rather than per connection.
    builder.Services.AddSingleton(services => PostgresDataSource.Build(
        connectionString,
        databaseOptions,
        services.GetRequiredService<ILoggerFactory>()));

    builder.Services.AddDbContext<SchedulingDbContext>((services, options) =>
        options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.UseNodaTime()));

    builder.Services.AddScoped<QueueNotifier>();

    // Busy time comes from two places. Appointments booked here are local and
    // always available; the host's Google calendar is remote and may not be.
    // The composite tolerates the second failing, so a slow or unreachable
    // Google costs accuracy rather than availability.
    var calendarOptions = builder.Configuration.GetSection(GoogleCalendarOptions.Section)
                              .Get<GoogleCalendarOptions>() ?? new GoogleCalendarOptions();
    builder.Services.AddSingleton(calendarOptions);

    builder.Services.AddScoped<DatabaseBusySource>();

    if (adminOptions.IsConfigured)
    {
        // Keys in the database, so the encrypted refresh token survives a
        // restart. Without this the container regenerates its key ring on every
        // deploy and the stored token becomes permanently unreadable.
        builder.Services
            .AddDataProtection()
            .PersistKeysToDbContext<SchedulingDbContext>();

        builder.Services.AddHttpClient<GoogleAccessTokens>(client =>
            client.Timeout = TimeSpan.FromSeconds(calendarOptions.TimeoutSeconds));

        builder.Services.AddHttpClient<GoogleCalendarBusySource>(client =>
            client.Timeout = TimeSpan.FromSeconds(calendarOptions.TimeoutSeconds));

        builder.Services.AddHttpClient<GoogleCalendarInvites>(client =>
            client.Timeout = TimeSpan.FromSeconds(calendarOptions.TimeoutSeconds));
        builder.Services.AddScoped<ICalendarInvites>(services =>
            services.GetRequiredService<GoogleCalendarInvites>());
    }
    else
    {
        // Same shape as the logging SMS sender: booking with an email works in
        // development without a Google project, it just sends no invite.
        builder.Services.AddScoped<ICalendarInvites, NoCalendarInvites>();
    }

    builder.Services.AddScoped<IBusySource>(services =>
    {
        var sources = new List<IBusySource> { services.GetRequiredService<DatabaseBusySource>() };

        if (adminOptions.IsConfigured)
        {
            sources.Add(services.GetRequiredService<GoogleCalendarBusySource>());
        }

        return new CompositeBusySource(
            sources,
            services.GetRequiredService<ILogger<CompositeBusySource>>());
    });

    if (twilioOptions.IsConfigured)
    {
        builder.Services.AddHttpClient<IMessageSender, TwilioMessageSender>(client =>
            // Bounded so a hung provider cannot pin a dispatcher slot: the tick
            // that follows will pick the message up again anyway.
            client.Timeout = TimeSpan.FromSeconds(15));
    }
    else
    {
        // No credentials, so the dispatcher still runs, retries and dead letters
        // against a sender that only writes to the log. The whole path stays
        // exercisable without an SMS account or A2P registration.
        builder.Services.AddScoped<IMessageSender, LoggingMessageSender>();

        if (twilioOptions.IsPartiallyConfigured)
        {
            // Loud, because the symptom otherwise looks like the carrier is at
            // fault: messages send and then every one of them dead letters.
            builder.Services.AddSingleton<IHostedService>(services =>
                new StartupWarning(
                    services.GetRequiredService<ILoggerFactory>().CreateLogger("Sms"),
                    "Twilio is partly configured, so no SMS will be sent. All four of "
                    + "Twilio:AccountSid, AuthToken, FromNumber and StatusCallbackUrl are required; "
                    + "the callback URL is what delivery receipts arrive on."));
        }
    }
    builder.Services.AddHostedService<OutboxDispatcher>();
}

// ---------------------------------------------------------------- fitness

// Personal training data and predictions. Fails closed three separate ways on
// purpose: no database connection, no allowlist, or no Google credentials for
// the sign-in schemes each mean the endpoints are never mapped at all. The one
// exception is the explicit Development-only owner bypass, decided by
// FitnessGate, so `make up` can show the real console against the local
// loopback database without a Google project.
var fitnessOptions = builder.Configuration.GetSection(FitnessOptions.Section).Get<FitnessOptions>()
                     ?? new FitnessOptions();
builder.Services.AddSingleton(fitnessOptions);

var fitnessConnection = builder.Configuration.GetConnectionString("Fitness");
var fitnessRequiresSignIn = FitnessGate.RequiresOwnerSignIn(
    builder.Environment.IsDevelopment(), fitnessOptions);
var fitnessEnabled = FitnessGate.IsEnabled(
    builder.Environment.IsDevelopment(), fitnessOptions, adminOptions.IsConfigured, fitnessConnection);

if (fitnessEnabled)
{
    if (fitnessRequiresSignIn)
    {
        builder.Services.AddFitnessAuthorization(fitnessOptions);
    }

    // One posterior per version of the history, shared by every request — the
    // development bypass needs it as much as the signed-in path does.
    builder.Services.AddSingleton<PosteriorCache>();

    // Its own data source: a different database on the same shared server, so
    // it cannot share scheduling's. Keyed, because the container can only hold
    // one unkeyed NpgsqlDataSource and scheduling already is it.
    var fitnessDatabaseOptions = builder.Configuration.GetSection(aberaTech.Postgres.DatabaseOptions.Section)
                                     .Get<aberaTech.Postgres.DatabaseOptions>()
                                 ?? new aberaTech.Postgres.DatabaseOptions();

    builder.Services.AddKeyedSingleton(
        "fitness-db",
        (services, _) => PostgresDataSource.Build(
            fitnessConnection!,
            fitnessDatabaseOptions,
            services.GetRequiredService<ILoggerFactory>()));

    builder.Services.AddDbContext<FitnessDbContext>((services, options) =>
        options.UseNpgsql(
            services.GetRequiredKeyedService<NpgsqlDataSource>("fitness-db"),
            npgsql => npgsql.UseNodaTime()));

    if (fitnessOptions.HasHevyApi)
    {
        builder.Services.AddHttpClient<HevyApiClient>(client =>
        {
            client.BaseAddress = new Uri(HevyApiClient.BaseAddress);
            client.DefaultRequestHeaders.Add("api-key", fitnessOptions.HevyApiKey);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }
}

// Rate limiting on everything a stranger can call that writes a row or causes a
// message to be sent. The booking page is public by design, and a public form
// wired to an SMS provider is a way to spend somebody else's money; this is the
// second half of that defence, after restricting destinations to +1.
//
// Partitioned by remote address, with a queue limit of zero: excess requests are
// rejected outright rather than held, because holding them is itself a way to
// exhaust the server.
// Compress what leaves the origin. Cloudflare compresses edge-to-browser
// regardless, but a cache MISS travels origin-to-edge as sent — and the
// template and Facewoof both learned this the measured way. EnableForHttps is
// safe here for the same reason it is there: no secrets appear in
// compressible responses (BREACH needs both in one body).
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(SchedulingEndpoints.PublicWritePolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseResponseCompression();

// Container Apps ingress terminates TLS and forwards over HTTP. Without this,
// every request appears to come from the ingress over plain HTTP: the rate
// limiter above partitions everyone into one shared bucket — so five booking
// attempts a minute was the budget for the whole internet, and one hostile
// caller could spend it — and HTTPS-dependent behaviour never engages. The
// known-proxy allowlists are cleared because the ingress has no fixed address;
// nothing reaches this container except through it, and ForwardedLimit stays
// at its default of one hop, so a spoofed X-Forwarded-For prepended by a
// caller is ignored in favour of the address the ingress itself appended.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Clear(), not an empty initializer: the defaults trust only loopback, an
// empty collection initializer leaves those defaults in place, and a list
// with entries in it means "trust only these" — cleared lists are how the
// middleware is told the one hop in front of it has no fixed address.
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Browser hardening headers on every response, static files included.
//
// The CSP names exactly what the client actually loads: MUI injects its styles
// as inline <style> elements, the guides embed Google Docs and YouTube players
// in iframes, and a handful of partner logos load from their own hosts.
// Everything else — scripts above all — is same-origin only.
// The prerendered pages carry MUI's color-scheme bootstrap as an inline
// script (it must run before first paint), so the policy allows exactly that
// script by hash, read from the shipped HTML at startup. Every prerendered
// page bakes the same script, so index.html speaks for all of them.
var inlineScriptHashes = "";
var shippedShell = app.Environment.WebRootFileProvider.GetFileInfo("/index.html");
if (shippedShell.Exists && shippedShell.PhysicalPath is not null)
{
    var hashes = CspInlineScripts.HashesIn(File.ReadAllText(shippedShell.PhysicalPath));
    if (hashes.Count > 0)
    {
        inlineScriptHashes = " " + string.Join(' ', hashes);
    }
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] =
        "default-src 'self'; "
        // The one third-party script: Cloudflare's RUM beacon, injected by
        // the CDN into every HTML response and opted into deliberately for
        // real-user Core Web Vitals. It loads from static.cloudflareinsights
        // and reports to cloudflareinsights (connect-src below).
        + $"script-src 'self' https://static.cloudflareinsights.com{inlineScriptHashes}; "
        + "style-src 'self' 'unsafe-inline'; "
        // lduhtrp.net (CJ Affiliate) 302-redirects to yceml.net, which serves
        // the actual image bytes; CSP checks every hop, so both hosts must be
        // listed.
        + "img-src 'self' data: https://www.va.gov https://www.lduhtrp.net "
        + "https://www.yceml.net "
        + "https://www.hiringourheroes.org https://nvf.org https://assets.recruitmilitary.com; "
        + "font-src 'self' data:; "
        + "connect-src 'self' https://cloudflareinsights.com; "
        + "frame-src https://docs.google.com https://drive.google.com "
        + "https://www.youtube.com https://www.youtube-nocookie.com; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; "
        + "frame-ancestors 'self'; upgrade-insecure-requests";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    // Cross-origin isolation (Spectre-class leak mitigations). COEP is
    // deliberately absent: the partner images (va.gov, yceml.net, ...) send
    // no CORP headers and require-corp would block them.
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Cross-Origin-Resource-Policy"] = "same-origin";

    if (context.Request.IsHttps)
    {
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

// Prerendered pages: /transition is on disk as /transition/index.html, so an
// extensionless GET whose prerendered file exists is rewritten to it before
// the static file middleware looks. Anything else falls through unchanged.
var webRoot = app.Environment.WebRootFileProvider;
app.Use((context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
    {
        var rewritten = PrerenderedPages.RewriteFor(
            context.Request.Path.Value ?? "/",
            candidate => webRoot.GetFileInfo(candidate).Exists);
        if (rewritten is not null)
        {
            context.Request.Path = rewritten;
        }
    }

    return next();
});

app.UseDefaultFiles(); // Serves 'index.html' automatically for root requests.

// One options object shared with the SPA fallback below, so every path that
// serves a file applies the same cache policy.
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl =
            StaticAssetCaching.For(ctx.Context.Request.Path, ctx.File.Name)
};

app.UseStaticFiles(staticFileOptions); // Serves files from wwwroot.

// Explicit, and deliberately AFTER the static file middleware. Left implicit,
// WebApplication puts routing at the front of the pipeline, where the SPA
// fallback endpoint matches every extensionless request — and the static file
// middleware stands down when an endpoint has already matched, so the
// prerendered-page rewrite and the default-files behaviour above would both be
// dead code and every page would serve the empty shell.
app.UseRouting();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

if (adminOptions.IsConfigured)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    // Migrate on start. Reasonable here because this deploys as a single
    // container app revision with one writer; it would not be reasonable behind
    // several replicas rolling independently, where two instances can race the
    // same migration. Revisit that before scaling out, not after.
    using (var scope = app.Services.CreateScope())
    {
        var database = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        await database.Database.MigrateAsync();

        if (app.Environment.IsDevelopment())
        {
            await SchedulingDevelopmentData.SeedAsync(database, schedulingOptions);
        }
    }

    app.MapSchedulingEndpoints();

    if (twilioOptions.IsConfigured)
    {
        app.MapSmsReceipts();
    }

    if (adminOptions.IsConfigured)
    {
        app.MapAdminAuthEndpoints(adminOptions);
        app.MapAdminEndpoints();
        app.MapAvailabilityEndpoints();
        app.MapCalendarAdminEndpoints();
    }
}
else
{
    // Deployed without a database yet. The tab is visible either way, so it has
    // to explain itself rather than break.
    app.MapSchedulingUnavailable(schedulingOptions);
}

if (fitnessEnabled)
{
    // Same single-writer reasoning as the scheduling migration above.
    using (var scope = app.Services.CreateScope())
    {
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        await database.Database.MigrateAsync();
    }

    app.MapFitnessEndpoints(fitnessOptions, fitnessRequiresSignIn);
}
else
{
    app.MapFitnessUnavailable();
}

// Running the queue needs a database *and* credentials, so the "not configured"
// answer has to cover a missing either. Kept out of the branches above because
// nesting it under the database check was exactly the bug: with no connection
// string the route was never mapped at all, the request fell through to the SPA
// fallback, and the admin page got index.html where it expected JSON.
if (string.IsNullOrWhiteSpace(connectionString) || !adminOptions.IsConfigured)
{
    app.MapAdminUnavailable();
}

// Before the SPA fallback, so a plain fetch of these two gets real HTML rather
// than an empty shell. Mapped unconditionally: they must answer on any
// deployment, including one with no database and no messaging configured, since
// a carrier reviewing the campaign will fetch them whatever else is switched on.
app.MapCompliancePages();

// spa.html, not index.html: index.html now carries the home page's
// prerendered markup, and a client-rendered route served over it would flash
// the wrong page and then hydrate against DOM that contradicts it. spa.html is
// the same shell with the root div left empty.
//
// The status code is the app's own answer, not a blanket 200. app-routes.json
// is written at build time from site/routes.ts — the list the router itself
// reads — so a path the app cannot render is a 404 here as well as on screen.
// A deployment without the manifest keeps the old behaviour rather than
// answering 404 to everything.
var appRoutes = AppRoutes.Load(app.Environment.WebRootPath);

app.MapFallback(async context =>
{
    var known = appRoutes is null || appRoutes.Contains(context.Request.Path.Value ?? "/");
    context.Response.StatusCode = known ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;

    // The same policy the static pipeline gives the shell: it changes under a
    // stable URL on every deploy, so it always revalidates.
    context.Response.Headers.CacheControl = StaticAssetCaching.For(context.Request.Path, "spa.html");
    context.Response.ContentType = "text/html; charset=utf-8";

    // The environment's provider, not staticFileOptions': that object leaves
    // FileProvider null and the middleware fills it in from the web root at
    // request time, which this endpoint never goes through.
    await context.Response.SendFileAsync(
        app.Environment.WebRootFileProvider.GetFileInfo("spa.html"),
        context.RequestAborted);
});

app.Run();

// So WebApplicationFactory can see the entry point; top-level statements
// compile to an internal Program class otherwise.
public partial class Program;
