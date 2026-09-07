using System.Globalization;

namespace aberaTech.Fitness.Domain;

/// <summary>A loaded march reduced to what the energy-cost model needs.</summary>
/// <param name="BodyKg">The athlete's body mass.</param>
/// <param name="LoadKg">The dry load carried — ruck, not water or rifle.</param>
/// <param name="DistanceMeters">Distance covered.</param>
/// <param name="Seconds">Time taken.</param>
/// <param name="GradePercent">Average grade; zero for a road or treadmill.</param>
public sealed record LoadedMarch(
    double BodyKg,
    double LoadKg,
    double DistanceMeters,
    double Seconds,
    double GradePercent = 0);

/// <summary>
/// What carrying a load costs, and therefore how fast a given engine can
/// carry it: the bridge between a running VDOT and a ruck time.
/// </summary>
/// <remarks>
/// <b>The cost.</b> Pandolf, Givoni and Goldman's equation for the metabolic
/// rate of walking under load,
///
/// M = 1.5W + 2.0(W+L)(L/W)² + η(W+L)(1.5V² + 0.35VG) watts,
///
/// with W the body mass, L the load, V the speed in m/s, G the grade in
/// percent and η a terrain factor (1 for pavement or a treadmill). It is the
/// load-carriage model the US Army's own research institute built and still
/// uses, and it is where every "how heavy, how fast" estimate in the military
/// literature starts. Its known limit is speed: it was fitted to walking and
/// under-predicts once the gait breaks into a shuffle above roughly
/// <see cref="MaxWalkingSpeed"/> m/s, which is faster than any twelve-mile
/// ruck this athlete will be timed at.
///
/// <b>The engine.</b> VDOT is an oxygen cost folded with running economy, so
/// it is not a VO2max and a loaded march cannot draw on all of it: the
/// movement is different, the shoulders and hips fatigue before the lungs do,
/// and the pack changes the gait. The share it can draw on is
/// <see cref="RuckEfficiency"/>, one number, calibrated so the published
/// selection benchmark pairs agree with each other — a 15:12 two-mile beside a
/// three-hour twelve-mile at 45 lb, a 13:30 two-mile beside 2:45 — for an
/// athlete of ordinary mass. It is a judgement made visible, not a
/// measurement, and a timed ruck of the athlete's own always outranks it.
///
/// Citations: <see cref="Citations.PandolfLoadCarriage"/>,
/// <see cref="Citations.DanielsVdot"/>, <see cref="Citations.SfasCompetitive"/>.
/// </remarks>
public static class LoadCarriage
{
    /// <summary>The dry ruck weight selection times at: 45 lb.</summary>
    public const double ReferenceLoadKg = 45 / BodyMass.PoundsPerKg;

    /// <summary>Energy released per litre of oxygen at a mixed fuel, in joules.</summary>
    public const double JoulesPerLitreOxygen = 20_900;

    /// <summary>The share of running VDOT a loaded march can sustain.</summary>
    public const double RuckEfficiency = 0.67;

    /// <summary>Above this speed the equation under-predicts: the gait is no longer a walk.</summary>
    public const double MaxWalkingSpeed = 2.2;

    /// <summary>Metabolic rate of walking under load, in watts (Pandolf 1977).</summary>
    public static double MetabolicWatts(
        double bodyKg, double loadKg, double metersPerSecond, double gradePercent = 0, double terrainFactor = 1.0)
    {
        if (bodyKg <= 0) throw new ArgumentOutOfRangeException(nameof(bodyKg));
        if (loadKg < 0) throw new ArgumentOutOfRangeException(nameof(loadKg));
        if (metersPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(metersPerSecond));
        if (terrainFactor <= 0) throw new ArgumentOutOfRangeException(nameof(terrainFactor));

        var total = bodyKg + loadKg;
        var ratio = loadKg / bodyKg;
        return 1.5 * bodyKg
               + 2.0 * total * ratio * ratio
               + terrainFactor * total * (1.5 * metersPerSecond * metersPerSecond
                                          + 0.35 * metersPerSecond * gradePercent);
    }

    /// <summary>The same cost as oxygen uptake, in ml/kg/min — VDOT's unit.</summary>
    public static double OxygenCost(double bodyKg, double loadKg, double metersPerSecond, double gradePercent = 0)
    {
        var watts = MetabolicWatts(bodyKg, loadKg, metersPerSecond, gradePercent);
        return watts * 60 / JoulesPerLitreOxygen * 1000 / bodyKg;
    }

    /// <summary>
    /// The walking speed whose oxygen cost is <paramref name="oxygenCost"/>,
    /// under this load. Cost rises monotonically with speed, so it bisects.
    /// </summary>
    public static double SpeedFor(double oxygenCost, double bodyKg, double loadKg, double gradePercent = 0)
    {
        if (oxygenCost <= 0) throw new ArgumentOutOfRangeException(nameof(oxygenCost));

        const double slowest = 0.2, fastest = 4.0;
        if (OxygenCost(bodyKg, loadKg, slowest, gradePercent) >= oxygenCost) return slowest;
        if (OxygenCost(bodyKg, loadKg, fastest, gradePercent) <= oxygenCost) return fastest;

        double slow = slowest, fast = fastest;
        for (var i = 0; i < 60; i++)
        {
            var mid = (slow + fast) / 2;
            if (OxygenCost(bodyKg, loadKg, mid, gradePercent) < oxygenCost) slow = mid; else fast = mid;
        }

        return (slow + fast) / 2;
    }

    /// <summary>
    /// The time a runner of <paramref name="vdot"/> should march
    /// <paramref name="distanceMeters"/> under <paramref name="loadKg"/> in.
    /// </summary>
    /// <remarks>
    /// The sustainable share of the engine depends on how long the effort
    /// lasts, and how long it lasts depends on the speed, so the two are
    /// iterated to a fixed point; the duration term moves slowly, and it
    /// settles in a handful of passes.
    /// </remarks>
    public static double PredictSeconds(
        double vdot, double bodyKg, double loadKg, double distanceMeters, double gradePercent = 0)
    {
        if (vdot <= 0) throw new ArgumentOutOfRangeException(nameof(vdot));
        if (distanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));

        var seconds = distanceMeters / 1.8;
        for (var i = 0; i < 30; i++)
        {
            var target = Vdot.SustainableFraction(seconds / 60) * RuckEfficiency * vdot;
            var speed = SpeedFor(target, bodyKg, loadKg, gradePercent);
            var next = distanceMeters / speed;
            if (Math.Abs(next - seconds) < 0.5)
            {
                return next;
            }

            seconds = next;
        }

        return seconds;
    }

    /// <summary>The running VDOT a timed march implies, read back through the same model.</summary>
    public static double ImpliedVdot(LoadedMarch march)
    {
        if (march.DistanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(march));
        if (march.Seconds <= 0) throw new ArgumentOutOfRangeException(nameof(march));

        var speed = march.DistanceMeters / march.Seconds;
        var cost = OxygenCost(march.BodyKg, march.LoadKg, speed, march.GradePercent);
        return cost / (Vdot.SustainableFraction(march.Seconds / 60) * RuckEfficiency);
    }

    /// <summary>
    /// A march's pace scaled to a reference heart rate and a reference load,
    /// so rucks carried at different weights on different days sit on one
    /// trend line the way <see cref="AerobicAnalysis"/> puts runs on one.
    /// </summary>
    public static double NormalizedSecPerKm(
        LoadedMarch march, int averageHr, int referenceHr, double referenceLoadKg = ReferenceLoadKg)
    {
        if (averageHr <= 0) throw new ArgumentOutOfRangeException(nameof(averageHr));
        if (referenceHr <= 0) throw new ArgumentOutOfRangeException(nameof(referenceHr));
        if (march.DistanceMeters <= 0 || march.Seconds <= 0) throw new ArgumentOutOfRangeException(nameof(march));

        // Speed scales with heart rate through the aerobic range, the same
        // linear reading the run trend uses.
        var speed = march.DistanceMeters / march.Seconds * referenceHr / averageHr;
        var cost = OxygenCost(march.BodyKg, march.LoadKg, speed, march.GradePercent);
        var atReference = SpeedFor(cost, march.BodyKg, referenceLoadKg, march.GradePercent);
        return 1000 / atReference;
    }

    /// <summary>The arithmetic behind a predicted march, with the athlete's numbers in it.</summary>
    public static IReadOnlyList<CalculationStep> Explain(
        double vdot, double bodyKg, double loadKg, double distanceMeters)
    {
        var seconds = PredictSeconds(vdot, bodyKg, loadKg, distanceMeters);
        var fraction = Vdot.SustainableFraction(seconds / 60);
        var target = fraction * RuckEfficiency * vdot;
        var speed = distanceMeters / seconds;
        var watts = MetabolicWatts(bodyKg, loadKg, speed);

        return new CalculationTrace()
            .Add(
                "Engine available to a march",
                Text($"VDOT {vdot:0.0} × {fraction:0.00} sustainable for {Format.Clock(seconds)} × {RuckEfficiency:0.00} ruck efficiency"),
                Text($"{target:0.0} ml/kg/min"),
                Citations.DanielsVdot.Id)
            .Add(
                "Cost of the load at that speed",
                Text($"1.5×{bodyKg:0} + 2.0×{bodyKg + loadKg:0}×({loadKg:0.0}/{bodyKg:0})² + {bodyKg + loadKg:0}×1.5×{speed:0.00}² W"),
                Text($"{watts:0} W = {OxygenCost(bodyKg, loadKg, speed):0.0} ml/kg/min"),
                Citations.PandolfLoadCarriage.Id)
            .Add(
                "Speed the two agree at",
                Text($"{speed:0.00} m/s under {loadKg * BodyMass.PoundsPerKg:0} lb ({loadKg:0.0} kg)"),
                Text($"{Format.Clock(1609.344 / speed)}/mile, {Format.Clock(seconds)} for {Format.Distance(distanceMeters)}"),
                Citations.SfasCompetitive.Id)
            .Steps;
    }

    private static string Text(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
