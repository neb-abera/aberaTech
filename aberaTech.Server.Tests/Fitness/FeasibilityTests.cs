using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The regression this whole model exists to fix: three goals that fail for
/// three different reasons used to come back with one sentence about elite
/// volume, and a 20:00 five-mile — faster than any human has run — came back
/// with a training plan of 45.8 hours a week.
/// </summary>
public sealed class FeasibilityTests
{
    private const double Mile = 1609.344;

    private static AthleteContext Athlete(
        double startVdot = 37,
        double availableHours = 7,
        int? age = 33,
        double currentHours = 4) =>
        new(
            new TrajectoryParameters(startVdot),
            NoHistory(startVdot),
            DoseResponse.Allocate(currentHours).Dose,
            availableHours,
            age);

    private static FitResult NoHistory(double startVdot) =>
        ModelFit.Fit([], new ModelFit.Priors(startVdot));

    private static FeasibilityReport Assess(double miles, string time, double months, AthleteContext? athlete = null)
    {
        var parts = time.Split(':').Select(double.Parse).ToArray();
        var seconds = parts.Length == 3
            ? parts[0] * 3600 + parts[1] * 60 + parts[2]
            : parts[0] * 60 + parts[1];
        return Feasibility.Assess(athlete ?? Athlete(), new GoalRequest(miles * Mile, seconds, months));
    }

    [Fact]
    public void A_time_no_human_has_run_is_named_as_such_not_priced_in_hours()
    {
        var report = Assess(5, "20:00", 24);

        Assert.Equal(FeasibilityVerdict.PastTheWorldRecord, report.Verdict);
        Assert.Null(report.Prescription);
        Assert.Contains("No human has run this", report.Headline);
        Assert.Contains("record", report.BindingConstraint);

        // And it says what the record actually is for the distance asked about.
        Assert.InRange(report.AchievableSecondsByDate!.Value, 20 * 60, 24 * 60);
    }

    [Fact]
    public void Each_kind_of_impossible_gets_its_own_answer()
    {
        // Five goals over the same distance, failing — or not — for five
        // different reasons. Before this model they shared one sentence.
        var reports = new[]
        {
            Assess(5, "20:00", 24),   // past the record book
            Assess(5, "24:30", 24),   // inside human range, past any week this athlete could train
            Assess(5, "30:30", 6),    // trainable, but not in six months
            Assess(5, "31:30", 24),   // trainable by the date, on hours they have not got
            Assess(5, "36:00", 24)    // trainable on the hours available
        };

        Assert.Equal(
            [
                FeasibilityVerdict.PastTheWorldRecord,
                FeasibilityVerdict.PastAnyTrainingCeiling,
                FeasibilityVerdict.NotByThatDate,
                FeasibilityVerdict.MoreHoursThanYouHave,
                FeasibilityVerdict.Reachable
            ],
            reports.Select(r => r.Verdict));

        Assert.Equal(5, reports.Select(r => r.Headline).Distinct().Count());
        Assert.Equal(5, reports.Select(r => r.BindingConstraint).Distinct().Count());
    }

    [Fact]
    public void An_unreachable_goal_still_says_what_is_reachable()
    {
        var report = Assess(5, "24:30", 24);

        Assert.Equal(FeasibilityVerdict.PastAnyTrainingCeiling, report.Verdict);
        Assert.NotNull(report.AchievableSecondsByDate);
        Assert.True(report.AchievableSecondsByDate < 30 * 60);
        Assert.Contains("tops out", report.Detail);
    }

    [Fact]
    public void A_deadline_that_is_too_close_reports_the_earliest_date_instead()
    {
        var report = Assess(5, "30:30", 6);

        Assert.Equal(FeasibilityVerdict.NotByThatDate, report.Verdict);
        Assert.NotNull(report.EarliestMonths);
        Assert.True(report.EarliestMonths > 6);
        Assert.Contains("earliest", report.Headline);
    }

    [Fact]
    public void A_reachable_goal_comes_back_as_hours_by_zone()
    {
        var report = Assess(5, "36:00", 24);

        Assert.Equal(FeasibilityVerdict.Reachable, report.Verdict);
        var dose = report.Prescription!.Dose;

        Assert.True(dose.EasyHours > dose.ThresholdHours + dose.IntervalHours);
        Assert.InRange(dose.EasyShare, 0.65, 0.92);
        Assert.Equal(dose.RunningHours, dose.EasyHours + dose.ThresholdHours + dose.IntervalHours, precision: 9);
        Assert.True(report.Prescription.RampMonths > 0);
        Assert.InRange(report.ProbabilityByDate, 0, 1);
    }

    [Fact]
    public void Needing_more_hours_than_the_athlete_has_is_its_own_answer()
    {
        var report = Assess(5, "31:30", 24);

        Assert.Equal(FeasibilityVerdict.MoreHoursThanYouHave, report.Verdict);
        Assert.Contains("7.0", report.Headline);
        Assert.Contains("h/week", report.BindingConstraint);
        Assert.NotNull(report.Prescription);
        Assert.NotNull(report.AchievableSecondsByDate);
    }

    [Fact]
    public void Age_moves_the_line_between_hard_and_impossible()
    {
        // The same time graded against two record books: inside the human
        // range at 33, past the age-graded record at 80.
        var young = Assess(5, "22:30", 36, Athlete(age: 33));
        var old = Assess(5, "22:30", 36, Athlete(age: 80));

        Assert.NotEqual(FeasibilityVerdict.PastTheAgeGradedRecord, young.Verdict);
        Assert.Equal(FeasibilityVerdict.PastTheAgeGradedRecord, old.Verdict);
        Assert.True(old.Grade > young.Grade);
    }

    [Fact]
    public void A_goal_already_met_says_so()
    {
        var report = Assess(2, "20:00", 12);
        Assert.Equal(FeasibilityVerdict.AlreadyThere, report.Verdict);
    }

    [Fact]
    public void Every_answer_ships_the_arithmetic_that_produced_it()
    {
        foreach (var report in new[] { Assess(5, "20:00", 24), Assess(2, "14:30", 18), Assess(5, "30:30", 6) })
        {
            Assert.NotEmpty(report.Steps);
            Assert.All(report.Steps, step =>
            {
                Assert.False(string.IsNullOrWhiteSpace(step.Expression));
                Assert.False(string.IsNullOrWhiteSpace(step.Value));
            });

            // Anything with a citation key names one that exists.
            Assert.All(
                report.Steps.Where(s => s.CitationId is not null),
                step => Assert.Contains(Citations.All, c => c.Id == step.CitationId));
        }
    }

    [Fact]
    public void The_target_score_is_the_one_the_race_equivalency_gives()
    {
        var report = Assess(2, "14:00", 18);
        Assert.Equal(Vdot.FromRace(2 * Mile, 14), report.TargetVdot, precision: 6);
    }

    [Fact]
    public void Altitude_makes_the_same_clock_time_a_harder_target()
    {
        var sea = Athlete() with { AltitudeMeters = 0 };
        var elPaso = Athlete() with { AltitudeMeters = 1190 };

        // Thin air slows aerobic races, so the same clock time asks for more
        // fitness at El Paso than at sea level.
        Assert.True(
            Feasibility.Assess(elPaso, new GoalRequest(2 * Mile, 14 * 60, 18)).TargetVdot >
            Feasibility.Assess(sea, new GoalRequest(2 * Mile, 14 * 60, 18)).TargetVdot);
    }
}
