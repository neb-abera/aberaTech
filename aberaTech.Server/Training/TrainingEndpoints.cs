using aberaTech.Scheduling.Admin;

namespace aberaTech.Server.Training;

/// <summary>
/// /api/training/solves: what Codeforces and AtCoder Problems say about the
/// owner's handles, read live, and the upcoming Codeforces rounds. Owner
/// only, because the page that asks is the owner's. The handles come in the
/// query: they are public names, not personal data.
/// </summary>
public static class TrainingEndpoints
{
    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/training/solves", async (string? codeforces, string? atcoder, JudgeReader judges, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrEmpty(codeforces) && !JudgeReader.IsHandle(codeforces))
                return Results.BadRequest("A Codeforces handle is 3 to 24 letters, digits, dots, hyphens, or underscores.");
            if (!string.IsNullOrEmpty(atcoder) && !JudgeReader.IsHandle(atcoder))
                return Results.BadRequest("An AtCoder handle is 3 to 24 letters, digits, dots, hyphens, or underscores.");

            var codeforcesTask = string.IsNullOrEmpty(codeforces) ? Task.FromResult<JudgeAnswer?>(null) : Answer(judges.CodeforcesAsync(codeforces, cancellationToken));
            var atcoderTask = string.IsNullOrEmpty(atcoder) ? Task.FromResult<JudgeAnswer?>(null) : Answer(judges.AtCoderAsync(atcoder, cancellationToken));
            var contestsTask = judges.UpcomingAsync(cancellationToken);
            await Task.WhenAll(codeforcesTask, atcoderTask, contestsTask);

            return Results.Ok(new
            {
                configured = true,
                readAt = time.GetUtcNow(),
                codeforces = Shape(codeforcesTask.Result),
                atcoder = Shape(atcoderTask.Result),
                contests = contestsTask.Result,
            });
        })
        .RequireAuthorization(AdminAuth.PolicyName)
        .WithTags("Training");

        return routes;
    }

    private static async Task<JudgeAnswer?> Answer(Task<JudgeAnswer> task) => await task;

    private static object? Shape(JudgeAnswer? answer) => answer is null
        ? null
        : new
        {
            handle = answer.Handle,
            ok = answer.Ok,
            error = answer.Error,
            rating = answer.Rating,
            maxRating = answer.MaxRating,
            rank = answer.Rank,
            ratedContests = answer.RatedContests,
            solved = answer.Solved.ToDictionary(pair => pair.Key, pair => pair.Value),
            readAt = answer.ReadAt,
        };

    /// <summary>Deployed without sign-in: the page asks and is told so. Anonymous because it discloses nothing.</summary>
    public static IEndpointRouteBuilder MapTrainingUnavailable(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/training/solves", () => Results.Ok(new { configured = false })).AllowAnonymous();
        return routes;
    }
}
