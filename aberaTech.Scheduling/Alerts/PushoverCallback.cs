using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// Pushover's acknowledgement callback (pushover.net/api/receipts, "Callback
/// URL"). When the owner acknowledges an alarm in the Pushover app, Pushover
/// posts the receipt here, and the occurrence counts as acknowledged
/// everywhere: the browser stops ringing and the phones are pushed.
/// </summary>
/// <remarks>
/// Anonymous, because Pushover has no session and signs nothing. So the body
/// is never trusted alone. The receipt must be one this server stored for a
/// send, and Pushover's own receipts API must say it was acknowledged. A
/// forged post can at most make the server ask Pushover about a receipt it
/// already holds. Rate limited, and a wrong receipt counts against the
/// failure limit (RateLimits.cs).
/// </remarks>
public static class PushoverCallback
{
    public const string Path = "/api/alerts/pushover/acknowledged";

    public const string Policy = "alerts-pushover-callback";

    /// <summary>Callbacks a minute per address. Pushover sends one per acknowledgement, and one more a minute while it gets no 2xx.</summary>
    public const int DefaultPerMinute = 30;

    public static IEndpointRouteBuilder MapPushoverCallback(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(Path, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Policy)
            .WithTags("Alerts");
        return routes;
    }

    /// <summary>
    /// 200 {"acknowledged":true} once the acknowledgement is recorded, or was
    /// already. 400 for a body that is not a receipt, 404 for a receipt this
    /// server did not send, 403 when Pushover says it is not acknowledged,
    /// and 503 when Pushover cannot be asked, so it posts again in a minute.
    /// </summary>
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IAlertStore store,
        PushoverClient pushover,
        AlertPushWorker pushes,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType) return Results.BadRequest();

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest();
        }

        var receipt = form["receipt"];
        if (receipt.Count != 1 || !PushoverClient.IsReceipt(receipt[0])) return Results.BadRequest();

        var delivery = await store.DeliveryByReceiptAsync(receipt[0]!, cancellationToken);
        if (delivery is null) return Results.NotFound();

        // A second call, or one after the phone or a browser answered: nothing more to do.
        if (await store.IsAcknowledgedAsync(delivery.Key, cancellationToken)) return Acknowledged();

        switch (await pushover.IsAcknowledgedAsync(receipt[0]!, cancellationToken))
        {
            case null:
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            case false:
                return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (await store.AcknowledgeAsync(
                delivery.Key, delivery.StartsAt, AlertAcknowledgementVia.Pushover, clock.GetCurrentInstant(), cancellationToken))
        {
            await pushes.PlanChangedAsync(cancellationToken);
        }

        return Acknowledged();
    }

    private static IResult Acknowledged() => Results.Ok(new { acknowledged = true });
}
