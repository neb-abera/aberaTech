using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class ReadinessTests
{
    private static Dictionary<string, Measurement> Measurements(params (string Metric, double Value)[] values) =>
        values.ToDictionary(
            v => v.Metric,
            v => new Measurement(v.Metric, v.Value, Basis.Measured, "test", new LocalDate(2026, 9, 1)));

    [Fact]
    public void Every_gate_cites_a_source_for_every_line()
    {
        var ids = Citations.All.Select(c => c.Id).ToHashSet();
        Assert.All(SelectionReadiness.Gates, gate =>
        {
            Assert.NotEmpty(gate.Requirements);
            Assert.All(gate.Requirements, r => Assert.Contains(r.CitationId, ids));
        });
    }

    [Fact]
    public void Own_standards_carry_every_essential_row_of_the_workbook()
    {
        var own = SelectionReadiness.Gates.Single(g => g.Id == "own-standards");

        (string Metric, double Target)[] expected =
        [
            (SelectionReadiness.Metrics.RunFiveMile, 34 * 60),
            (SelectionReadiness.Metrics.RunTwoMile, (12 * 60) + 15),
            (SelectionReadiness.Metrics.RuckTwelveMileAt35, (2 * 3600) + (30 * 60)),
            ("sprint-drag-carry", 90),
            (SelectionReadiness.Metrics.Plank, 210)
        ];

        Assert.All(expected, line =>
        {
            var requirement = Assert.Single(own.Requirements, r => r.Metric == line.Metric);
            Assert.Equal(line.Target, requirement.Target);
            Assert.Equal(Citations.NebFitnessGoals.Id, requirement.CitationId);
        });
        Assert.Equal(14, own.Requirements.Count);
    }

    [Fact]
    public void Own_standards_fall_due_at_the_end_of_2027_whatever_the_selection_date()
    {
        foreach (var selection in new LocalDate?[] { null, new LocalDate(2029, 3, 1) })
        {
            var own = Assert.Single(SelectionReadiness.Evaluate(Measurements(), selection), r => r.Gate.Id == "own-standards");
            Assert.Equal(new LocalDate(2027, 12, 31), own.DueOn);
        }
    }

    [Fact]
    public void Gates_count_back_from_the_selection_date()
    {
        var results = SelectionReadiness.Evaluate(Measurements(), new LocalDate(2028, 4, 1));

        var dayOne = Assert.Single(results, r => r.Gate.Id == "sfas-day-one");
        // 2028 is a leap year, so 52 weeks before 1 April 2028 is 3 April 2027.
        Assert.Equal(new LocalDate(2027, 4, 3), dayOne.DueOn);

        var competitive = Assert.Single(results, r => r.Gate.Id == "sfas-competitive");
        Assert.Equal(new LocalDate(2028, 4, 1), competitive.DueOn);

        Assert.All(SelectionReadiness.Evaluate(Measurements(), null).Where(r => r.Gate.DueDate is null), r => Assert.Null(r.DueOn));
    }

    [Fact]
    public void A_gate_fails_on_any_miss_and_stays_unknown_while_lines_are_unmeasured()
    {
        var short2mile = Measurements(
            (SelectionReadiness.Metrics.RunTwoMile, 16 * 60),
            (SelectionReadiness.Metrics.HandReleasePushUps, 45),
            (SelectionReadiness.Metrics.PullUps, 12));
        var dayOne = SelectionReadiness.Evaluate(short2mile, null).Single(r => r.Gate.Id == "sfas-day-one");
        Assert.Equal(GateStatus.Fail, dayOne.Status);
        Assert.Equal(2, dayOne.Passed);
        Assert.Equal(3, dayOne.Known);

        var run = dayOne.Requirements.Single(r => r.Requirement.Metric == SelectionReadiness.Metrics.RunTwoMile);
        Assert.Equal(GateStatus.Fail, run.Status);
        Assert.Equal("short by 0:48", run.Gap);

        var partial = SelectionReadiness.Evaluate(
            Measurements((SelectionReadiness.Metrics.RunTwoMile, 14 * 60), (SelectionReadiness.Metrics.PullUps, 12)), null)
            .Single(r => r.Gate.Id == "sfas-day-one");
        Assert.Equal(GateStatus.Unknown, partial.Status);
        Assert.Equal(2, partial.Known);
        Assert.Contains(partial.Requirements, r => r.Status == GateStatus.Unknown && r.Gap.Contains("nothing in the log"));

        var all = SelectionReadiness.Evaluate(
            Measurements(
                (SelectionReadiness.Metrics.RunTwoMile, 14 * 60),
                (SelectionReadiness.Metrics.HandReleasePushUps, 45),
                (SelectionReadiness.Metrics.PullUps, 12)), null)
            .Single(r => r.Gate.Id == "sfas-day-one");
        Assert.Equal(GateStatus.Pass, all.Status);
        Assert.Equal("clear by 1:12", all.Requirements.Single(r => r.Requirement.Metric == SelectionReadiness.Metrics.RunTwoMile).Gap);
    }

    [Fact]
    public void The_readiness_evaluation_gate_is_the_one_with_untracked_lines()
    {
        var sfre = SelectionReadiness.Gates.Single(g => g.Id == "sfre-prerequisites");
        Assert.Contains("100 m swim", sfre.Untracked);
        Assert.Contains(sfre.Requirements, r => r.Metric == SelectionReadiness.Metrics.RuckTwelveMileAt45 && r.Target == 3 * 3600);
    }

    [Theory]
    [InlineData(SelectionReadiness.Units.Seconds, 9900, "2:45:00")]
    [InlineData(SelectionReadiness.Units.Reps, 12, "12 reps")]
    [InlineData(SelectionReadiness.Units.Bodyweights, 1.5, "1.50× bodyweight")]
    [InlineData(SelectionReadiness.Units.Pounds, 340, "340 lb (154 kg)")]
    [InlineData(SelectionReadiness.Units.Percent, 14.5, "14.5%")]
    [InlineData(SelectionReadiness.Units.SecondsPerMile, 480, "8:00/mile")]
    [InlineData(SelectionReadiness.Units.Meters, 100, "100 m")]
    public void Values_read_in_their_own_units(string unit, double value, string expected)
    {
        Assert.Equal(expected, SelectionReadiness.Describe(unit, value));
    }
}
