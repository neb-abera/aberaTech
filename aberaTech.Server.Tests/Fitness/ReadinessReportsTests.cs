using aberaTech.Fitness.Api;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The fitness test as the gates see it. An official card from five months
/// ago is still the athlete's record — it was being thrown away as too old,
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
    public void Lifts_are_read_by_name_without_their_variants(string exercise, string lift, bool expected)
    {
        Assert.Equal(expected, ReadinessReports.IsLift(exercise, lift));
    }
}
