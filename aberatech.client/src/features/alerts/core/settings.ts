/**
 * The words and arithmetic the settings form and the page's summary share,
 * so the two cannot disagree about what a saved setting does.
 */

import type { AlertSettings } from "./api";

/** "1 minute", "10 minutes", "48 hours". */
export function count(value: number, unit: string): string {
  return `${value} ${unit}${value === 1 ? "" : "s"}`;
}

/**
 * The sounds pushover.net/api#sounds marks "(long)", checked on 2026-09-28.
 * The rest are one short chime.
 */
export const longSounds = [
  "alien",
  "climb",
  "persistent",
  "echo",
  "updown",
] as const;

/**
 * The closest Pushover comes to a phone that keeps ringing. It will not
 * repeat faster than every 30 s, and iOS plays a notification sound for up
 * to 30 s, so a long sound at the 30 s floor plays into each gap.
 */
export const nonstop = {
  repeatSeconds: 30,
  sound: "persistent",
};

/** The 30 s floor and a long sound: what Nonstop sets. */
export function isNonstop(repeatSeconds: number, sound: string): boolean {
  return (
    repeatSeconds === nonstop.repeatSeconds &&
    (longSounds as readonly string[]).includes(sound)
  );
}

function minutes(value: number): string {
  return Number.isInteger(value) ? `${value}` : value.toFixed(1);
}

/**
 * When an unacknowledged alarm stops, with the working:
 * Pushover sounds it at most `maxSounds` times, so the stop is the smaller
 * of the owner's limit and `maxSounds` × the repeat.
 */
export function stopWork(
  repeatSeconds: number,
  stopAfterMinutes: number,
  maxSounds: number,
): { minutes: number; text: string } {
  const capped = (maxSounds * repeatSeconds) / 60;
  const sounds = `${maxSounds} sounds × ${repeatSeconds} s = ${minutes(capped)} min`;
  if (capped < stopAfterMinutes)
    return {
      minutes: capped,
      text: `Stops after ${minutes(capped)} min: ${sounds}, before the ${stopAfterMinutes} min limit.`,
    };
  return {
    minutes: stopAfterMinutes,
    text: `Stops after ${stopAfterMinutes} min: the limit comes before ${sounds}.`,
  };
}

/**
 * What an alarm does under the saved settings, with the numbers in:
 * "rings every 60 s until you acknowledge it on the phone or here, and
 * stops after 50 min."
 */
export function alarmLine(
  settings: Pick<AlertSettings, "repeatSeconds" | "stopAfterMinutes">,
  maxSounds: number,
): string {
  const stop = stopWork(
    settings.repeatSeconds,
    settings.stopAfterMinutes,
    maxSounds,
  ).minutes;
  return `rings every ${settings.repeatSeconds} s until you acknowledge it on the phone or here, and stops after ${minutes(stop)} min.`;
}

/** What each type does under the saved settings, for the page's closing paragraph. */
export function describe(
  settings: AlertSettings,
  maxSounds: number,
  timeZone: string,
): string {
  const alarm = `Ring until stopped is one Pushover message. It ${alarmLine(settings, maxSounds)} It plays through Pushover's quiet hours. With the Pushover app's Critical Alerts setting on, an iPhone also plays it through the silent switch and Focus.`;
  const notification =
    settings.notificationPriority === 1
      ? "Ring once is one message with one sound, even during Pushover's quiet hours."
      : "Ring once is one message with one sound. It follows the phone's settings.";
  const unmarked =
    settings.defaultType === "notification"
      ? "An event with no mark and no type set here rings once."
      : "An event with no mark and no type set here sends nothing.";
  return `${alarm} ${notification} ${unmarked} Mute and Skip are checked just before each send. Times are in ${timeZone}, the calendar's own zone.`;
}

/** What each type is called on the page, and what it sends in one line. */
export const typeLabels = {
  none: "Off",
  notification: "Ring once",
  alarm: "Ring until stopped",
} as const;
