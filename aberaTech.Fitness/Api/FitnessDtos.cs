namespace aberaTech.Fitness.Api;

public sealed record AerobicPointDto(string Month, double MedianSecPerKm, int Runs);

public sealed record WeekVolumeDto(string WeekStart, double Minutes);

public sealed record E1RmDto(string Date, string Exercise, double E1RmKg);

public sealed record HighlightDto(string Kind, string Headline, string Evidence, bool Positive);

/// <summary>One line of the arithmetic, on its way to the "show the maths" panel.</summary>
public sealed record StepDto(string Label, string Expression, string Value, string? CitationId);

public sealed record SettingsDto(
    int ReferenceHr,
    double? LtSecondsPerKm,
    double PlanMinutesPerWeek,
    double StartVdot,
    string? VdotMeasuredOn,
    double? CurrentWeightKg,
    int? BirthYear,
    bool? Female,
    double AvailableHoursPerWeek,
    double? SustainedWeeklyHours,
    double? PastPeakDistanceMeters,
    double? PastPeakSeconds,
    int? PastPeakYear,
    double? PastPeakWeightKg,
    double? GoalWeightKg,
    /// <summary>
    /// The clamp the server applies to a weight adjustment, served so the page
    /// can offer exactly the range the model will honour rather than a range
    /// that silently gets clamped to a different answer.
    /// </summary>
    double MaxWeightAdjustmentFraction,
    double HomeAltitudeMeters);

public sealed record TrainingPaceDto(
    string Zone,
    string Name,
    string Purpose,
    double FastSecPerKm,
    double SlowSecPerKm);

/// <summary>One zone of a training week, and what the next hour there is worth.</summary>
public sealed record ZoneHoursDto(string Zone, double Hours, double Strain, double MarginalVdotPerHour);

/// <summary>A training week, by intensity.</summary>
public sealed record DoseDto(
    double EasyHours,
    double ThresholdHours,
    double IntervalHours,
    double StrengthHours,
    double RunningHours,
    double Strain,
    double EasyShare,
    IReadOnlyList<ZoneHoursDto> Zones);

/// <summary>A projected fitness with the interval around it.</summary>
public sealed record BandDto(double Months, double Vdot, double Low, double High, double StandardDeviation);

/// <summary>A predicted race time, with the interval expressed back in seconds.</summary>
public sealed record RaceTimeDto(double DistanceMeters, double Seconds, double FastSeconds, double SlowSeconds);

public sealed record CheckpointDto(
    double Months,
    double Vdot,
    double Low,
    double High,
    IReadOnlyList<RaceTimeDto> Races);

/// <summary>What fitting the model to the athlete's own history produced.</summary>
public sealed record FitDto(
    double StartVdot,
    double RatePerMonth,
    double RateStandardError,
    double Responsiveness,
    double ResponsivenessStandardError,
    double ResidualSd,
    double RSquared,
    int Observations,
    double DataWeight,
    IReadOnlyList<StepDto> Steps);

public sealed record GoalOutlookDto(
    string Metric,
    string Label,
    double DistanceMeters,
    double TargetValue,
    double TargetVdot,
    string TargetDate,
    double MonthsAway,
    double? MonthsToReach,
    double Probability,
    string Verdict,
    string Headline);

public sealed record RealityCheckDto(
    double? MeasuredPacePercent,
    int MeasuredOverDays,
    double ModelPacePercentNext90Days);

public sealed record PredictionDto(
    DoseDto Plan,
    DoseDto Measured,
    DoseDto Effective,
    double Ceiling,
    double HourPrice,
    double StrainPrice,
    double RampMonths,
    double StartVdot,
    double WeightAdjustedStartVdot,
    double? ReclaimVdot,
    double AltitudePenaltyPercent,
    IReadOnlyList<BandDto> Curve,
    IReadOnlyList<CheckpointDto> Checkpoints,
    IReadOnlyList<GoalOutlookDto> Goals,
    FitDto Fit,
    RealityCheckDto RealityCheck,
    IReadOnlyList<StepDto> Steps,
    IReadOnlyList<string> Assumptions);

public sealed record SummaryDto(
    SettingsDto Settings,
    IReadOnlyList<AerobicPointDto> AerobicTrend,
    IReadOnlyList<WeekVolumeDto> WeeklyVolume,
    IReadOnlyList<E1RmDto> StrengthTrend,
    IReadOnlyList<HighlightDto> Highlights,
    IReadOnlyList<TrainingPaceDto> TrainingPaces,
    DoseDto MeasuredDose,
    IReadOnlyList<StepDto> MeasuredDoseSteps,
    double? DeficiencySpread,
    int ActivityCount);

/// <summary>The weekly plan a goal implies.</summary>
public sealed record PrescriptionDto(
    DoseDto Dose,
    double HourPrice,
    double StrainPrice,
    double RampMonths,
    double? WeeklyMiles);

/// <summary>Everything the engine can say about one goal.</summary>
public sealed record FeasibilityDto(
    string Verdict,
    string Headline,
    string Detail,
    string BindingConstraint,
    double DistanceMeters,
    double TargetSeconds,
    double MonthsAvailable,
    double TargetVdot,
    double StartVdot,
    double Grade,
    string GradeBand,
    double RecordEquivalentSeconds,
    string RecordHolder,
    double? CeilingReachable,
    PrescriptionDto? Prescription,
    double? MonthsAtHoursAvailable,
    double? EarliestMonths,
    double ProbabilityByDate,
    double? MonthsForEvenOdds,
    double? AchievableSecondsByDate,
    IReadOnlyList<StepDto> Steps);

/// <summary>A prediction written down before the fact, and how it turned out.</summary>
public sealed record LockedPredictionDto(
    string Id,
    string MadeOn,
    string TargetDate,
    double DistanceMeters,
    double PredictedSeconds,
    double PredictedFastSeconds,
    double PredictedSlowSeconds,
    double WeeklyHours,
    double Compliance,
    double? RaceMassKg,
    double? ActualSeconds,
    string? Note,
    /// <summary>pending, due, or scored.</summary>
    string Status,
    /// <summary>Signed error in seconds once scored: positive means slower than predicted.</summary>
    double? ErrorSeconds,
    /// <summary>Whether the outcome fell inside the interval that was quoted.</summary>
    bool? InsideInterval);

public sealed record LockPredictionRequest(
    string TargetDate,
    double DistanceMeters,
    double PredictedSeconds,
    double PredictedFastSeconds,
    double PredictedSlowSeconds,
    double WeeklyHours,
    double Compliance,
    double? RaceMassKg = null,
    string? Note = null);

public sealed record ScorePredictionRequest(double ActualSeconds, string? Note = null);
