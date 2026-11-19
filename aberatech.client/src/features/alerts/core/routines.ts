/**
 * Routine alarms: a time of day, the weekdays it repeats on, a label and a
 * snooze. Stored on the server, rung by a paired phone. Nothing here rings.
 */

import { changeState, type StateChangeResult } from "./api";

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
  /**
   * Sub-alarms that ring after it, in order, each offsetMinutes after the
   * item before it. Set on the paired phone. The page's update sends none,
   * and the server keeps them. An older server leaves the field out.
   */
  steps?: RoutineStep[];
}

/** One step of a routine alarm. */
export interface RoutineStep {
  id: string;
  label: string;
  /** Minutes after the item before it: the routine's ring for the first step. */
  offsetMinutes: number;
}

/** "Take medication, 5 min after": a step and its offset, as the list reads it. */
export function stepSummary(step: RoutineStep): string {
  return `${step.label}, ${step.offsetMinutes} min after`;
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

/** A routine alarm's create, update or delete. */
export type RoutineResult = StateChangeResult;

/** Adds a routine alarm. 409 when 50 are kept. */
export function createRoutine(fields: RoutineFields): Promise<RoutineResult> {
  return changeState("POST", "/api/alerts/routines", fields);
}

/** Replaces one routine alarm with the whole body. */
export function updateRoutine(
  id: string,
  fields: RoutineFields,
): Promise<RoutineResult> {
  return changeState(
    "PUT",
    `/api/alerts/routines/${encodeURIComponent(id)}`,
    fields,
  );
}

export function deleteRoutine(id: string): Promise<RoutineResult> {
  return changeState(
    "DELETE",
    `/api/alerts/routines/${encodeURIComponent(id)}`,
  );
}
