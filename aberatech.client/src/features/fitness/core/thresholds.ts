/**
 * The threshold log's calls. Kept out of api.ts, which the site's entry
 * chunk carries, so they load with the Log tab that uses them.
 */
import { ApiError } from "./api";

export type ThresholdKind = "aet-hr" | "lt-hr" | "lt-pace";

/** One logged threshold value. The newest test date of each kind is in use. */
export interface ThresholdEntry {
  id: string;
  kind: ThresholdKind;
  /** Beats per minute for the heart rates, seconds per km for the pace. */
  value: number;
  /** yyyy-mm-dd of the test; null for a value carried over from before the log. */
  measuredOn: string | null;
  source: string | null;
  activityId: string | null;
  current: boolean;
}

export async function fetchThresholds(): Promise<ThresholdEntry[]> {
  const response = await fetch("/api/fitness/thresholds");
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
  return (await response.json()) as ThresholdEntry[];
}

/** Logs one value. Nothing else is touched, and nothing is overwritten. */
export async function logThreshold(entry: {
  kind: ThresholdKind;
  value: number;
  measuredOn: string;
  source?: string;
  activityId?: string;
}): Promise<{ current: boolean }> {
  const response = await fetch("/api/fitness/thresholds", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(entry),
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
  return (await response.json()) as { current: boolean };
}

export async function deleteThreshold(id: string): Promise<void> {
  const response = await fetch(
    `/api/fitness/thresholds/${encodeURIComponent(id)}`,
    { method: "DELETE" },
  );
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
}
