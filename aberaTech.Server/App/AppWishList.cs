using System.Text.Json;
using aberaTech.Server.Playbook;

namespace aberaTech.Server.App;

public sealed record AppWishItemDto(
    string Id,
    string Item,
    string? Pick,
    double? Price,
    string? Link,
    string? Category,
    string? BuyWindow,
    string? Deadline,
    string? Status);

/// <summary>
/// Neb's wish list for the app, read-only and live from Notion: what is
/// still wanted, best score first. Items bought, skipped, or marked Do not
/// buy are left out, and so are the case against and the scoring inputs.
/// </summary>
public static class AppWishList
{
    private static readonly string[] Finished = ["Bought", "Skip"];

    public static IEndpointRouteBuilder MapAppWishListEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/app/wishlist", async (NotionClient notion, AppOptions app, CancellationToken cancellationToken) =>
            {
                if (!NotionIds.TryNormalize(app.WishListDataSourceId, out var dataSource))
                {
                    return Results.Problem(statusCode: 503, title: "The wish list is not connected.");
                }

                IReadOnlyList<JsonElement> pages;
                try
                {
                    pages = await notion.QueryAsync(dataSource, cancellationToken);
                }
                catch (Exception exception) when (exception is NotionException or NotionThrottledException or HttpRequestException)
                {
                    return Results.Problem(statusCode: 502, title: "Notion did not answer. Try again.");
                }

                return Results.Ok(Wanted(pages));
            })
            .RequireAuthorization(AppAuth.SignedInPolicy);

        return routes;
    }

    internal static IReadOnlyList<AppWishItemDto> Wanted(IEnumerable<JsonElement> pages) =>
        pages
            .Select(page => (Page: page, Score: Score(page)))
            .Where(x => !Finished.Contains(AppRequests.Select(x.Page, "Status"))
                        && AppRequests.Select(x.Page, "Buy window") != "Do not buy")
            .OrderByDescending(x => x.Score ?? double.MinValue)
            .Select(x => new AppWishItemDto(
                x.Page.GetProperty("id").GetString() ?? "",
                AppRequests.Text(x.Page, "Item") ?? "(untitled)",
                AppRequests.Text(x.Page, "Pick"),
                AppRequests.Number(x.Page, "Price"),
                AppRequests.Url(x.Page, "Buy link"),
                AppRequests.Select(x.Page, "Category"),
                AppRequests.Select(x.Page, "Buy window"),
                AppRequests.DateStart(x.Page, "Deadline"),
                AppRequests.Select(x.Page, "Status")))
            .ToList();

    private static double? Score(JsonElement page) =>
        page.TryGetProperty("properties", out var props) && props.TryGetProperty("Score", out var score)
        && score.TryGetProperty("formula", out var formula) && formula.TryGetProperty("number", out var number)
        && number.ValueKind == JsonValueKind.Number
            ? number.GetDouble()
            : null;
}
