/**
 * Routine alarms: a time of day, how it repeats, a label and a snooze.
 * It repeats on weekdays, every week or every N weeks, or every N minutes,
 * hours, days, weeks, months or years from a start date. Stored on the
 * server, rung by a paired phone. Nothing here rings.
 */

import { changeState, type StateChangeResult } from "./api";

export interface Routine {
  id: string;
  label: string;
  /** Wall-clock time. The server never converts it. */
  hour: number;
  minute: number;
  /**
   * ISO weekdays, Monday 1 to Sunday 7, sorted. Empty with repeat
   * "weekdays" does not repeat. Always empty with repeat "interval".
   */
  days: number[];
  /**
   * "weekdays": on days, every `every` weeks. "interval": every `every`
   * units from startsOn. An older server leaves all four out, and the
   * routine reads as weekdays every week.
   */
  repeat?: string;
  /** The N of "every N": 1 to 999. */
  every?: number;
  unit?: string;
  /** "yyyy-MM-dd": the date the series counts from. Null for weekdays every week. */
  startsOn?: string | null;
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
  /**
   * 1 rings at every ring. Above 1, every that many days from startsOn, and
   * a day it does not ring takes no time. An older server leaves it out.
   */
  everyDays?: number;
  /** "2026-11-27": the first day it rings when everyDays is above 1. */
  startsOn?: string | null;
}

const monthNames = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec",
] as const;

/** "Fri 27 Nov 2026" for "2026-11-27", with no time zone involved. */
export function stepDay(isoDate: string): string {
  const [year, month, day] = isoDate.split("-").map(Number);
  const weekday = new Date(Date.UTC(year, month - 1, day)).getUTCDay();
  return `${dayNames[(weekday + 6) % 7]} ${day} ${monthNames[month - 1]} ${year}`;
}

/**
 * "Take medication, 5 min after", and for a step on alternate days
 * "Tretinoin, 24 min after, every 2 days from Fri 27 Nov 2026".
 */
export function stepSummary(step: RoutineStep): string {
  const base = `${step.label}, ${step.offsetMinutes} min after`;
  const every = step.everyDays ?? 1;
  if (every <= 1 || !step.startsOn) return base;
  return `${base}, every ${every} days from ${stepDay(step.startsOn)}`;
}

export type RoutineRepeat = "weekdays" | "interval";

export type RoutineUnit =
  | "minutes"
  | "hours"
  | "days"
  | "weeks"
  | "months"
  | "years";

/** How a routine repeats, with every field present. */
export interface RepeatFields {
  repeat: RoutineRepeat;
  every: number;
  unit: RoutineUnit;
  startsOn: string | null;
}

/**
 * What a create or an update sends. An update that leaves the four repeat
 * fields out keeps the stored ones: the on and off switch sends none.
 */
export interface RoutineFields extends Partial<RepeatFields> {
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
  every: { min: 1, max: 999 },
} as const;

/** The units of an interval, each as the select reads it for 1 and for more. */
export const routineUnits: readonly {
  value: RoutineUnit;
  one: string;
  many: string;
}[] = [
  { value: "minutes", one: "minute", many: "minutes" },
  { value: "hours", one: "hour", many: "hours" },
  { value: "days", one: "day", many: "days" },
  { value: "weeks", one: "week", many: "weeks" },
  { value: "months", one: "month", many: "months" },
  { value: "years", one: "year", many: "years" },
];

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

/**
 * The four repeat fields of a routine, with the defaults for any the
 * server left out and for a repeat or unit this page does not know.
 */
export function repeatOf(
  routine: Partial<
    Pick<Routine, "days" | "repeat" | "every" | "unit" | "startsOn">
  >,
): RepeatFields {
  return {
    repeat: routine.repeat === "interval" ? "interval" : "weekdays",
    every: routine.every ?? 1,
    unit:
      routineUnits.find((unit) => unit.value === routine.unit)?.value ??
      "weeks",
    startsOn: routine.startsOn ?? null,
  };
}

/**
 * How a routine repeats, in the words the phone uses too: "No repeat",
 * "Weekdays", "Mon Wed Fri", "Every 2 weeks on Mon Wed Fri",
 * "Every 97 minutes", "Every hour".
 */
export function repeatSummary(
  routine: Pick<Routine, "days" | "repeat" | "every" | "unit" | "startsOn">,
): string {
  const { repeat, every, unit } = repeatOf(routine);
  if (repeat === "interval") {
    const names = routineUnits.find((choice) => choice.value === unit);
    return every === 1
      ? `Every ${names?.one}`
      : `Every ${every} ${names?.many}`;
  }
  if (every > 1 && routine.days.length > 0) {
    const sorted = [...new Set(routine.days)].sort((a, b) => a - b);
    return `Every ${every} weeks on ${sorted.map((day) => dayNames[day - 1]).join(" ")}`;
  }
  return daySummary(routine.days);
}

/** The calendar date a "yyyy-MM-dd" names, or null when it names none. */
function parseDay(text: string | null | undefined): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text ?? "");
  if (!match) return null;
  const [year, month, day] = [
    Number(match[1]),
    Number(match[2]),
    Number(match[3]),
  ];
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCFullYear(year);
  return date.getUTCMonth() === month - 1 && date.getUTCDate() === day
    ? date
    : null;
}

/**
 * "Wed 25 Nov": a start date with its weekday, and its year when that is
 * not `thisYear`. Empty for no date.
 */
export function startDay(
  startsOn: string | null | undefined,
  thisYear: number = new Date().getFullYear(),
): string {
  const date = parseDay(startsOn);
  if (!date) return "";
  const year = date.getUTCFullYear();
  const text = `${dayNames[(date.getUTCDay() + 6) % 7]} ${date.getUTCDate()} ${monthNames[date.getUTCMonth()]}`;
  return year === thisYear ? text : `${text} ${year}`;
}

/** Today on this computer's calendar, as "yyyy-MM-dd". */
export function todayDay(now: Date = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/**
 * What the server would refuse in a repeat, by the field it names. The
 * server checks the same rules and stays the authority.
 */
export function repeatErrors(
  fields: RepeatFields & { days: number[] },
): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const { min, max } = routineBounds.every;
  const everyOk =
    Number.isInteger(fields.every) &&
    fields.every >= min &&
    fields.every <= max;
  if (!everyOk) errors.every = [`A whole number, ${min} to ${max}.`];
  const dated = fields.repeat === "interval" || fields.every !== 1;
  if (fields.repeat === "weekdays" && everyOk && fields.every > 1) {
    if (fields.days.length === 0) errors.days = ["Pick at least one day."];
  }
  if (dated && everyOk && !parseDay(fields.startsOn)) {
    errors.startsOn = ["A start date."];
  }
  return errors;
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
