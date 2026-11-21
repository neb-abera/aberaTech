using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using Microsoft.AspNetCore.OutputCaching;
using NodaTime;

namespace aberaTech.Server.App;

/// <summary>
/// <c>POST /api/app/health/weights</c>: Apple Health weigh-ins from the
/// owner's aberaTech app, stored as the Log tab's weigh-ins.
/// </summary>
/// <remarks>
/// Neb logs his weight in Lose It, which writes it to Apple Health and
/// sends nothing to Garmin. Apple Health has no server API, so the app on
/// his phone reads it and posts it here (2026-11-21). The owner's token
/// alone: a household member's app reads his fitness and never writes it.
/// </remarks>
public static class AppHealth
{
    public const string WeightsPath = "/api/app/health/weights";

    public static IEndpointRouteBuilder MapAppHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(WeightsPath, async (
                HealthWeightsUpload upload, FitnessDbContext database, IClock clock,
                IOutputCacheStore cache, CancellationToken cancellationToken) =>
            {
                var today = clock.GetCurrentInstant().InUtc().Date;
                if (HealthWeights.Problem(upload, today) is { } problem)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["weights"] = [problem] });
                }

                var result = await HealthWeights.ApplyAsync(database, upload, cancellationToken);
                if (result.Saved > 0) await cache.EvictFitnessAsync();
                return Results.Ok(result);
            })
            .RequireAuthorization(AppAuth.OwnerPolicy)
            .RequireRateLimiting(AppRequests.WritePolicy);

        return routes;
    }
}
