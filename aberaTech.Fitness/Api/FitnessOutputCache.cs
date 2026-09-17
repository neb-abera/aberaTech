using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using NodaTime;

namespace aberaTech.Fitness.Api;

/// <summary>
/// Server-side caching of the fitness pages that are expensive to write and
/// say the same thing until the log changes.
/// </summary>
/// <remarks>
/// The summary, the digest and the outlook each read the whole log and fit a
/// model to it, and between two imports the answer does not move. The console
/// asks for them on every visit and every reload.
///
/// ASP.NET's stock policy refuses to cache anything for a signed-in caller,
/// which is every caller these endpoints have, so this is a policy of its own
/// and the care the stock one takes has to be taken here instead:
///
/// <list type="bullet">
/// <item>The middleware runs after authentication and authorization, so a
/// request that is not the owner's is answered 401 or 403 before the cache is
/// ever consulted. The policy does not lean on that alone: for an endpoint
/// that demands authorization it neither reads nor writes the cache unless
/// the caller is signed in, and the entry is keyed by who they are.</item>
/// <item>Only a 200 without a Set-Cookie is stored.</item>
/// <item>The pages are written as of today, so the key carries the date and
/// an entry lives an hour at most.</item>
/// <item>Every write to the fitness API, and every sync that stores
/// something, evicts the lot by tag.</item>
/// </list>
///
/// This is the origin's own memory and nothing downstream: no Cache-Control
/// is added, so the browser and Cloudflare treat /api exactly as before.
/// </remarks>
public static class FitnessOutputCache
{
    public const string PolicyName = "fitness-reads";

    /// <summary>The tag every cached fitness page carries, and the one eviction names.</summary>
    public const string Tag = "fitness";

    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public static IServiceCollection AddFitnessOutputCache(this IServiceCollection services) =>
        services.AddOutputCache(options => options.AddPolicy(PolicyName, new OwnerReadPolicy()));

    /// <summary>Forget every cached page: the log, or something the pages read beside it, has changed.</summary>
    public static ValueTask EvictFitnessAsync(this IOutputCacheStore store) =>
        // Not the request's token: a caller who hangs up after their write
        // has been saved must not leave the old page behind.
        store.EvictByTagAsync(Tag, CancellationToken.None);

    /// <summary>
    /// Evicts after anything that is not a read. Deliberately by method rather
    /// than per endpoint: a new write route is covered the day it is added,
    /// and an eviction too many costs one recomputation.
    /// </summary>
    internal static RouteGroupBuilder EvictOnWrite(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);

            var method = context.HttpContext.Request.Method;
            if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            {
                await context.HttpContext.RequestServices.GetRequiredService<IOutputCacheStore>().EvictFitnessAsync();
            }

            return result;
        });

        return group;
    }

    private sealed class OwnerReadPolicy : IOutputCachePolicy
    {
        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            var http = context.HttpContext;
            var method = http.Request.Method;
            var read = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

            var guarded = http.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>() is not null;
            var signedIn = http.User.Identity?.IsAuthenticated == true;
            var allowed = read && (!guarded || signedIn);

            context.EnableOutputCaching = true;
            context.AllowCacheLookup = allowed;
            context.AllowCacheStorage = allowed;
            context.AllowLocking = true;
            context.ResponseExpirationTimeSpan = Lifetime;
            context.Tags.Add(Tag);

            var today = http.RequestServices.GetRequiredService<IClock>().GetCurrentInstant().InUtc().Date;
            context.CacheVaryByRules.QueryKeys = "*";
            context.CacheVaryByRules.VaryByValues["owner"] = http.User.FindFirstValue(ClaimTypes.Email) ?? "";
            context.CacheVaryByRules.VaryByValues["day"] = today.ToString("uuuu-MM-dd", CultureInfo.InvariantCulture);

            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
            ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            var response = context.HttpContext.Response;

            if (response.StatusCode != StatusCodes.Status200OK
                || !StringValues.IsNullOrEmpty(response.Headers.SetCookie))
            {
                context.AllowCacheStorage = false;
            }

            return ValueTask.CompletedTask;
        }
    }
}
