namespace aberaTech.Server;

/// <summary>The names this site answers to.</summary>
public sealed class HostAllowlistOptions
{
    public const string Section = "HostAllowlist";

    /// <summary>
    /// Exact host names, or <c>*.suffix</c> for any subdomain. Blank entries
    /// are ignored. An empty list means no filtering in Development, which is
    /// what compose and local work want, and a refusal to start anywhere else.
    /// </summary>
    public string[] Hosts { get; set; } = [];
}

/// <summary>
/// Refuses a request whose Host header is not one of the site's own names.
/// </summary>
/// <remarks>
/// The framework has this built in, driven by <c>AllowedHosts</c>, and it is
/// deliberately left at "*": the built-in filter sits at the very front of the
/// pipeline with no exceptions, and a Container Apps HTTP probe addresses the
/// container by its pod IP — a name nobody can list in advance. How this
/// app's probes are configured lives in Azure, not in this repository, so a
/// filter that cannot exempt them risks a revision that never turns ready.
/// This one answers /healthz and /readyz under any name and filters the rest.
///
/// Outside Development an empty list stops the server at startup. A filter
/// that quietly filters nothing because a setting was lost in a deploy is
/// the failure nobody notices; a revision that will not start is the one
/// the deploy gate catches.
///
/// Nothing here builds a link from the Host header that a visitor then
/// trusts — the Twilio callback URL is configured, and Google refuses a
/// redirect URI it was not given — so this is defence in depth against
/// cache poisoning and whatever is written next, not the fix for a hole.
/// </remarks>
public static class HostAllowlist
{
    private static readonly string[] ProbePaths = ["/healthz", "/readyz"];

    public static IApplicationBuilder UseHostAllowlist(this WebApplication app)
    {
        var hosts = (app.Configuration.GetSection(HostAllowlistOptions.Section).Get<HostAllowlistOptions>()
                     ?? new HostAllowlistOptions()).Hosts
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .ToArray();

        if (hosts.Length == 0)
        {
            if (app.Environment.IsDevelopment())
            {
                return app;
            }

            throw new InvalidOperationException(
                $"{HostAllowlistOptions.Section}:Hosts is empty in the {app.Environment.EnvironmentName} environment, "
                + "so the site would answer to any name. List the names it goes by in appsettings.Production.json, "
                + $"or set {HostAllowlistOptions.Section}__Hosts__0, __1, ... in the environment.");
        }

        return app.Use((context, next) =>
        {
            if (Allows(hosts, context.Request.Host.Host)
                || ProbePaths.Any(path => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                return next();
            }

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        });
    }

    /// <summary><paramref name="host"/> is the name alone; HostString has already dropped any port.</summary>
    public static bool Allows(IReadOnlyList<string> allowed, string host) =>
        host.Length > 0 && allowed.Any(entry => entry.StartsWith("*.", StringComparison.Ordinal)
            // "*.example.io" is any subdomain and not the bare domain, and
            // the dot is part of the suffix so "notexample.io" is not one.
            ? host.Length > entry.Length - 1 && host.EndsWith(entry[1..], StringComparison.OrdinalIgnoreCase)
            : string.Equals(host, entry, StringComparison.OrdinalIgnoreCase));
}
