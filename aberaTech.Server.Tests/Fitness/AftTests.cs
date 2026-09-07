using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The scorer against rows read straight off the published tables, so a
/// transcription error anywhere in the men's 32-36 column — the one this
/// athlete is scored on — fails here rather than on a scorecard.
/// </summary>
public sealed class AftTests
{
    private const int Age = 33;
    private const double Lb = 1 / 2.2046226218;

    [Theory]
    [InlineData(350, 100)]
    [InlineData(340, 99)]
    [InlineData(335, 97)] // 335 reaches the 330 row, not the 340 one.
    [InlineData(140, 60)]
    [InlineData(139, 50)] // below sixty the scale steps by tens
    [InlineData(80, 0)]
    [InlineData(79, 0)]
    public void Deadlift_scores_off_the_men_32_36_column(double pounds, int expected)
    {
        Assert.Equal(expected, Aft.Points(AftEvent.Deadlift, pounds, Age, female: false));
    }

    [Theory]
    [InlineData(60, 100)]
    [InlineData(13, 60)]
    [InlineData(12, 50)]
    [InlineData(4, 0)]
    public void Push_ups_score_off_the_men_32_36_column(double reps, int expected)
    {
        Assert.Equal(expected, Aft.Points(AftEvent.HandReleasePushUps, reps, Age, female: false));
    }

    [Theory]
    [InlineData(93, 100)] // 1:33
    [InlineData(156, 60)] // 2:36
    [InlineData(157, 59)]
    [InlineData(216, 0)] // 3:36
    [InlineData(300, 0)] // slower than the zero row is still zero
    public void Sprint_drag_carry_is_faster_is_better(double seconds, int expected)
    {
        Assert.Equal(expected, Aft.Points(AftEvent.SprintDragCarry, seconds, Age, female: false));
    }

    [Theory]
    [InlineData(205, 100)] // 3:25
    [InlineData(75, 60)] // 1:15
    [InlineData(74, 58)] // the 59 row is empty; 1:14 lands on 58
    public void Plank_skips_the_empty_rows(double seconds, int expected)
    {
        Assert.Equal(expected, Aft.Points(AftEvent.Plank, seconds, Age, female: false));
    }

    [Theory]
    [InlineData(822, 100)] // 13:42
    [InlineData(1244, 60)] // 20:44
    [InlineData(1412, 0)] // 23:32
    public void Two_mile_scores_off_the_men_32_36_column(double seconds, int expected)
    {
        Assert.Equal(expected, Aft.Points(AftEvent.TwoMileRun, seconds, Age, female: false));
    }

    [Fact]
    public void Women_score_on_their_own_column()
    {
        Assert.Equal(100, Aft.Points(AftEvent.HandReleasePushUps, 47, Age, female: true));
        Assert.Equal(100, Aft.Points(AftEvent.Deadlift, 230, Age, female: true));
    }

    [Theory]
    [InlineData(17, 0)]
    [InlineData(21, 0)]
    [InlineData(22, 1)]
    [InlineData(36, 3)]
    [InlineData(37, 4)]
    [InlineData(61, 8)]
    [InlineData(62, 9)]
    [InlineData(80, 9)]
    public void Age_bands_follow_the_table(int age, int band)
    {
        Assert.Equal(band, Aft.AgeBandIndex(age));
    }

    [Fact]
    public void Raw_for_a_points_target_is_the_row_that_earns_it()
    {
        Assert.Equal(140, Aft.RawFor(AftEvent.Deadlift, 60, Age, false));
        Assert.Equal(1244, Aft.RawFor(AftEvent.TwoMileRun, 60, Age, false));
        // The 59 plank row is empty, so 59 points needs the 60 row.
        Assert.Equal(75, Aft.RawFor(AftEvent.Plank, 59, Age, false));
    }

    [Fact]
    public void Combat_standard_needs_sixty_everywhere_and_350_in_total()
    {
        var floors = Aft.Score(
            new AftPerformance(140 * Lb, 13, 156, 75, 1244), Age, female: false);
        Assert.Equal(300, floors.Total);
        Assert.False(floors.MeetsCombatStandard);

        var seventy = Aft.Score(
            new AftPerformance(190 * Lb, 26, 130, 107, 1110), Age, female: false);
        Assert.Equal(350, seventy.Total);
        Assert.True(seventy.MeetsCombatStandard);

        // 350 in total with one event under sixty is still a fail.
        var lopsided = Aft.Score(
            new AftPerformance(350 * Lb, 60, 93, 205, 1300), Age, female: false);
        Assert.True(lopsided.Total >= 350);
        Assert.False(lopsided.MeetsCombatStandard);
    }

    [Fact]
    public void Explanation_names_every_event_and_the_standard()
    {
        var score = Aft.Score(new AftPerformance(300 * Lb, 45, 120, 180, 900), Age, female: false);

        var steps = Aft.Explain(score);

        Assert.Equal(6, steps.Count);
        Assert.All(steps, s => Assert.Equal(Citations.ArmyAft.Id, s.CitationId));
        Assert.Contains(steps, s => s.Label == "Two-mile run" && s.Expression.Contains("15:00"));
        Assert.Contains(steps, s => s.Label == "Combat standard" && s.Value == "met");
    }
}
