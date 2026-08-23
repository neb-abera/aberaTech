using System.Threading.RateLimiting;
using aberaTech.Server;
using aberaTech.Scheduling;
using aberaTech.Scheduling.Api;
using aberaTech.Scheduling.Data;
using aberaTech.Scheduling.Domain;
using aberaTech.Scheduling.Outbox;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NodaTime;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ---------------------------------------------------------------- scheduling

var schedulingOptions = builder.Configuration.GetSection(SchedulingOptions.Section).Get<SchedulingOptions>()
                        ?? new SchedulingOptions();
builder.Services.AddSingleton(schedulingOptions);

// One clock, injected everywhere, so that "now" is a dependency rather than an
// ambient fact. It is what lets the domain tests assert behaviour at a daylight
// saving transition without waiting for March.
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

var connectionString = builder.Configuration.GetConnectionString("Scheduling");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<SchedulingDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime()));

    builder.Services.AddScoped<QueueNotifier>();

    // Swapped for a real provider once SMS is configured; until then the
    // dispatcher runs, retries and dead letters against a sender that only
    // writes to the log, so the whole path is exercisable without credentials.
    builder.Services.AddScoped<IMessageSender, LoggingMessageSender>();
    builder.Services.AddHostedService<OutboxDispatcher>();
}

// Rate limiting on everything a stranger can call that writes a row or causes a
// message to be sent. The booking page is public by design, and a public form
// wired to an SMS provider is a way to spend somebody else's money; this is the
// second half of that defence, after restricting destinations to +1.
//
// Partitioned by remote address, with a queue limit of zero: excess requests are
// rejected outright rather than held, because holding them is itself a way to
// exhaust the server.
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

app.UseDefaultFiles(); // Serves 'index.html' automatically for root requests.
app.UseStaticFiles(); // Serves files from wwwroot.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

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
}
else
{
    // Deployed without a database yet. The tab is visible either way, so it has
    // to explain itself rather than break.
    app.MapSchedulingUnavailable(schedulingOptions);
}

app.MapFallbackToFile("index.html");

app.Run();
