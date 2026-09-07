namespace aberaTech.Fitness.Api;

public sealed record AerobicPointDto(string Month, double MedianSecPerKm, int Runs, int IndoorRuns = 0);

/// <summary>A field test the log turned out to contain.</summary>
public sealed record FieldTestDto(
    string Kind,
    Guid ActivityId,
    string Date,
    double SecPerKm,
    int AverageHr,
    double? DriftPercent,
    bool Indoor,
    string Evidence);

/// <summary>What the latest tests say the profile's thresholds should read.</summary>
public sealed record ThresholdSuggestionDto(int? AetHr, int? LtHr, double? LtSecPerKm, string Reason, string Basis);

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
    double HomeAltitudeMeters,
    /// <summary>The date the readiness gates count back from, if named.</summary>
    string? SelectionDate = null,
    /// <summary>The lactate-threshold heart rate, when a test has set it.</summary>
    int? LtHr = null);

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
    int ActivityCount,
    ReadinessDto Readiness,
    IReadOnlyList<FieldTestDto> FieldTests,
    ThresholdSuggestionDto? ThresholdSuggestion);

/// <summary>The athlete's standing on one metric, and where the number came from.</summary>
public sealed record StandingDto(string Metric, double Value, string Basis, string Evidence, string? On);

/// <summary>One line of a gate, scored.</summary>
public sealed record RequirementResultDto(
    string Metric,
    string Label,
    string Comparison,
    double Target,
    string Unit,
    string CitationId,
    string Status,
    StandingDto? Current,
    string Gap);

/// <summary>One published standard on the way to selection, scored and dated.</summary>
public sealed record GateDto(
    string Id,
    string Name,
    string Purpose,
    int WeeksBeforeSelection,
    string? DueOn,
    string Status,
    int Passed,
    int Known,
    IReadOnlyList<RequirementResultDto> Requirements,
    IReadOnlyList<string> Untracked);

/// <summary>A month of rucking, as a median pace at the reference heart rate and load.</summary>
public sealed record RuckPointDto(string Month, double MedianSecPerKm, int Rucks);

/// <summary>One ruck with a known load, and the run fitness it implies.</summary>
public sealed record RuckMarchDto(
    string Date,
    double DistanceMeters,
    double Seconds,
    double LoadKg,
    int? AverageHr,
    double? ImpliedVdot);

/// <summary>Rucking, read through the load-carriage model.</summary>
public sealed record RuckReportDto(
    double ReferenceLoadKg,
    double RuckEfficiency,
    IReadOnlyList<RuckPointDto> Trend,
    IReadOnlyList<RuckMarchDto> Marches,
    double? PredictedTwelveMileAt45Seconds,
    double? PredictedTwelveMileAt35Seconds,
    /// <summary>Rucks in the log with no load recorded, which the models cannot read.</summary>
    int RucksWithoutLoad,
    IReadOnlyList<StepDto> Steps);

/// <summary>The best of one bodyweight movement on one day.</summary>
public sealed record BestSetDto(string Date, string Metric, double Value);

/// <summary>The calisthenics the gates count, out of the strength log.</summary>
public sealed record CalisthenicsDto(IReadOnlyList<BestSetDto> Latest, IReadOnlyList<BestSetDto> History);

/// <summary>One weigh-in, with what it says about lean mass when body fat was recorded.</summary>
public sealed record BodyPointDto(string Date, double WeightKg, double? BodyFatPercent, double? LeanMassKg);

/// <summary>Body composition, and the selection cohort's rate at it.</summary>
public sealed record BodyReportDto(
    IReadOnlyList<BodyPointDto> Points,
    double? LatestBodyFatPercent,
    double? LatestLeanMassKg,
    double? CohortRateByBodyFat,
    double? CohortRateByLeanMass);

public sealed record AftEventDto(string Event, string Name, double Raw, int Points);

/// <summary>One fitness test, scored on the published tables as of today.</summary>
public sealed record AftResultDto(
    string Id,
    string Date,
    double DeadliftKg,
    int HandReleasePushUps,
    double SprintDragCarrySeconds,
    double PlankSeconds,
    double TwoMileSeconds,
    int Total,
    int LowestEvent,
    bool MeetsCombatStandard,
    string AgeBand,
    /// <summary>Scored on the youngest band because no birth year is recorded.</summary>
    bool AgeAssumed,
    IReadOnlyList<AftEventDto> Events,
    IReadOnlyList<StepDto> Steps);

/// <summary>Everything between the athlete and a selection slot, as the log sees it.</summary>
public sealed record ReadinessDto(
    string? SelectionDate,
    IReadOnlyList<GateDto> Gates,
    RuckReportDto Ruck,
    CalisthenicsDto Calisthenics,
    BodyReportDto Body,
    IReadOnlyList<AftResultDto> AftResults);

public sealed record AftResultUpdate(
    string Date,
    double DeadliftKg,
    int HandReleasePushUps,
    double SprintDragCarrySeconds,
    double PlankSeconds,
    double TwoMileSeconds);

/// <summary>The load a ruck was carried at; null clears it.</summary>
public sealed record ActivityLoadUpdate(double? LoadKg);

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
