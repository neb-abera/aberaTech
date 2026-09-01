using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>Which constraint decided the answer.</summary>
public enum FeasibilityVerdict
{
    /// <summary>The athlete is already at or past the target.</summary>
    AlreadyThere,

    /// <summary>Nobody has run this. The record book is the constraint.</summary>
    PastTheWorldRecord,

    /// <summary>Nobody of this age has run this.</summary>
    PastTheAgeGradedRecord,

    /// <summary>Inside human range, but past what any sustainable training week supports for this athlete.</summary>
    PastAnyTrainingCeiling,

    /// <summary>Reachable eventually, but not by the date asked for.</summary>
    NotByThatDate,

    /// <summary>Reachable by the date, but only on more weekly hours than the athlete has.</summary>
    MoreHoursThanYouHave,

    /// <summary>Reachable on the hours available.</summary>
    Reachable
}

/// <summary>What the athlete brings to the question.</summary>
/// <param name="Parameters">Their fitted trajectory parameters.</param>
/// <param name="Fit">The fit those parameters came from, for its uncertainty.</param>
/// <param name="CurrentDose">The training week they are on now, which every plan ramps from.</param>
/// <param name="AvailableHours">Weekly running hours they say they can commit.</param>
/// <param name="Age">Age in years, for grading against the record book.</param>
/// <param name="Female">Which record book applies.</param>
/// <param name="AltitudeMeters">Where the goal race happens.</param>
/// <param name="MaxStrain">Their sustainable weekly recovery budget.</param>
public sealed record AthleteContext(
    TrajectoryParameters Parameters,
    FitResult Fit,
    TrainingDose CurrentDose,
    double AvailableHours,
    int? Age = null,
    bool Female = false,
    double AltitudeMeters = 0,
    double MaxStrain = DoseResponse.EliteStrain)
{
    public DoseLimits Limits => new(MaxStrain, Parameters.Responsiveness);
}

/// <summary>The goal, as the athlete stated it.</summary>
/// <param name="DistanceMeters">Any distance, not a menu item.</param>
/// <param name="TargetSeconds">The time to run it in, at the athlete's altitude.</param>
/// <param name="Months">Months from now to the date.</param>
public sealed record GoalRequest(double DistanceMeters, double TargetSeconds, double Months);

/// <summary>The weekly plan a goal implies, zone by zone.</summary>
/// <param name="Dose">Hours by zone.</param>
/// <param name="Strain">Its recovery cost, in easy-hour equivalents.</param>
/// <param name="HourPrice">VDOT the next weekly hour would buy.</param>
/// <param name="StrainPrice">VDOT lost per unit of recovery budget the week is short of.</param>
/// <param name="RampMonths">Months of building before the full week is being trained.</param>
/// <param name="WeeklyMiles">The easy-zone hours expressed as miles at the athlete's easy pace.</param>
public sealed record DosePrescription(
    TrainingDose Dose,
    double Strain,
    double HourPrice,
    double StrainPrice,
    double RampMonths,
    double? WeeklyMiles);

/// <summary>Everything the engine can say about one goal.</summary>
public sealed record FeasibilityReport(
    FeasibilityVerdict Verdict,
    string Headline,
    string Detail,
    string BindingConstraint,
    double TargetVdot,
    double StartVdot,
    double Grade,
    string GradeBand,
    double RecordEquivalentSeconds,
    string RecordHolder,
    double? CeilingReachable,
    DosePrescription? Prescription,
    double? MonthsAtHoursAvailable,
    double? EarliestMonths,
    double ProbabilityByDate,
    double? MonthsForEvenOdds,
    double? AchievableSecondsByDate,
    IReadOnlyList<CalculationStep> Steps);

/// <summary>
/// Decides whether a goal is possible, and if not, which wall it hits.
/// </summary>
/// <remarks>
/// The failure this replaces is worth naming. A model whose ceiling rose
/// linearly with training hours would quote a dose for any time at all, so
/// every ambitious goal — a good one, a national-class one, and one faster
/// than any human has run — came back with the same sentence about elite
/// volume. Three different walls, one message.
///
/// They are separated here, and each answer carries the number that decided
/// it: the record it beats, the age-graded record it beats, the ceiling no
/// sustainable week supports, the date it needs, or the hours it needs. The
/// checks run in that order because that is the order in which they bind —
/// there is no point discussing training hours for a time nobody has ever run.
///
/// Citations: <see cref="Citations.DanielsVdot"/>,
/// <see cref="Citations.WmaAgeGrading"/>, <see cref="Citations.SeilerPolarized"/>,
/// <see cref="Citations.GabbettWorkload"/>.
/// </remarks>
public static class Feasibility
{
    /// <summary>Chance below which a plan is not worth calling a plan.</summary>
    public const double LongShot = 0.20;

    public static FeasibilityReport Assess(
        AthleteContext athlete, GoalRequest goal, double? easyPaceSecPerKm = null)
    {
        if (goal.DistanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(goal));
        if (goal.TargetSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(goal));
        if (goal.Months <= 0) throw new ArgumentOutOfRangeException(nameof(goal));

        var p = athlete.Parameters;
        var seaLevel = Altitude.ToSeaLevel(goal.TargetSeconds, athlete.AltitudeMeters);
        var targetVdot = Vdot.FromRace(goal.DistanceMeters, seaLevel / 60.0);

        // Graded against the record nearest this distance, the way age-grading
        // works, rather than against whichever mark in the book scores highest.
        var grade = HumanLimits.Grade(targetVdot, goal.DistanceMeters, athlete.Female, athlete.Age);
        var record = HumanLimits.NearestRecord(goal.DistanceMeters, athlete.Female);
        var openCeiling = HumanLimits.DistanceCeiling(goal.DistanceMeters, athlete.Female);
        var ageGradedCeiling =
            HumanLimits.AgeGradedDistanceCeiling(goal.DistanceMeters, athlete.Female, athlete.Age);
        var recordEquivalent = Altitude.AtAltitude(
            HumanLimits.RecordEquivalentSeconds(goal.DistanceMeters, athlete.Female, athlete.Age),
            athlete.AltitudeMeters);

        var thinAir = athlete.AltitudeMeters > Altitude.FreeMeters
            ? Text($", worth {Format.Clock(seaLevel)} at sea level")
            : string.Empty;

        var trace = new CalculationTrace()
            .Add(
                "Target as a fitness score",
                Text($"{Format.Distance(goal.DistanceMeters)} in {Format.Clock(goal.TargetSeconds)}") + thinAir,
                Text($"VDOT {targetVdot:0.0}"),
                Citations.DanielsVdot.Id)
            .AddRange(HumanLimits.Explain(targetVdot, goal.DistanceMeters, athlete.Female, athlete.Age));

        // The ceiling of the biggest week this athlete could sustain, which is
        // the outer bound on anything training can do for them.
        var maxCeiling = DoseResponse.MaxReachableCeiling(athlete.Limits);
        var available = Math.Max(0, athlete.AvailableHours);

        FeasibilityReport Report(
            FeasibilityVerdict verdict,
            string headline,
            string detail,
            string binding,
            DosePrescription? prescription = null,
            double? monthsAtAvailable = null,
            double? earliest = null,
            double probability = 0,
            double? evenOdds = null,
            double? achievable = null) =>
            new(verdict, headline, detail, binding, targetVdot, p.StartVdot, grade,
                HumanLimits.Band(grade), recordEquivalent, record.Holder,
                maxCeiling, prescription, monthsAtAvailable, earliest,
                probability, evenOdds, achievable, trace.Steps);

        if (targetVdot <= p.StartVdot)
        {
            return Report(
                FeasibilityVerdict.AlreadyThere,
                $"You are already there — your anchor is worth {Format.Clock(BestTime(p.StartVdot, goal.DistanceMeters, athlete.AltitudeMeters))} for this distance.",
                "Set the target somewhere you have to go and get it.",
                "none — the target is behind you",
                achievable: BestTime(p.StartVdot, goal.DistanceMeters, athlete.AltitudeMeters));
        }

        if (targetVdot > openCeiling)
        {
            var margin = targetVdot / openCeiling - 1;
            return Report(
                FeasibilityVerdict.PastTheWorldRecord,
                $"No human has run this. {Format.Clock(goal.TargetSeconds)} for {Format.Distance(goal.DistanceMeters)} scores VDOT {targetVdot:0.0}; the record nearest that distance scores {openCeiling:0.0}.",
                $"The target is {Format.Percent(margin)} beyond the record book, so no training answer exists. Record-equivalent for this distance is {Format.Clock(recordEquivalent)} ({record.Holder}'s level).",
                $"the world record — VDOT {openCeiling:0.0}",
                achievable: recordEquivalent);
        }

        if (grade >= 1.0)
        {
            return Report(
                FeasibilityVerdict.PastTheAgeGradedRecord,
                $"Faster than anyone has run it at {athlete.Age}. VDOT {targetVdot:0.0} against an age-graded ceiling of {ageGradedCeiling:0.0}.",
                $"The open record over this distance scores {openCeiling:0.0}, which the age-grading discounts to {ageGradedCeiling:0.0} at your age. An age-graded record run would be {Format.Clock(recordEquivalent)}.",
                $"the age-graded record — VDOT {ageGradedCeiling:0.0}",
                achievable: recordEquivalent);
        }

        if (targetVdot >= maxCeiling)
        {
            var ceilingTime = BestTime(maxCeiling, goal.DistanceMeters, athlete.AltitudeMeters);
            trace.Add(
                "Ceiling of the largest sustainable week",
                Text($"{athlete.MaxStrain:0} easy-hour equivalents a week, split optimally, at responsiveness {p.Responsiveness:0.00}"),
                Text($"VDOT {maxCeiling:0.0} — worth {Format.Clock(ceilingTime)}"),
                Citations.SeilerPolarized.Id);

            return Report(
                FeasibilityVerdict.PastAnyTrainingCeiling,
                $"Inside human range, past yours. The largest week you could sustain supports VDOT {maxCeiling:0.0}; this target needs {targetVdot:0.0}.",
                $"At {athlete.MaxStrain:0} easy-hour equivalents a week — full-time-athlete volume — the model tops out at {Format.Clock(ceilingTime)} for this distance, with no date attached. Beating that would mean a higher responsiveness than your history shows ({p.Responsiveness:0.00} ± {athlete.Fit.Responsiveness.StandardError:0.00}).",
                $"your trainable ceiling — VDOT {maxCeiling:0.0}",
                achievable: ceilingTime);
        }

        // Everything below here is reachable given enough time and hours; the
        // question is which of the two runs out first.
        var mostHours = athlete.MaxStrain / TrainingDose.StrainWeight(TrainingZone.Easy);
        var earliestMonths = Trajectory.MonthsToReach(
            p,
            new DoseSchedule(DoseResponse.Allocate(mostHours, athlete.Limits).Dose, athlete.CurrentDose),
            targetVdot);

        var requiredHours = Trajectory.HoursToReach(
            p, targetVdot, goal.Months, athlete.CurrentDose, athlete.Limits);

        if (requiredHours is null)
        {
            var atDeadline = Trajectory.VdotAt(
                p,
                new DoseSchedule(DoseResponse.Allocate(mostHours, athlete.Limits).Dose, athlete.CurrentDose),
                goal.Months);
            var best = BestTime(atDeadline, goal.DistanceMeters, athlete.AltitudeMeters);

            trace.Add(
                "Fastest arrival",
                Text($"the largest sustainable week, ramped from your current {athlete.CurrentDose.RunningHours:0.0} h at 8%/week"),
                earliestMonths is { } m
                    ? Text($"VDOT {targetVdot:0.0} in {m:0.0} months — {m - goal.Months:0.0} months past your date")
                    : "not inside ten years",
                Citations.GabbettWorkload.Id);

            return Report(
                FeasibilityVerdict.NotByThatDate,
                earliestMonths is { } soonest
                    ? $"Reachable, but not in {goal.Months:0.#} months — the earliest is about {soonest:0.0} months out, and that is on full-time volume."
                    : $"Not reachable on any schedule inside ten years from VDOT {p.StartVdot:0.0}.",
                $"By your date the biggest sustainable week gets you to VDOT {atDeadline:0.0}, worth {Format.Clock(best)}. The target needs {targetVdot:0.0}.",
                earliestMonths is { } bind
                    ? $"the calendar — {bind:0.0} months needed against {goal.Months:0.#} given"
                    : "the calendar, past any horizon worth quoting",
                earliest: earliestMonths,
                achievable: best);
        }

        var allocation = DoseResponse.Allocate(requiredHours.Value, athlete.Limits);
        var schedule = new DoseSchedule(allocation.Dose, athlete.CurrentDose);
        var prescription = new DosePrescription(
            allocation.Dose,
            allocation.Dose.Strain,
            allocation.HourPrice,
            allocation.StrainPrice,
            schedule.MonthsToFullDose(),
            easyPaceSecPerKm is { } pace and > 0
                ? allocation.Dose.EasyHours * 3600 / pace / (Vdot.MileMeters / 1000)
                : null);

        var probability = Forecast.Probability(p, athlete.Fit, schedule, targetVdot, goal.Months);
        var evenOdds = Forecast.MonthsForProbability(p, athlete.Fit, schedule, targetVdot, 0.5);

        trace
            .AddRange(DoseResponse.Explain(allocation.Dose, p.Responsiveness))
            .Add(
                "Weekly hours the date needs",
                Text($"solved so the trajectory passes VDOT {targetVdot:0.0} at month {goal.Months:0.#}, ramped from {athlete.CurrentDose.RunningHours:0.0} h"),
                Text($"{requiredHours.Value:0.0} h/week of running"),
                Citations.BanisterModel.Id)
            .AddRange(Forecast.Explain(p, athlete.Fit, schedule, targetVdot, goal.Months));

        if (requiredHours.Value > available && available > 0)
        {
            var atAvailable = Trajectory.MonthsToReach(
                p,
                new DoseSchedule(DoseResponse.Allocate(available, athlete.Limits).Dose, athlete.CurrentDose),
                targetVdot);
            var byDate = Trajectory.VdotAt(
                p,
                new DoseSchedule(DoseResponse.Allocate(available, athlete.Limits).Dose, athlete.CurrentDose),
                goal.Months);

            return Report(
                FeasibilityVerdict.MoreHoursThanYouHave,
                $"The date needs {requiredHours.Value:0.0} h/week; you have said you can give {available:0.0}.",
                atAvailable is { } months
                    ? $"On {available:0.0} h/week the same target arrives around month {months:0.0} instead. Holding your date would mean finding {requiredHours.Value - available:0.0} more hours a week, or accepting {Format.Clock(BestTime(byDate, goal.DistanceMeters, athlete.AltitudeMeters))} on the day."
                    : $"On {available:0.0} h/week it does not arrive at all — that week supports VDOT {DoseResponse.Ceiling(DoseResponse.Allocate(available, athlete.Limits).Dose, p.Responsiveness):0.0}.",
                $"your available time — {available:0.0} h/week against {requiredHours.Value:0.0} needed",
                prescription,
                atAvailable,
                earliestMonths,
                probability,
                evenOdds,
                BestTime(byDate, goal.DistanceMeters, athlete.AltitudeMeters));
        }

        // A dose solved to arrive exactly on the date is a coin flip by
        // construction, and saying so is more use than dressing 50% up as
        // either confidence or doubt.
        var confidence = probability >= 0.65
            ? "and the fitted uncertainty puts the odds in your favour"
            : probability > 0.35
                ? "which lands the projection on your date exactly — even odds, so hours above this buy the margin"
                : probability >= LongShot
                    ? "though the fitted uncertainty makes it a stretch rather than a plan"
                    : "but on your own measured scatter it is a long shot at that date";

        return Report(
            FeasibilityVerdict.Reachable,
            $"Reachable on {requiredHours.Value:0.0} h/week of running — {Format.Percent(probability)} by your date.",
            $"{allocation.Dose.EasyHours:0.0} h easy, {allocation.Dose.ThresholdHours:0.0} h threshold and {allocation.Dose.IntervalHours:0.0} h intervals, built from your current {athlete.CurrentDose.RunningHours:0.0} h over about {schedule.MonthsToFullDose():0.0} months, {confidence}."
            + (evenOdds is { } fifty && Math.Abs(fifty - goal.Months) > 0.5
                ? $" Even odds land around month {fifty:0.0}."
                : string.Empty),
            $"training time — {requiredHours.Value:0.0} h/week, {allocation.Dose.Strain:0.0} strain units",
            prescription,
            goal.Months,
            earliestMonths,
            probability,
            evenOdds,
            BestTime(Trajectory.VdotAt(p, schedule, goal.Months), goal.DistanceMeters, athlete.AltitudeMeters));
    }

    /// <summary>The time a VDOT is worth over a distance, at the athlete's altitude.</summary>
    public static double BestTime(double vdot, double distanceMeters, double altitudeMeters) =>
        Altitude.AtAltitude(Vdot.MinutesFor(distanceMeters, vdot) * 60, altitudeMeters);

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
