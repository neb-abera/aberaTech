/**
 * The fitness API, typed. Every call is same-origin and cookie-authenticated;
 * a 401 means "show the sign-in button", a 403 means "signed in as the wrong
 * account", and both are states the page renders rather than errors it throws.
 */

export interface FitnessMe {
  configured: boolean;
  signedIn: boolean;
  hevyApi: boolean;
  /** Whether this deployment has Strava credentials, so a connect button makes sense. */
  strava: boolean;
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
  sustainedWeeklyHours: number | null;
  pastPeakDistanceMeters: number | null;
  pastPeakSeconds: number | null;
  pastPeakYear: number | null;
  pastPeakWeightKg: number | null;
  goalWeightKg: number | null;
  /** The clamp the server applies, so the page can offer the range it honours. */
  maxWeightAdjustmentFraction: number;
  homeAltitudeMeters: number;
  /** The date the readiness gates count back from, if named. */
  selectionDate: string | null;
  /** The lactate-threshold heart rate, when a test has set it. */
  ltHr: number | null;
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
  sustainedWeeklyHours: number | null;
  pastPeakDistanceMeters: number | null;
  pastPeakSeconds: number | null;
  pastPeakYear: number | null;
  pastPeakWeightKg: number | null;
  goalWeightKg: number | null;
  homeAltitudeMeters: number;
  anchorDistanceMeters: number | null;
  anchorSeconds: number | null;
  selectionDate: string | null;
  ltHr: number | null;
}

export interface AerobicPoint {
  month: string;
  medianSecPerKm: number;
  runs: number;
  /** How many of the month's runs were on a treadmill. */
  indoorRuns: number;
}

/** A field test the log turned out to contain: an AeT (MAF / drift) run or a threshold time trial. */
export interface FieldTest {
  kind: "aet" | "threshold";
  activityId: string;
  date: string;
  secPerKm: number;
  averageHr: number;
  /** Pace-to-heart-rate decoupling between the halves, when laps allowed it. */
  driftPercent: number | null;
  indoor: boolean;
  evidence: string;
}

/** What the latest tests say the profile's thresholds should read. */
export interface ThresholdSuggestion {
  aetHr: number | null;
  ltHr: number | null;
  ltSecPerKm: number | null;
  reason: string;
  basis: string;
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
  readiness: Readiness;
  fieldTests: FieldTest[];
  thresholdSuggestion: ThresholdSuggestion | null;
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
  /** The dry load on a ruck, in kilograms; null when nothing recorded it. */
  loadKg: number | null;
}

/**
 * A failed call, with the status kept.
 *
 * Without it every failure looked the same to the page, and a session that had
 * simply expired was reported as the server being down.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }

  /** Signed out, or signed in as someone this data is not for. */
  get needsSignIn(): boolean {
    return this.status === 401 || this.status === 403;
  }
}

async function get<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new ApiError(response.status, `${url} answered ${response.status}`);
  }
  return (await response.json()) as T;
}

export const fetchMe = () => get<FitnessMe>("/api/fitness/me");

export const fetchSummary = () => get<Summary>("/api/fitness/summary");

export const fetchCitations = () => get<Citation[]>("/api/fitness/citations");

/** The newest rows, and how many there are in total. */
export interface ActivityPage {
  activities: ActivityRow[];
  total: number;
  limit: number;
}

export const fetchActivities = () =>
  get<ActivityPage>("/api/fitness/activities");

/** Take one row back out, for when a file brought in something wrong. */
export async function deleteActivity(id: string): Promise<void> {
  const response = await fetch(`/api/fitness/activities/${id}`, {
    method: "DELETE",
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
}

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
    throw new ApiError(response.status, await response.text());
  }
}

export async function deleteGoal(metric: string): Promise<void> {
  const response = await fetch(
    `/api/fitness/goals/${encodeURIComponent(metric)}`,
    { method: "DELETE" },
  );
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
}

/**
 * Any file these services hand out. The server decides what it was — see
 * Ingest/Import.cs — so the page never asks which button a download belongs to.
 */
export interface ImportOutcome {
  kind: string;
  parsed: number;
  added: number;
  /** Already described better by an export, so not stored again. */
  skipped: number;
  /** Wall-clock copies replaced by an export's account of the same session. */
  superseded: number;
}

export async function uploadFile(file: File): Promise<ImportOutcome> {
  const response = await fetch("/api/fitness/import", {
    method: "POST",
    body: file,
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
  return (await response.json()) as ImportOutcome;
}

/** Where one automatic source stands. */
export interface SourceStatus {
  configured: boolean;
  connected: boolean;
  lastRunAt: string | null;
  lastSyncedAt: string | null;
  lastOutcome: string | null;
}

export interface IngestStatus {
  hevy: SourceStatus;
  strava: SourceStatus;
}

export const fetchIngestStatus = () => get<IngestStatus>("/api/fitness/ingest");

async function runSync(
  url: string,
): Promise<{ fetched: number; added: number }> {
  const response = await fetch(url, { method: "POST" });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
  return (await response.json()) as { fetched: number; added: number };
}

export const syncHevy = () => runSync("/api/fitness/ingest/hevy/sync");

export const syncStrava = () => runSync("/api/fitness/ingest/strava/sync");

export async function disconnectStrava(): Promise<void> {
  const response = await fetch("/api/fitness/ingest/strava/disconnect", {
    method: "POST",
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
}

export async function saveSettings(update: SettingsUpdate): Promise<void> {
  const response = await fetch("/api/fitness/settings", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(update),
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
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
    throw new ApiError(response.status, await response.text());
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
  /** The week you say you are starting from; null means start on the plan. */
  startHours: number | null;
  /** Fractional weekly build-up. Zero projects the plan as written. */
  rampPerWeek: number;
  /** Whether the imported months count as evidence about how you respond. */
  useHistory: boolean;
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

/** A prediction written down before the fact, and how it turned out. */
export interface LockedPrediction {
  id: string;
  madeOn: string;
  targetDate: string;
  distanceMeters: number;
  predictedSeconds: number;
  predictedFastSeconds: number;
  predictedSlowSeconds: number;
  weeklyHours: number;
  compliance: number;
  raceMassKg: number | null;
  actualSeconds: number | null;
  note: string | null;
  status: "pending" | "due" | "scored";
  /** Positive means the day came out slower than predicted. */
  errorSeconds: number | null;
  insideInterval: boolean | null;
}

export const fetchLockedPredictions = () =>
  get<LockedPrediction[]>("/api/fitness/predictions/locked");

export async function lockPrediction(body: {
  targetDate: string;
  distanceMeters: number;
  predictedSeconds: number;
  predictedFastSeconds: number;
  predictedSlowSeconds: number;
  weeklyHours: number;
  compliance: number;
  raceMassKg: number | null;
  note: string | null;
}): Promise<void> {
  const response = await fetch("/api/fitness/predictions/locked", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error(await response.text());
}

export async function scorePrediction(
  id: string,
  actualSeconds: number,
): Promise<void> {
  const response = await fetch(
    `/api/fitness/predictions/locked/${encodeURIComponent(id)}/actual`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ actualSeconds }),
    },
  );
  if (!response.ok) throw new Error(await response.text());
}

export async function deleteLockedPrediction(id: string): Promise<void> {
  const response = await fetch(
    `/api/fitness/predictions/locked/${encodeURIComponent(id)}`,
    { method: "DELETE" },
  );
  if (!response.ok) throw new Error(await response.text());
}

/** The athlete's standing on one metric, and where the number came from. */
export interface Standing {
  metric: string;
  value: number;
  basis: "Measured" | "Modeled";
  evidence: string;
  on: string | null;
}

export type GateStatus = "Pass" | "Fail" | "Unknown";

export interface RequirementResult {
  metric: string;
  label: string;
  comparison: "AtLeast" | "AtMost";
  target: number;
  unit: string;
  citationId: string;
  status: GateStatus;
  current: Standing | null;
  gap: string;
}

/** One published standard on the way to selection, scored and dated. */
export interface Gate {
  id: string;
  name: string;
  purpose: string;
  weeksBeforeSelection: number;
  dueOn: string | null;
  status: GateStatus;
  passed: number;
  known: number;
  requirements: RequirementResult[];
  untracked: string[];
}

export interface RuckPoint {
  month: string;
  medianSecPerKm: number;
  rucks: number;
}

export interface RuckMarch {
  date: string;
  distanceMeters: number;
  seconds: number;
  loadKg: number;
  averageHr: number | null;
  impliedVdot: number | null;
}

export interface RuckReport {
  referenceLoadKg: number;
  ruckEfficiency: number;
  trend: RuckPoint[];
  marches: RuckMarch[];
  predictedTwelveMileAt45Seconds: number | null;
  predictedTwelveMileAt35Seconds: number | null;
  rucksWithoutLoad: number;
  steps: Step[];
}

export interface BestSet {
  date: string;
  metric: string;
  value: number;
}

export interface CalisthenicsReport {
  latest: BestSet[];
  history: BestSet[];
}

export interface BodyPoint {
  date: string;
  weightKg: number;
  bodyFatPercent: number | null;
  leanMassKg: number | null;
}

export interface BodyReport {
  points: BodyPoint[];
  latestBodyFatPercent: number | null;
  latestLeanMassKg: number | null;
  cohortRateByBodyFat: number | null;
  cohortRateByLeanMass: number | null;
}

export interface AftEvent {
  event: string;
  name: string;
  raw: number;
  points: number;
}

export interface AftResult {
  id: string;
  date: string;
  deadliftKg: number;
  handReleasePushUps: number;
  sprintDragCarrySeconds: number;
  plankSeconds: number;
  twoMileSeconds: number;
  total: number;
  lowestEvent: number;
  meetsCombatStandard: boolean;
  ageBand: string;
  ageAssumed: boolean;
  events: AftEvent[];
  steps: Step[];
}

/** Everything between the athlete and a selection slot, as the log sees it. */
/** One gate line, looked at by its due date. */
export interface OutlookLine {
  metric: string;
  label: string;
  probability: number | null;
  method: "trajectory" | "trend" | "held" | "none";
  evidence: string;
  projected: number | null;
  readyInMonths: number | null;
  hoursToReach: number | null;
  unit: string;
  comparison: "AtLeast" | "AtMost";
  target: number;
}

/** One gate, looked at by its due date. */
export interface OutlookGate {
  id: string;
  name: string;
  weeksBeforeSelection: number;
  dueOn: string | null;
  monthsAway: number;
  probability: number | null;
  forecast: number;
  total: number;
  readyInMonths: number | null;
  lines: OutlookLine[];
}

/** The gates as a forecast, under one training week. */
export interface Outlook {
  selectionDate: string | null;
  weeklyHours: number;
  measuredWeeklyHours: number;
  compliance: number;
  startVdot: number;
  gates: OutlookGate[];
  earliestSelectionDate: string | null;
  bindingGate: string | null;
  assumptions: string[];
}

export function fetchOutlook(
  weeklyHours: number | null,
  compliance: number,
): Promise<Outlook> {
  const query = new URLSearchParams({ compliance: String(compliance) });
  if (weeklyHours !== null) query.set("weeklyHours", String(weeklyHours));
  return get<Outlook>(`/api/fitness/readiness/outlook?${query.toString()}`);
}

export interface Readiness {
  selectionDate: string | null;
  gates: Gate[];
  ruck: RuckReport;
  calisthenics: CalisthenicsReport;
  body: BodyReport;
  aftResults: AftResult[];
}

export interface AftResultUpdate {
  date: string;
  deadliftKg: number;
  handReleasePushUps: number;
  sprintDragCarrySeconds: number;
  plankSeconds: number;
  twoMileSeconds: number;
}

async function send(
  url: string,
  method: string,
  body: unknown,
): Promise<Response> {
  const response = await fetch(url, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
  return response;
}

/** Record a fitness test; the answer is the test scored on the published tables. */
export async function saveAftResult(
  update: AftResultUpdate,
): Promise<AftResult> {
  const response = await send("/api/fitness/aft", "POST", update);
  return (await response.json()) as AftResult;
}

export async function deleteAftResult(id: string): Promise<void> {
  const response = await fetch(`/api/fitness/aft/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
}

/** The load a ruck was carried at, in kilograms; null clears it. */
export async function saveActivityLoad(
  id: string,
  loadKg: number | null,
): Promise<void> {
  await send(`/api/fitness/activities/${encodeURIComponent(id)}/load`, "PUT", {
    loadKg,
  });
}
