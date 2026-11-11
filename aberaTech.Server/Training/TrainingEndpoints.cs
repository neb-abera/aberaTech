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
    /// <summary>Drill runs, per address. Each costs Compiler Explorer one call per test.</summary>
    public const string DrillPolicy = "training-drills";

    public const int DefaultDrillsPerMinute = 10;

    /// <summary>The band above the current rating that practice is drawn from: the plan's rating plus 0 to 200.</summary>
    public const int BandWidth = 200;

    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/training/solves", async (string? codeforces, string? atcoder, JudgeReader judges, TimeProvider time, IServiceProvider services, CancellationToken cancellationToken) =>
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
                // Sample questions under `make e2e` only (FakeAssessment.cs).
                fixture = services.GetService<FakeAssessment>()?.Fixture,
            });
        })
        .RequireAuthorization(AdminAuth.PolicyName)
        .WithTags("Training");

        // Problems in the owner's band, rating to rating + 200, for the
        // pool's slots that Mostafa Saad's sheet cannot fill: Codeforces by
        // rating, and AtCoder by AtCoder Problems difficulty on its own scale.
        routes.MapGet("/api/training/band", async (int? rating, JudgeReader judges, CancellationToken cancellationToken) =>
        {
            if (rating is not { } from || from < 0 || from > 4000)
                return Results.BadRequest("A rating is a whole number from 0 to 4000.");
            var codeforcesTask = judges.BandAsync(from, from + BandWidth, cancellationToken);
            var atcoderTask = judges.AtCoderBandAsync(from, from + BandWidth, cancellationToken);
            await Task.WhenAll(codeforcesTask, atcoderTask);
            var answer = codeforcesTask.Result;
            var atcoder = atcoderTask.Result;
            return Results.Ok(new
            {
                ok = answer.Ok,
                error = answer.Error,
                from = answer.From,
                to = answer.To,
                readAt = answer.ReadAt,
                problems = answer.Problems.Select(problem => new { id = problem.Id, name = problem.Name, rating = problem.Rating, url = problem.Url }),
                atcoder = new
                {
                    ok = atcoder.Ok,
                    error = atcoder.Error,
                    from = atcoder.From,
                    to = atcoder.To,
                    readAt = atcoder.ReadAt,
                    problems = atcoder.Problems.Select(problem => new { id = problem.Id, contest = problem.Contest, index = problem.Index, name = problem.Name, difficulty = problem.Difficulty, url = problem.Url }),
                },
            });
        })
        .RequireAuthorization(AdminAuth.PolicyName)
        .WithTags("Training");

        // A drill's C++ goes to Compiler Explorer, which compiles and runs
        // it. Nothing runs here. With Training:GraderUrl set it goes through
        // the Rust grader, and without it this process calls Compiler
        // Explorer itself. Owner only, and ten runs a minute.
        routes.MapPost("/api/training/drills/run", async (DrillRun run, DrillRunner runner, GraderClient grader, CancellationToken cancellationToken) =>
        {
            if (DrillRunner.Invalid(run) is { } problem) return Results.BadRequest(problem);
            return Results.Ok(grader.IsConfigured
                ? await grader.RunAsync(run, cancellationToken)
                : await runner.RunAsync(run, cancellationToken));
        })
        .RequireAuthorization(AdminAuth.PolicyName)
        .RequireRateLimiting(DrillPolicy)
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
