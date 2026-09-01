using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>What a week of training is allowed to be.</summary>
/// <param name="MaxStrain">
/// The weekly recovery budget in easy-hour equivalents. The default is what a
/// full-time endurance athlete carries: about 25 hours a week at an elite
/// intensity distribution.
/// </param>
/// <param name="Responsiveness">
/// The athlete's trainability, 1.0 being the generic athlete the constants were
/// calibrated on. Fitted from the athlete's own history when there is enough of
/// it (<see cref="ModelFit"/>); it is the single knob that separates a talented
/// responder from an average one, and it carries a confidence interval.
/// </param>
public sealed record DoseLimits(double MaxStrain = DoseResponse.EliteStrain, double Responsiveness = 1.0);

/// <summary>
/// The dose-response surface: what ceiling a week of training supports, what
/// the next hour in each zone is worth, and how a fixed number of hours should
/// be split between zones.
/// </summary>
/// <remarks>
/// <b>The surface.</b> Each zone contributes a saturating amount of ceiling:
/// C(h) = C₀ + r·Σᵢ bᵢ·sᵢ·(1 − e^(−hᵢ/sᵢ)). The first hour of VO2max work a
/// week is worth more than the first easy hour; the fourth is worth almost
/// nothing, because the adaptations it drives are close to maximal after one
/// session or two. Easy volume saturates an order of magnitude more slowly,
/// which is the whole reason base training is the long game.
///
/// <b>Where the constants come from.</b> The saturation scales sᵢ are the
/// volumes at which each zone's returns are ~63% spent: 14 h/week easy,
/// 1.2 h/week threshold, 0.6 h/week interval (≈ two quality sessions),
/// 1.5 h/week strength. The marginal values bᵢ were then fixed by two
/// conditions, both checked in the tests:
/// <list type="number">
/// <item>the optimal split of an 8-hour week comes out at 80% easy — the
/// distribution Seiler documents in elite endurance athletes, reproduced by
/// the model rather than assumed by it; and</item>
/// <item>over 4–12 h/week the surface agrees within about 1.5 VDOT with the
/// linear ceiling this app previously used, which was calibrated against
/// documented aerobic-deficiency recoveries.</item>
/// </list>
/// Past 12 h/week it deliberately parts company with that line: a straight
/// line has no ceiling, and extrapolating one is what let this calculator once
/// answer "45.8 hours a week" for a time no human has run.
///
/// <b>Allocation.</b> Splitting H hours to maximise C is a constrained optimum,
/// and the Lagrangian conditions are what make it explainable. Stationarity
/// gives r·bᵢ·e^(−hᵢ/sᵢ) = λ + μ·cᵢ, so at the optimum every zone in use
/// returns the same ceiling per hour once its recovery cost is priced at μ.
/// λ is the shadow price of an hour — what one more hour a week is worth, in
/// VDOT — and it falls as the week gets longer. Both multipliers are reported,
/// because "where should my next hour go" is answered by comparing them.
///
/// Citations: <see cref="Citations.SeilerPolarized"/>,
/// <see cref="Citations.SanMillanBrooks"/>, <see cref="Citations.UphillAthleteAet"/>,
/// <see cref="Citations.RonnestadStrength"/>, <see cref="Citations.CogganPmc"/>.
/// </remarks>
public static class DoseResponse
{
    /// <summary>The ceiling of an athlete who trains nothing.</summary>
    public const double UntrainedCeiling = 38.0;

    /// <summary>Weekly strain a full-time endurance athlete sustains.</summary>
    public const double EliteStrain = 33.0;

    /// <summary>Hours at which a zone's returns are about 63% spent.</summary>
    public static double Saturation(TrainingZone zone) => zone switch
    {
        TrainingZone.Easy => 14.0,
        TrainingZone.Threshold => 1.2,
        TrainingZone.Interval => 0.6,
        TrainingZone.Strength => 1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    /// <summary>VDOT per weekly hour of a zone, at the first hour.</summary>
    public static double FirstHourValue(TrainingZone zone) => zone switch
    {
        TrainingZone.Easy => 1.960,
        TrainingZone.Threshold => 2.856,
        TrainingZone.Interval => 3.373,
        TrainingZone.Strength => 1.078,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    /// <summary>The most a zone can ever add, however many hours go into it.</summary>
    public static double MaxContribution(TrainingZone zone) =>
        FirstHourValue(zone) * Saturation(zone);

    /// <summary>The ceiling this week of training supports.</summary>
    public static double Ceiling(TrainingDose dose, double responsiveness = 1.0) =>
        UntrainedCeiling + Gain(dose, responsiveness);

    /// <summary>How far above the untrained ceiling this dose reaches.</summary>
    public static double Gain(TrainingDose dose, double responsiveness = 1.0)
    {
        if (responsiveness <= 0) throw new ArgumentOutOfRangeException(nameof(responsiveness));
        return responsiveness * TrainingDose.Zones.Sum(zone =>
        {
            var hours = dose[zone];
            if (hours < 0) throw new ArgumentOutOfRangeException(nameof(dose));
            var scale = Saturation(zone);
            return FirstHourValue(zone) * scale * (1 - Math.Exp(-hours / scale));
        });
    }

    /// <summary>
    /// ∂C/∂h for one zone at this dose: the VDOT the next weekly hour there
    /// would buy.
    /// </summary>
    public static double Marginal(TrainingDose dose, TrainingZone zone, double responsiveness = 1.0) =>
        responsiveness * FirstHourValue(zone) * Math.Exp(-dose[zone] / Saturation(zone));

    /// <summary>The highest ceiling any dose can reach under a strain budget.</summary>
    public static double MaxReachableCeiling(DoseLimits limits)
    {
        // Strain is the binding resource, and easy hours buy the most ceiling
        // per unit of it, so the maximum is approached by spending the budget
        // the way the allocator would with unlimited hours.
        var dose = Allocate(limits.MaxStrain / TrainingDose.StrainWeight(TrainingZone.Easy), limits).Dose;
        return Ceiling(dose with { StrengthHours = Saturation(TrainingZone.Strength) * 4 }, limits.Responsiveness);
    }

    /// <summary>An optimal week, and the shadow prices that explain it.</summary>
    /// <param name="Dose">The hours by zone.</param>
    /// <param name="HourPrice">
    /// VDOT bought by one more hour a week, spent where it is cheapest to
    /// recover from. It is the Lagrange multiplier λ of the hours constraint
    /// while recovery is free, and λ + μ·c once recovery has a price.
    /// </param>
    /// <param name="StrainPrice">μ — VDOT lost to each unit of recovery budget the week is short of.</param>
    public sealed record Allocation(TrainingDose Dose, double HourPrice, double StrainPrice);

    /// <summary>
    /// Split <paramref name="runningHours"/> between the running zones to
    /// maximise the ceiling, subject to the weekly recovery budget.
    /// </summary>
    /// <remarks>
    /// Solved from the Lagrangian conditions rather than by search: with
    /// hᵢ = sᵢ·ln(r·bᵢ/(λ + μ·cᵢ)) the only unknowns are the two multipliers,
    /// so an inner bisection on λ meets the hours constraint and an outer one
    /// on μ meets the strain constraint. Both are monotone, which makes the
    /// brackets sound.
    /// </remarks>
    public static Allocation Allocate(double runningHours, DoseLimits? limits = null)
    {
        if (runningHours < 0) throw new ArgumentOutOfRangeException(nameof(runningHours));
        var bounds = limits ?? new DoseLimits();

        // Even the cheapest hour costs one unit of recovery, so a week longer
        // than the budget is not a week that can be trained. Asking for one
        // gets the longest week that fits, which is what every caller wants.
        var hours = Math.Min(runningHours, bounds.MaxStrain / TrainingDose.StrainWeight(TrainingZone.Easy));

        if (hours <= 0)
        {
            return new Allocation(
                new TrainingDose(),
                HourPrice: bounds.Responsiveness * FirstHourValue(TrainingZone.Easy),
                StrainPrice: 0);
        }

        var strainPrice = 0.0;
        var dose = SplitAt(hours, strainPrice, bounds.Responsiveness);

        if (dose.Dose.Strain > bounds.MaxStrain)
        {
            // Recovery is the binding resource: price it until the week fits.
            // Strain falls monotonically in that price, because a dearer unit
            // of recovery moves hours towards the zone that uses least of it.
            double cheap = 0, dear = 100;
            for (var i = 0; i < 40; i++)
            {
                strainPrice = (cheap + dear) / 2;
                if (SplitAt(hours, strainPrice, bounds.Responsiveness).Dose.Strain > bounds.MaxStrain)
                {
                    cheap = strainPrice;
                }
                else
                {
                    dear = strainPrice;
                }
            }

            strainPrice = dear;
            dose = SplitAt(hours, strainPrice, bounds.Responsiveness);
        }

        return dose with { StrainPrice = strainPrice };
    }

    /// <summary>
    /// The hours split at a given price of recovery, meeting the hours
    /// constraint exactly.
    /// </summary>
    /// <remarks>
    /// The hours constraint is an equality, so its multiplier λ is free in
    /// sign: once recovery is expensive, λ goes negative and only the cheap
    /// zone stays affordable, which is precisely how a strain budget pushes a
    /// week towards easy running. The bracket runs from just above −μ·c(easy),
    /// where easy hours diverge, to the richest first hour on offer, where no
    /// zone is worth any hours at all.
    /// </remarks>
    private static Allocation SplitAt(double runningHours, double strainPrice, double responsiveness)
    {
        var floor = -strainPrice * TrainingDose.RunningZones.Min(TrainingDose.StrainWeight);
        // Sixty halvings of a bracket a few units wide is already past double
        // precision; the count matters because the solver calls this inside a
        // root-find inside a loop over posterior draws.
        double low = floor + 1e-9, high = responsiveness * TrainingDose.RunningZones.Max(FirstHourValue);
        for (var i = 0; i < 60; i++)
        {
            var mid = (low + high) / 2;
            if (HoursAt(mid, strainPrice, responsiveness).RunningHours > runningHours) low = mid; else high = mid;
        }

        var dose = HoursAt((low + high) / 2, strainPrice, responsiveness);
        return new Allocation(dose, Marginal(dose, TrainingZone.Easy, responsiveness), strainPrice);
    }

    /// <summary>The stationarity solution hᵢ = sᵢ·ln(r·bᵢ/(λ + μ·cᵢ)), floored at zero.</summary>
    private static TrainingDose HoursAt(double hourPrice, double strainPrice, double responsiveness)
    {
        var dose = new TrainingDose();
        foreach (var zone in TrainingDose.RunningZones)
        {
            var price = hourPrice + strainPrice * TrainingDose.StrainWeight(zone);
            var value = responsiveness * FirstHourValue(zone);
            var hours = price <= 0 || value <= price ? 0 : Saturation(zone) * Math.Log(value / price);
            dose = dose.With(zone, hours);
        }

        return dose;
    }

    /// <summary>
    /// The fewest weekly running hours whose optimal split supports
    /// <paramref name="targetCeiling"/>, or null when no sustainable week does.
    /// </summary>
    public static double? HoursForCeiling(double targetCeiling, DoseLimits? limits = null)
    {
        var bounds = limits ?? new DoseLimits();
        if (Ceiling(new TrainingDose(), bounds.Responsiveness) >= targetCeiling) return 0;

        var most = bounds.MaxStrain / TrainingDose.StrainWeight(TrainingZone.Easy);
        if (Ceiling(Allocate(most, bounds).Dose, bounds.Responsiveness) < targetCeiling) return null;

        double fewer = 0, more = most;
        for (var i = 0; i < 80; i++)
        {
            var mid = (fewer + more) / 2;
            if (Ceiling(Allocate(mid, bounds).Dose, bounds.Responsiveness) < targetCeiling) fewer = mid; else more = mid;
        }

        return (fewer + more) / 2;
    }

    /// <summary>The arithmetic behind one week's ceiling, zone by zone.</summary>
    public static IReadOnlyList<CalculationStep> Explain(TrainingDose dose, double responsiveness = 1.0)
    {
        var trace = new CalculationTrace();
        foreach (var zone in TrainingDose.Zones)
        {
            var hours = dose[zone];
            if (hours <= 0) continue;
            var scale = Saturation(zone);
            var value = FirstHourValue(zone);
            var gain = responsiveness * value * scale * (1 - Math.Exp(-hours / scale));
            trace.Add(
                $"{zone} contribution",
                Text($"{responsiveness:0.00} × {value:0.000} × {scale:0.0} × (1 − e^(−{hours:0.00}/{scale:0.0}))"),
                Text($"+{gain:0.00} VDOT (next hour there: +{Marginal(dose, zone, responsiveness):0.00})"),
                zone == TrainingZone.Strength ? Citations.RonnestadStrength.Id : Citations.SeilerPolarized.Id);
        }

        return trace
            .Add(
                "Ceiling this week supports",
                Text($"{UntrainedCeiling:0.0} + {Gain(dose, responsiveness):0.00}"),
                Text($"VDOT {Ceiling(dose, responsiveness):0.0}"),
                Citations.BanisterModel.Id)
            .Add(
                "Recovery cost",
                Text($"{dose.EasyHours:0.00}×1.0 + {dose.ThresholdHours:0.00}×2.5 + {dose.IntervalHours:0.00}×4.5 + {dose.StrengthHours:0.00}×1.5"),
                Text($"{dose.Strain:0.0} easy-hour equivalents"),
                Citations.CogganPmc.Id)
            .Steps;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
