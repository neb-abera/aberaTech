/**
 * Countdowns: a label and an instant, with the zone the date is written
 * in. Stored on the server, each with an event at its target on Google
 * Calendar. /dates and the paired phone show the time left.
 */

import { changeState, type StateChangeResult } from "./api";

export interface Countdown {
  id: string;
  label: string;
  /** ISO 8601 with its offset. */
  targetAt: string;
  /** The IANA zone the target's date and time are written in. */
  timeZone: string;
  updatedAt: string;
  /** Its calendar event rings as an alarm at the target. An older server leaves it out. */
  alarm?: boolean;
  /** False for a countdown saved before countdowns made calendar events. Its next save makes one. */
  onCalendar?: boolean;
  /** The unit it repeats in. For one that repeats, targetAt is the next date. An older server leaves it out. */
  repeat?: CountdownRepeat;
  /** How many units between two dates. */
  repeatEvery?: number;
}

/** Never, or every N hours, days, weeks, months or years. */
export type CountdownRepeat =
  | "none"
  | "hourly"
  | "daily"
  | "weekly"
  | "monthly"
  | "yearly";

/** The units, in the order the form lists them, singular and plural. */
export const countdownRepeats: readonly {
  value: CountdownRepeat;
  one: string;
  many: string;
}[] = [
  { value: "none", one: "Never", many: "Never" },
  { value: "hourly", one: "hour", many: "hours" },
  { value: "daily", one: "day", many: "days" },
  { value: "weekly", one: "week", many: "weeks" },
  { value: "monthly", one: "month", many: "months" },
  { value: "yearly", one: "year", many: "years" },
];

/** "Every 2 weeks", "Every year". Null for one that does not repeat. */
export function describeRepeat(
  repeat: CountdownRepeat | undefined,
  every = 1,
): string | null {
  const unit = countdownRepeats.find((choice) => choice.value === repeat);
  if (!unit || unit.value === "none") return null;
  return every === 1 ? `Every ${unit.one}` : `Every ${every} ${unit.many}`;
}

/** What a create or an update sends. */
export interface CountdownFields {
  label: string;
  /** ISO 8601 with its offset. */
  targetAt: string;
  timeZone: string;
  /** Ring an alarm at the target, on the phone and through Pushover, until it is stopped. */
  alarm: boolean;
  repeat: CountdownRepeat;
  repeatEvery: number;
}

/** What the server takes. It checks the same numbers. */
export const countdownBounds = {
  label: 60,
  repeatEvery: 999,
  max: 50,
  defaultLabel: "Countdown",
} as const;

/** Adds a countdown. 409 when 50 are kept. */
export function createCountdown(
  fields: CountdownFields,
): Promise<StateChangeResult> {
  return changeState("POST", "/api/alerts/countdowns", fields);
}

/** Replaces one countdown with the whole body. */
export function updateCountdown(
  id: string,
  fields: CountdownFields,
): Promise<StateChangeResult> {
  return changeState(
    "PUT",
    `/api/alerts/countdowns/${encodeURIComponent(id)}`,
    fields,
  );
}

export function deleteCountdown(id: string): Promise<StateChangeResult> {
  return changeState(
    "DELETE",
    `/api/alerts/countdowns/${encodeURIComponent(id)}`,
  );
}
