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
  /** How it repeats. For one that repeats, targetAt is the next date. An older server leaves it out. */
  repeat?: CountdownRepeat;
}

/** Never, or every week, month or year at the same time on its zone's clock. */
export type CountdownRepeat = "none" | "weekly" | "monthly" | "yearly";

/** The choices, in the order the form lists them, with what the page calls each. */
export const countdownRepeats: readonly {
  value: CountdownRepeat;
  label: string;
}[] = [
  { value: "none", label: "Never" },
  { value: "yearly", label: "Every year" },
  { value: "monthly", label: "Every month" },
  { value: "weekly", label: "Every week" },
];

/** What a create or an update sends. */
export interface CountdownFields {
  label: string;
  /** ISO 8601 with its offset. */
  targetAt: string;
  timeZone: string;
  /** Ring an alarm at the target, on the phone and through Pushover, until it is stopped. */
  alarm: boolean;
  repeat: CountdownRepeat;
}

/** What the server takes. It checks the same numbers. */
export const countdownBounds = {
  label: 60,
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
