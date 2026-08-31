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

export interface Summary {
  settings: SettingsDto;
  aerobicTrend: AerobicPoint[];
  weeklyVolume: WeekVolume[];
  strengthTrend: E1RmPoint[];
  highlights: Highlight[];
  trainingPaces: TrainingPace[];
  deficiencySpread: number | null;
  activityCount: number;
}

export interface ProjectionPoint {
  months: number;
  vdot: number;
}

export interface Checkpoint {
  months: number;
  vdot: number;
  twoMileSeconds: number;
  fiveMileSeconds: number;
  oneAndAHalfMileSeconds: number;
}

export interface GoalOutlook {
  metric: string;
  targetValue: number;
  targetVdot: number;
  targetDate: string;
  monthsToReach: number | null;
  reachable: boolean;
}

export interface RealityCheck {
  measuredPacePercent: number | null;
  measuredOverDays: number;
  modelPacePercentNext90Days: number;
}

export interface Prediction {
  effectiveHours: number;
  ceiling: number;
  weightAdjustedStartVdot: number;
  reclaimVdot: number | null;
  altitudePenaltyPercent: number;
  curve: ProjectionPoint[];
  checkpoints: Checkpoint[];
  goals: GoalOutlook[];
  realityCheck: RealityCheck;
  assumptions: string[];
}

export interface RequiredDose {
  startVdot: number;
  targetVdot: number;
  monthsAvailable: number;
  requiredEffectiveHours: number | null;
  requiredWeeklyHoursAtCompliance: number | null;
  verdict: string;
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

export function fetchPrediction(
  weeklyHours: number,
  compliance: number,
  targetWeightKg: number | null,
): Promise<Prediction> {
  const query = new URLSearchParams({
    weeklyHours: String(weeklyHours),
    compliance: String(compliance),
  });
  if (targetWeightKg !== null) {
    query.set("targetWeightKg", String(targetWeightKg));
  }
  return get<Prediction>(`/api/fitness/predictions?${query}`);
}

export function fetchRequiredDose(
  distanceMeters: number,
  targetSeconds: number,
  monthsAvailable: number,
  compliance: number,
): Promise<RequiredDose> {
  const query = new URLSearchParams({
    distanceMeters: String(distanceMeters),
    targetSeconds: String(targetSeconds),
    monthsAvailable: String(monthsAvailable),
    compliance: String(compliance),
  });
  return get<RequiredDose>(`/api/fitness/predictions/required?${query}`);
}

/**
 * Any file these services hand out. The server decides what it was — see
 * Ingest/Import.cs — so the page never asks which button a download belongs to.
 */
export async function uploadFile(
  file: File,
): Promise<{ kind: string; parsed: number; added: number }> {
  const response = await fetch("/api/fitness/import", {
    method: "POST",
    body: file,
  });
  if (!response.ok) {
    throw new Error(await response.text());
  }
  return (await response.json()) as {
    kind: string;
    parsed: number;
    added: number;
  };
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
