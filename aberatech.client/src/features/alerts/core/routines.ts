/**
 * Routine alarms: a time of day, the weekdays it repeats on, a label and a
 * snooze. Stored on the server, rung by a paired phone. Nothing here rings.
 */

import type { ActionResult, AlertsState } from "./api";

export interface Routine {
  id: string;
  label: string;
  /** Wall-clock time. The server never converts it. */
  hour: number;
  minute: number;
  /** ISO weekdays, Monday 1 to Sunday 7, sorted. Empty does not repeat. */
  days: number[];
  enabled: boolean;
  snoozeMinutes: number;
  updatedAt: string;
}

/** What a create or an update sends. */
export interface RoutineFields {
  label: string;
  hour: number;
  minute: number;
  days: number[];
  enabled: boolean;
  snoozeMinutes: number;
}

/** What the server takes. It checks the same numbers. */
export const routineBounds = {
  label: 60,
  snooze: { min: 1, max: 30 },
  defaultSnooze: 9,
  max: 50,
} as const;

/** Monday first, as ISO numbers them. */
export const dayNames = [
  "Mon",
  "Tue",
  "Wed",
  "Thu",
  "Fri",
  "Sat",
  "Sun",
] as const;

/** "06:30": the stored time on a 24-hour clock. */
export function routineTime(hour: number, minute: number): string {
  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

/** "Every day", "Weekdays", "Weekends", "No repeat", or short names: "Mon Wed Fri". */
export function daySummary(days: number[]): string {
  const sorted = [...new Set(days)].sort((a, b) => a - b);
  const key = sorted.join(",");
  if (sorted.length === 0) return "No repeat";
  if (key === "1,2,3,4,5,6,7") return "Every day";
  if (key === "1,2,3,4,5") return "Weekdays";
  if (key === "6,7") return "Weekends";
  return sorted.map((day) => dayNames[day - 1]).join(" ");
}

export type RoutineResult =
  | ActionResult
  | { ok: false; reason: "full" | "missing"; detail?: string };

/** Adds a routine alarm. 409 when 50 are kept. */
export function createRoutine(fields: RoutineFields): Promise<RoutineResult> {
  return request("POST", "/api/alerts/routines", fields);
}

/** Replaces one routine alarm with the whole body. */
export function updateRoutine(
  id: string,
  fields: RoutineFields,
): Promise<RoutineResult> {
  return request(
    "PUT",
    `/api/alerts/routines/${encodeURIComponent(id)}`,
    fields,
  );
}

export function deleteRoutine(id: string): Promise<RoutineResult> {
  return request("DELETE", `/api/alerts/routines/${encodeURIComponent(id)}`);
}

async function request(
  method: "POST" | "PUT" | "DELETE",
  path: string,
  body?: RoutineFields,
): Promise<RoutineResult> {
  try {
    const response = await fetch(path, {
      method,
      credentials: "same-origin",
      ...(body === undefined
        ? {}
        : {
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
          }),
    });
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (response.status === 429) return { ok: false, reason: "throttled" };
    if (response.status === 404) return { ok: false, reason: "missing" };
    const json = response.headers.get("content-type")?.includes("json");
    if (!response.ok) {
      const problem: { detail?: string; errors?: Record<string, string[]> } =
        (json ? await response.json() : null) ?? {};
      if (response.status === 400 && problem.errors)
        return { ok: false, reason: "invalid", errors: problem.errors };
      if (response.status === 409)
        return { ok: false, reason: "full", detail: problem.detail };
      return { ok: false, reason: "refused" };
    }
    if (!json) return { ok: false, reason: "refused" };
    const {
      configured: _configured,
      missing: _missing,
      ...state
    } = (await response.json()) as AlertsState & {
      configured?: boolean;
      missing?: string[];
    };
    return { ok: true, state };
  } catch {
    return { ok: false, reason: "network" };
  }
}
