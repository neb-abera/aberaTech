/**
 * A countdown's clock: the time left until its target, or the time since
 * once it has passed, and the target written in the countdown's own zone.
 */

export interface Remaining {
  /** True once the target is behind. The parts are then the time since. */
  passed: boolean;
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
}

/** Whole seconds between now and the target. A target this second has not passed. */
export function remaining(targetMs: number, nowMs: number): Remaining {
  const passed = nowMs > targetMs;
  let left = Math.floor(Math.abs(targetMs - nowMs) / 1000);
  const days = Math.floor(left / 86_400);
  left -= days * 86_400;
  const hours = Math.floor(left / 3600);
  left -= hours * 3600;
  const minutes = Math.floor(left / 60);
  return { passed, days, hours, minutes, seconds: left - minutes * 60 };
}

const two = (value: number) => String(value).padStart(2, "0");

/** "41 days 05:12:33". */
export function formatRemaining(parts: Remaining): string {
  const days = `${parts.days.toLocaleString("en-US")} ${parts.days === 1 ? "day" : "days"}`;
  return `${days} ${two(parts.hours)}:${two(parts.minutes)}:${two(parts.seconds)}`;
}

/** "41 days 05:12:33 left", or "3 days 00:00:12 ago". */
export function describeRemaining(targetMs: number, nowMs: number): string {
  const parts = remaining(targetMs, nowMs);
  return `${formatRemaining(parts)} ${parts.passed ? "ago" : "left"}`;
}

interface WallClock {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
}

const partFormats = new Map<string, Intl.DateTimeFormat>();

function wallClock(ms: number, timeZone: string): WallClock {
  let format = partFormats.get(timeZone);
  if (!format) {
    format = new Intl.DateTimeFormat("en-US", {
      timeZone,
      hourCycle: "h23",
      year: "numeric",
      month: "numeric",
      day: "numeric",
      hour: "numeric",
      minute: "numeric",
      second: "numeric",
    });
    partFormats.set(timeZone, format);
  }
  const parts = Object.fromEntries(
    format.formatToParts(new Date(ms)).map((part) => [part.type, part.value]),
  );
  return {
    year: Number(parts.year),
    month: Number(parts.month),
    day: Number(parts.day),
    hour: Number(parts.hour) % 24,
    minute: Number(parts.minute),
    second: Number(parts.second),
  };
}

/** The zone's offset from UTC at an instant, in milliseconds. */
function offsetAt(ms: number, timeZone: string): number {
  const wall = wallClock(ms, timeZone);
  const at = new Date(0);
  at.setUTCFullYear(wall.year, wall.month - 1, wall.day);
  at.setUTCHours(wall.hour, wall.minute, wall.second, 0);
  return at.getTime() - Math.floor(ms / 1000) * 1000;
}

/**
 * The zone as the tz database spells it, or null when this browser does
 * not know it. Intl takes any letter case. The server does not.
 */
export function canonicalZone(timeZone: string): string | null {
  try {
    return new Intl.DateTimeFormat("en-US", { timeZone }).resolvedOptions()
      .timeZone;
  } catch {
    return null;
  }
}

/** True when this browser's copy of the tz database knows the zone. */
export function knownZone(timeZone: string): boolean {
  return canonicalZone(timeZone) !== null;
}

/** The browser's own zone, or UTC when it names none. */
export function browserZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
}

/**
 * "2027-03-01" and "09:00" in Asia/Amman to the instant, as ISO 8601 with
 * its offset: "2027-03-01T09:00:00+03:00". A time a clock change skips is
 * read with the offset before the change. Null for a bad date or time.
 */
export function zonedToIso(
  date: string,
  time: string,
  timeZone: string,
): string | null {
  const d = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
  const t = /^(\d{2}):(\d{2})$/.exec(time);
  if (!d || !t || !knownZone(timeZone)) return null;
  const at = new Date(0);
  at.setUTCFullYear(Number(d[1]), Number(d[2]) - 1, Number(d[3]));
  at.setUTCHours(Number(t[1]), Number(t[2]), 0, 0);
  const wall = at.getTime();
  if (
    at.getUTCDate() !== Number(d[3]) ||
    at.getUTCHours() !== Number(t[1]) ||
    at.getUTCMinutes() !== Number(t[2])
  )
    return null;
  // The offset in force at that wall time. Two guesses cover a clock
  // change. In a gap neither holds, and the earlier offset, the smaller,
  // is used: 02:30 on a spring-forward night is 03:30 by the new clock.
  const first = offsetAt(wall, timeZone);
  const second = offsetAt(wall - first, timeZone);
  const holds = [first, second].filter(
    (guess) => offsetAt(wall - guess, timeZone) === guess,
  );
  const offset =
    holds.length > 0 ? Math.max(...holds) : Math.min(first, second);
  // A zone's offset had seconds before about 1900. UTC carries that instant exactly.
  if (offset % 60_000 !== 0) return new Date(wall - offset).toISOString();
  const sign = offset < 0 ? "-" : "+";
  const minutes = Math.abs(offset) / 60_000;
  return `${date}T${time}:00${sign}${two(Math.floor(minutes / 60))}:${two(minutes % 60)}`;
}

/** The instant written in the zone, for the form's date and time inputs. */
export function isoToZoned(
  iso: string,
  timeZone: string,
): { date: string; time: string } {
  // A zone this browser lacks (the phone's tz data can be newer) reads in UTC.
  const wall = wallClock(
    Date.parse(iso),
    knownZone(timeZone) ? timeZone : "UTC",
  );
  return {
    date: `${String(wall.year).padStart(4, "0")}-${two(wall.month)}-${two(wall.day)}`,
    time: `${two(wall.hour)}:${two(wall.minute)}`,
  };
}

const targetFormats = new Map<string, Intl.DateTimeFormat>();

/** "Mon, Mar 1, 2027, 9:00 AM GMT+3": the target in its zone, naming the zone. */
export function formatTarget(iso: string, timeZone: string): string {
  let format = targetFormats.get(timeZone);
  if (!format) {
    format = new Intl.DateTimeFormat("en-US", {
      timeZone: knownZone(timeZone) ? timeZone : "UTC",
      weekday: "short",
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
      timeZoneName: "short",
    });
    targetFormats.set(timeZone, format);
  }
  // ICU puts a narrow no-break space before AM and PM. A plain space reads the same.
  return format.format(new Date(iso)).replace(/[  ]/g, " ");
}
