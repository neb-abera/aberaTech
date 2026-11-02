namespace aberaTech.Fitness.Domain;

/// <summary>The four events of the FBI special agent Physical Fitness Test.</summary>
public enum FbiEvent
{
    PullUps,
    Sprint300m,
    PushUps,
    Run1_5Mile
}

/// <summary>
/// The FBI special agent Physical Fitness Test, scored on the published
/// tables: −2 to 10 points an event, in the order the test runs them.
/// </summary>
/// <remarks>
/// Transcribed from fbijobs.gov, Physical Requirements, Events 1 to 4, read
/// on 2 November 2026 (<see cref="Citations.FbiPft"/>). Push-ups are untimed
/// and continuous; the test ends when the candidate stops. Times are seconds.
/// Each table runs from 10 points down, so the first row reached is the score.
/// A time row holds the slowest time that still earns its points; a
/// repetition row holds the fewest repetitions that do.
/// </remarks>
public static class Fbi
{
    public const int MaxPoints = 10;

    /// <summary>The score below every table's 0 row.</summary>
    public const int FloorPoints = -2;

    public static IReadOnlyList<FbiEvent> Events { get; } =
        [FbiEvent.PullUps, FbiEvent.Sprint300m, FbiEvent.PushUps, FbiEvent.Run1_5Mile];

    public static bool LowerIsBetter(FbiEvent fbiEvent) =>
        fbiEvent is FbiEvent.Sprint300m or FbiEvent.Run1_5Mile;

    public static string Name(FbiEvent fbiEvent) => fbiEvent switch
    {
        FbiEvent.PullUps => "Pull-ups",
        FbiEvent.Sprint300m => "300-meter sprint",
        FbiEvent.PushUps => "Push-ups",
        FbiEvent.Run1_5Mile => "1.5-mile run",
        _ => fbiEvent.ToString()
    };

    /// <summary>Points for one event.</summary>
    /// <param name="raw">Repetitions for pull-ups and push-ups, seconds for the runs.</param>
    public static int Points(FbiEvent fbiEvent, double raw, bool female)
    {
        if (raw < 0 || !double.IsFinite(raw)) throw new ArgumentOutOfRangeException(nameof(raw));

        var lower = LowerIsBetter(fbiEvent);
        foreach (var (points, threshold) in Table(fbiEvent, female))
        {
            var reached = lower ? raw <= threshold + 1e-9 : raw >= threshold - 1e-9;
            if (reached) return points;
        }

        // Pull-ups have no −2 row: zero repetitions is zero points.
        return fbiEvent == FbiEvent.PullUps ? 0 : FloorPoints;
    }

    private static (int Points, double Threshold)[] Table(FbiEvent fbiEvent, bool female) => (fbiEvent, female) switch
    {
        (FbiEvent.PullUps, false) => [(10, 20), (9, 18), (8, 16), (7, 14), (6, 12), (5, 10), (4, 8), (3, 6), (2, 4), (1, 2), (0, 0)],
        (FbiEvent.PullUps, true) => [(10, 10), (9, 9), (8, 8), (7, 7), (6, 6), (5, 5), (4, 4), (3, 3), (2, 2), (1, 1), (0, 0)],
        (FbiEvent.Sprint300m, false) =>
            [(10, 40.9), (9, 41.9), (8, 42.9), (7, 43.9), (6, 44.9), (5, 46.0), (4, 47.9), (3, 49.4), (2, 51.0), (1, 52.4), (0, 55.0)],
        (FbiEvent.Sprint300m, true) =>
            [(10, 49.9), (9, 50.9), (8, 51.9), (7, 52.9), (6, 53.9), (5, 55.9), (4, 57.4), (3, 59.9), (2, 62.4), (1, 64.9), (0, 67.4)],
        (FbiEvent.PushUps, false) => [(10, 71), (9, 65), (8, 61), (7, 57), (6, 54), (5, 50), (4, 44), (3, 40), (2, 33), (1, 30), (0, 20)],
        (FbiEvent.PushUps, true) => [(10, 45), (9, 42), (8, 39), (7, 36), (6, 33), (5, 30), (4, 27), (3, 22), (2, 19), (1, 14), (0, 5)],
        (FbiEvent.Run1_5Mile, false) =>
            [(10, 539), (9, 559), (8, 574), (7, 594), (6, 614), (5, 634), (4, 669), (3, 694), (2, 734), (1, 744), (0, 809)],
        (FbiEvent.Run1_5Mile, true) =>
            [(10, 634), (9, 644), (8, 665), (7, 674), (6, 694), (5, 716), (4, 749), (3, 779), (2, 814), (1, 839), (0, 899)],
        _ => throw new ArgumentOutOfRangeException(nameof(fbiEvent))
    };
}
