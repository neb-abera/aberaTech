/**
 * The fitness API, typed. Every call is same-origin and cookie-authenticated;
 * a 401 means "show the sign-in button", a 403 means "signed in as the wrong
 * account", and both are states the page renders rather than errors it throws.
 */

export interface FitnessMe {
  configured: boolean;
  signedIn: boolean;
  hevyApi: boolean;
}

export interface SettingsDto {
  referenceHr: number;
  ltSecondsPerKm: number | null;
  planMinutesPerWeek: number;
  startVdot: number;
  vdotMeasuredOn: string | null;
  currentWeightKg: number | null;
  birthYear: number | null;
  female: boolean | null;
  availableHoursPerWeek: number;
  pastPeakDistanceMeters: number | null;
  pastPeakSeconds: number | null;
  pastPeakYear: number | null;
  homeAltitudeMeters: number;
}

export interface SettingsUpdate {
  referenceHr: number;
  ltSecondsPerKm: number | null;
  planMinutesPerWeek: number;
  startVdot: number;
  vdotMeasuredOn: string | null;
  birthYear: number | null;
  female: boolean | null;
  availableHoursPerWeek: number;
  pastPeakDistanceMeters: number | null;
  pastPeakSeconds: number | null;
  pastPeakYear: number | null;
  homeAltitudeMeters: number;
  anchorDistanceMeters: number | null;
  anchorSeconds: number | null;
}

export interface AerobicPoint {
  month: string;
  medianSecPerKm: number;
  runs: number;
}

export interface WeekVolume {
  weekStart: string;
  minutes: number;
}

export interface E1RmPoint {
  date: string;
  exercise: string;
  e1RmKg: number;
}

export interface Highlight {
  kind: string;
  headline: string;
  evidence: string;
  positive: boolean;
}

export interface TrainingPace {
  zone: string;
  name: string;
  purpose: string;
  fastSecPerKm: number;
  slowSecPerKm: number;
}

/** One line of the arithmetic behind a number the page shows. */
export interface Step {
  label: string;
  expression: string;
  value: string;
  citationId: string | null;
}

export type Zone = "Easy" | "Threshold" | "Interval" | "Strength";

export interface ZoneHours {
  zone: Zone;
  hours: number;
  strain: number;
  marginalVdotPerHour: number;
}

/** A training week, by intensity. */
export interface Dose {
  easyHours: number;
  thresholdHours: number;
  intervalHours: number;
  strengthHours: number;
  runningHours: number;
  strain: number;
  easyShare: number;
  zones: ZoneHours[];
}

export interface Summary {
  settings: SettingsDto;
  aerobicTrend: AerobicPoint[];
  weeklyVolume: WeekVolume[];
  strengthTrend: E1RmPoint[];
  highlights: Highlight[];
  trainingPaces: TrainingPace[];
  measuredDose: Dose;
  measuredDoseSteps: Step[];
  deficiencySpread: number | null;
  activityCount: number;
}

/** A projected fitness with the interval around it. */
export interface ProjectionPoint {
  months: number;
  vdot: number;
  low: number;
  high: number;
  standardDeviation: number;
}

export interface RaceTime {
  distanceMeters: number;
  seconds: number;
  fastSeconds: number;
  slowSeconds: number;
}

export interface Checkpoint {
  months: number;
  vdot: number;
  low: number;
  high: number;
  races: RaceTime[];
}

export interface Fit {
  startVdot: number;
  ratePerMonth: number;
  rateStandardError: number;
  responsiveness: number;
  responsivenessStandardError: number;
  residualSd: number;
  rSquared: number;
  observations: number;
  dataWeight: number;
  steps: Step[];
}

export interface GoalOutlook {
  metric: string;
  label: string;
  distanceMeters: number;
  targetValue: number;
  targetVdot: number;
  targetDate: string;
  monthsAway: number;
  monthsToReach: number | null;
  probability: number;
  verdict: string;
  headline: string;
}

export interface RealityCheck {
  measuredPacePercent: number | null;
  measuredOverDays: number;
  modelPacePercentNext90Days: number;
}

export interface Prediction {
  plan: Dose;
  measured: Dose;
  effective: Dose;
  ceiling: number;
  hourPrice: number;
  strainPrice: number;
  rampMonths: number;
  startVdot: number;
  weightAdjustedStartVdot: number;
  reclaimVdot: number | null;
  altitudePenaltyPercent: number;
  curve: ProjectionPoint[];
  checkpoints: Checkpoint[];
  goals: GoalOutlook[];
  fit: Fit;
  realityCheck: RealityCheck;
  steps: Step[];
  assumptions: string[];
}

export interface Prescription {
  dose: Dose;
  hourPrice: number;
  strainPrice: number;
  rampMonths: number;
  weeklyMiles: number | null;
}

export type Verdict =
  | "AlreadyThere"
  | "PastTheWorldRecord"
  | "PastTheAgeGradedRecord"
  | "PastAnyTrainingCeiling"
  | "NotByThatDate"
  | "MoreHoursThanYouHave"
  | "Reachable";

/** Everything the engine can say about one goal. */
export interface Feasibility {
  verdict: Verdict;
  headline: string;
  detail: string;
  bindingConstraint: string;
  distanceMeters: number;
  targetSeconds: number;
  monthsAvailable: number;
  targetVdot: number;
  startVdot: number;
  grade: number;
  gradeBand: string;
  recordEquivalentSeconds: number;
  recordHolder: string;
  ceilingReachable: number | null;
  prescription: Prescription | null;
  monthsAtHoursAvailable: number | null;
  earliestMonths: number | null;
  probabilityByDate: number;
  monthsForEvenOdds: number | null;
  achievableSecondsByDate: number | null;
  steps: Step[];
}

export interface Citation {
  id: string;
  claim: string;
  who: string;
  work: string;
  year: number;
  url: string | null;
}

export interface ActivityRow {
  id: string;
  source: string;
  startedAt: string;
  sport: string;
  name: string;
  distanceMeters: number | null;
  durationSeconds: number;
  averageHr: number | null;
}

async function get<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`${url} answered ${response.status}`);
  }
  return (await response.json()) as T;
}

export const fetchMe = () => get<FitnessMe>("/api/fitness/me");

export const fetchSummary = () => get<Summary>("/api/fitness/summary");

export const fetchCitations = () => get<Citation[]>("/api/fitness/citations");

export const fetchActivities = () =>
  get<ActivityRow[]>("/api/fitness/activities");

/** A plan stated by zone, or a total the model is asked to split itself. */
export interface PlanRequest {
  weeklyHours?: number;
  easyHours?: number;
  thresholdHours?: number;
  intervalHours?: number;
  strengthHours?: number;
}

export function fetchPrediction(
  plan: PlanRequest,
  compliance: number,
  targetWeightKg: number | null,
  distances: number[],
  horizons: number[],
): Promise<Prediction> {
  const query = new URLSearchParams({ compliance: String(compliance) });
  for (const [key, value] of Object.entries(plan)) {
    if (value !== undefined) {
      query.set(key, String(value));
    }
  }
  if (targetWeightKg !== null) {
    query.set("targetWeightKg", String(targetWeightKg));
  }
  if (distances.length > 0) {
    query.set("distances", distances.join(","));
  }
  if (horizons.length > 0) {
    query.set("horizons", horizons.join(","));
  }
  return get<Prediction>(`/api/fitness/predictions?${query}`);
}

export function fetchFeasibility(
  distanceMeters: number,
  targetSeconds: number,
  monthsAvailable: number,
  availableHours: number,
): Promise<Feasibility> {
  const query = new URLSearchParams({
    distanceMeters: String(distanceMeters),
    targetSeconds: String(targetSeconds),
    monthsAvailable: String(monthsAvailable),
    availableHours: String(availableHours),
  });
  return get<Feasibility>(`/api/fitness/predictions/goal?${query}`);
}

export async function saveGoal(goal: {
  metric: string;
  targetValue: number;
  targetDate: string;
  distanceMeters: number;
  label: string;
}): Promise<void> {
  const response = await fetch("/api/fitness/goals", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(goal),
  });
  if (!response.ok) {
    throw new Error(await response.text());
  }
}

export async function deleteGoal(metric: string): Promise<void> {
  const response = await fetch(
    `/api/fitness/goals/${encodeURIComponent(metric)}`,
    { method: "DELETE" },
  );
  if (!response.ok) {
    throw new Error(await response.text());
  }
}

export async function uploadFile(
  kind: "hevy-csv" | "garmin-csv" | "fit",
  file: File,
): Promise<{ parsed: number; added: number }> {
  const response = await fetch(`/api/fitness/import/${kind}`, {
    method: "POST",
    body: file,
  });
  if (!response.ok) {
    throw new Error(await response.text());
  }
  return (await response.json()) as { parsed: number; added: number };
}

export async function syncHevy(): Promise<{ fetched: number; added: number }> {
  const response = await fetch("/api/fitness/sync/hevy", { method: "POST" });
  if (!response.ok) {
    throw new Error(await response.text());
  }
  return (await response.json()) as { fetched: number; added: number };
}

export async function saveSettings(update: SettingsUpdate): Promise<void> {
  const response = await fetch("/api/fitness/settings", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(update),
  });
  if (!response.ok) {
    throw new Error(await response.text());
  }
}

export async function saveBodyMetric(
  date: string,
  weightKg: number,
  bodyFatPercent: number | null,
): Promise<void> {
  const response = await fetch("/api/fitness/body-metrics", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ date, weightKg, bodyFatPercent }),
  });
  if (!response.ok) {
    throw new Error(await response.text());
  }
}

/** The five levers a what-if can hold or solve for. */
export type FactorName =
  | "WeeklyHours"
  | "Compliance"
  | "RaceMassKg"
  | "StrengthHours"
  | "Months";

export interface ScenarioRequest {
  distanceMeters: number;
  months: number;
  weeklyHours: number;
  compliance: number;
  raceMassKg: number | null;
  strengthHours: number;
}

/** An answer as a distribution, which is the only honest shape for one. */
export interface SpreadValue {
  median: number;
  low: number;
  high: number;
  /** Share of the posterior in which nothing reaches the target. */
  impossible: number;
  /** Share in which the factor needs no change at all. */
  alreadyMet: number;
}

export interface Sensitivity {
  factor: FactorName;
  label: string;
  value: number;
  elasticity: number;
  perUnitSeconds: number;
  lowValue: number;
  highValue: number;
  lowSeconds: number;
  highSeconds: number;
  swing: number;
}

export interface ModelParameter {
  name: string;
  median: number;
  low: number;
  high: number;
  unit: string;
}

export interface ModelCard {
  parameters: ModelParameter[];
  acceptanceRate: number;
  rHat: number;
  effectiveSampleSize: number;
  converged: boolean;
  observations: number;
  timeTrials: number;
  draws: number;
  steps: Step[];
}

export interface SolveResult {
  predicted: SpreadValue;
  solveFor: FactorName | null;
  solved: SpreadValue | null;
  probability: number | null;
  sensitivities: Sensitivity[];
  fan: ProjectionPoint[];
  model: ModelCard;
  steps: Step[];
}

export interface SurfaceResult {
  across: FactorName;
  down: FactorName;
  acrossValues: number[];
  downValues: number[];
  seconds: number[][];
  targetSeconds: number | null;
}

export interface Measurement {
  atMonths: number;
  kind: "TimeTrial" | "NormalizedPace";
  widthBeforeSeconds: number;
  widthAfterSeconds: number;
  reduction: number;
}

export interface MeasurePlan {
  options: Measurement[];
  steps: Step[];
}

async function post<T>(url: string, body: unknown): Promise<T> {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`${url} answered ${response.status}`);
  }
  return (await response.json()) as T;
}

export const fetchModel = () => get<ModelCard>("/api/fitness/model");

export function solve(
  scenario: ScenarioRequest,
  solveFor: FactorName | null,
  targetSeconds: number | null,
): Promise<SolveResult> {
  return post<SolveResult>("/api/fitness/solve", {
    scenario,
    solveFor,
    targetSeconds,
  });
}

export function fetchSurface(
  scenario: ScenarioRequest,
  across: FactorName,
  down: FactorName,
  targetSeconds: number | null,
): Promise<SurfaceResult> {
  return post<SurfaceResult>("/api/fitness/surface", {
    scenario,
    across,
    down,
    targetSeconds,
  });
}

export const fetchMeasurementPlan = (scenario: ScenarioRequest) =>
  post<MeasurePlan>("/api/fitness/measure", scenario);
