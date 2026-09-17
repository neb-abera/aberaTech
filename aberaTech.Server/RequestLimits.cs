using aberaTech.Fitness.Api;
using Microsoft.AspNetCore.Http.Features;

namespace aberaTech.Server;

/// <summary>
/// How much a request may carry, said deliberately rather than inherited.
/// </summary>
/// <remarks>
/// Kestrel's default body ceiling is 30 MB and the form reader's is 128 MB
/// across 1,024 values — sized for an application that accepts uploads from
/// everyone. Here the public routes take a name, a phone number and an
/// address, and the one webhook takes about twenty short fields. The two
/// routes with a real need are the owner's, and each raises its own ceiling:
/// the document store here, and the importer in its handler, after
/// authorization has already run.
/// </remarks>
public static class RequestLimits
{
    /// <summary>Every body, unless a route says otherwise. A booking is under 1 KB.</summary>
    public const long MaxBodyBytes = 64 * 1024;

    /// <summary>Headroom over the document cap, so the route's own 413 is the one a too-large document meets.</summary>
    private const long DocumentBodyBytes = ProgressEndpoints.MaxBytes * 2;

    public static WebApplicationBuilder AddRequestLimits(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxBodyBytes);

        // Twilio's status callback is the only form this app reads: around
        // twenty fields, none longer than a message SID or a URL.
        builder.Services.Configure<FormOptions>(form =>
        {
            form.ValueCountLimit = 64;
            form.KeyLengthLimit = 128;
            form.ValueLengthLimit = 4 * 1024;
            form.BufferBodyLengthLimit = MaxBodyBytes;
            form.MultipartBodyLengthLimit = MaxBodyBytes;
        });

        return builder;
    }

    /// <summary>The per-route raises that are not made by the route itself.</summary>
    public static IApplicationBuilder UseRequestLimits(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (HttpMethods.IsPut(context.Request.Method)
                && context.Request.Path.StartsWithSegments("/api/progress")
                && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = DocumentBodyBytes;
            }

            return next();
        });
}
