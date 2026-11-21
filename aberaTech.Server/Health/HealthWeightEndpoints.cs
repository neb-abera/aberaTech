using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Scheduling.Alerts;
using Microsoft.AspNetCore.OutputCaching;
using NodaTime;

namespace aberaTech.Server.Health;

/// <summary>
/// <c>POST /api/alerts/health/weights</c>: Apple Health weigh-ins from the
/// paired abera-alarms phone, stored as the Log tab's weigh-ins.
/// </summary>
/// <remarks>
/// It sits under /api/alerts because the phone's token is an alerts token
/// and only that scheme issues one. It is the phone's alone: a browser has
/// no Apple Health to read, and the Log tab already takes a weigh-in by
/// hand. It is mapped only where both the alerts and the fitness surfaces
/// are, since it writes the one and is authorized by the other.
/// </remarks>
public static class HealthWeightEndpoints
{
    public const string Path = "/api/alerts/health/weights";

    public static IEndpointRouteBuilder MapHealthWeightEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(Path, async (
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
            .RequireAuthorization(AlertsAuth.DevicePolicy)
            .RequireRateLimiting(AlertsEndpoints.ActionsPolicy)
            .WithTags("Alerts");

        return routes;
    }
}
