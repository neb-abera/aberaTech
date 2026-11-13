using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The fitness test as the gates see it. An official card from five months
/// ago is still the athlete's record. It was being thrown away as too old,
/// which left the combat-standard gate unknown with a 428 sitting in the
/// database.
/// </summary>
public sealed class ReadinessReportsTests
{
    private static readonly AthleteSettings Athlete = new() { Id = 1, BirthYear = 1991 };

    private static AftResultDto Card(LocalDate date) =>
        ReadinessReports.Score(
            new AftResult
            {
                Id = Guid.NewGuid(),
                Date = date,
                DeadliftKg = 300 / BodyMass.PoundsPerKg,
                HandReleasePushUps = 44,
                SprintDragCarrySeconds = 104,
                PlankSeconds = 120,
                TwoMileSeconds = 16 * 60 + 49
            },
            Athlete,
            new LocalDate(2026, 9, 7));

    [Fact]
    public void A_months_old_test_still_scores_the_gates_and_says_its_age()
    {
        var measurements = new Dictionary<string, Measurement>();
        var weight = new BodyMetric { Id = Guid.NewGuid(), Date = new LocalDate(2026, 8, 30), WeightKg = 79.4 };

        ReadinessReports.AftMeasurements(
            [Card(new LocalDate(2026, 4, 3))], since: new LocalDate(2026, 5, 10), weight, measurements);

        Assert.Equal(428, measurements[SelectionReadiness.Metrics.AftTotal].Value);
        Assert.Equal(74, measurements[SelectionReadiness.Metrics.AftLowestEvent].Value);
        Assert.Equal(300, measurements[SelectionReadiness.Metrics.DeadliftTripleLb].Value, precision: 6);
        Assert.Equal(44, measurements[SelectionReadiness.Metrics.HandReleasePushUps].Value);
        Assert.Equal(300 / BodyMass.PoundsPerKg / 79.4, measurements[SelectionReadiness.Metrics.DeadliftTripleToBodyweight].Value, precision: 6);
        Assert.Contains("days old", measurements[SelectionReadiness.Metrics.AftTotal].Evidence);
        Assert.All(measurements.Values, m => Assert.Equal(Basis.Measured, m.Basis));
    }

    [Fact]
    public void A_recent_test_carries_no_age_warning_and_does_not_undercut_a_better_set()
    {
        var measurements = new Dictionary<string, Measurement>
        {
            [SelectionReadiness.Metrics.HandReleasePushUps] = new(
                SelectionReadiness.Metrics.HandReleasePushUps, 50, Basis.Measured, "best set", new LocalDate(2026, 8, 20))
        };

        ReadinessReports.AftMeasurements(
            [Card(new LocalDate(2026, 8, 1))], since: new LocalDate(2026, 5, 10), null, measurements);

        Assert.DoesNotContain("days old", measurements[SelectionReadiness.Metrics.AftTotal].Evidence);
        Assert.Equal(50, measurements[SelectionReadiness.Metrics.HandReleasePushUps].Value);
        Assert.False(measurements.ContainsKey(SelectionReadiness.Metrics.DeadliftTripleToBodyweight));
    }

    private static DatedSet Bench(LocalDate date, double pounds, int reps) => new(
        new StrengthSet
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Exercise = "Bench Press (Barbell)",
            WeightKg = pounds / BodyMass.PoundsPerKg,
            Reps = reps,
        },
        date.AtMidnight().InUtc().ToInstant());

    [Fact]
    public void A_lift_names_the_set_it_came_from_and_a_light_day_does_not_drag_its_trend()
    {
        // Neb's bench, 2026-11: 155 lb for 10 is a 207 lb single by Epley.
        DatedSet[] sets =
        [
            Bench(new LocalDate(2026, 10, 20), 150, 9),
            Bench(new LocalDate(2026, 10, 31), 135, 8),
            Bench(new LocalDate(2026, 11, 3), 155, 10),
            Bench(new LocalDate(2026, 11, 8), 135, 15),
            Bench(new LocalDate(2026, 11, 8), 155, 9),
            Bench(new LocalDate(2026, 11, 11), 155, 10),
        ];
        var measurements = new Dictionary<string, Measurement>();
        var histories = new Dictionary<string, List<DatedValue>>();

        ReadinessReports.Strength(
            sets, DateTimeZone.Utc, null, new LocalDate(2026, 7, 15), measurements, histories);

        var bench = measurements[SelectionReadiness.Metrics.BenchPressLb];
        Assert.Equal(155 * (1 + 10 / 30.0), bench.Value, 6);
        // The newest of the equal sets, named with its reps.
        Assert.Equal(new LocalDate(2026, 11, 11), bench.On);
        Assert.StartsWith("155 lb × 10 on 2026-11-11, an Epley one-rep max of 207 lb", bench.Evidence);
        Assert.Contains("above 10 reps are not counted", bench.Evidence);

        // The 31 Oct day of 135 lb sets reads as the best of its four weeks,
        // 150 lb x 9 on 20 Oct, not as a 171 lb max.
        var history = histories[SelectionReadiness.Metrics.BenchPressLb];
        var light = history.Single(x => x.Date == new LocalDate(2026, 10, 31));
        Assert.Equal(150 * (1 + 9 / 30.0), light.Value, 6);
        var after = history.Where(x => x.Date >= new LocalDate(2026, 11, 3)).Select(x => x.Value);
        Assert.All(after, v => Assert.Equal(155 * (1 + 10 / 30.0), v, 6));
    }

    [Theory]
    [InlineData("Squat (Barbell)", "back squat", true)]
    [InlineData("Back Squat", "back squat", true)]
    [InlineData("Front Squat (Barbell)", "back squat", false)]
    [InlineData("Bulgarian Split Squat", "back squat", false)]
    [InlineData("Goblet Squat", "back squat", false)]
    [InlineData("Front Squat (Barbell)", "front squat", true)]
    [InlineData("Bench Press (Barbell)", "bench press", true)]
    [InlineData("Incline Bench Press (Dumbbell)", "bench press", false)]
    [InlineData("Deadlift (Barbell)", "deadlift", true)]
    [InlineData("Romanian Deadlift (Barbell)", "deadlift", false)]
    [InlineData("Straight Leg Deadlift", "deadlift", false)]
    [InlineData("RDL (Dumbbell)", "deadlift", false)]
    public void Lifts_are_read_by_name_without_their_variants(string exercise, string lift, bool expected)
    {
        Assert.Equal(expected, ReadinessReports.IsLift(exercise, lift));
    }
}
