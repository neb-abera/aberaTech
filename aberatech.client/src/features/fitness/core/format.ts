/** Seconds to m:ss or h:mm:ss — race times and durations. */
export function formatSeconds(totalSeconds: number): string {
  const seconds = Math.round(totalSeconds);
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  if (h > 0) {
    return `${h}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
  }
  return `${m}:${String(s).padStart(2, "0")}`;
}

/** Seconds-per-km to a m:ss/km pace label. */
export function formatPace(secPerKm: number): string {
  return `${formatSeconds(secPerKm)}/km`;
}

/** Kilograms to a whole-pound label, for a lifter who thinks in pounds. */
export function kgToLb(kg: number): number {
  return kg * 2.2046226218;
}

export function lbToKg(lb: number): number {
  return lb / 2.2046226218;
}

/** Months-from-now to a "Mar 2027" label, anchored at the current month. */
export function monthsFromNow(months: number, from: Date = new Date()): string {
  const date = new Date(
    from.getFullYear(),
    from.getMonth() + Math.round(months),
    1,
  );
  return date.toLocaleDateString("en-US", { month: "short", year: "numeric" });
}

const METRIC_LABELS: Record<string, string> = {
  "run-1.5mi": "1.5-mile run",
  "run-2mi": "2-mile run",
  "run-5mi": "5-mile run",
  "run-10mi": "10-mile run",
  "ruck-12mi": "12-mile ruck",
};

export function metricLabel(metric: string): string {
  return METRIC_LABELS[metric] ?? metric;
}

const MILE_METERS = 1609.344;

/** Distance units an athlete actually types, and what one of each is in metres. */
export const DISTANCE_UNITS = {
  mi: MILE_METERS,
  km: 1000,
  m: 1,
} as const;

export type DistanceUnit = keyof typeof DISTANCE_UNITS;

export function toMeters(value: number, unit: DistanceUnit): number {
  return value * DISTANCE_UNITS[unit];
}

export function fromMeters(meters: number, unit: DistanceUnit): number {
  return meters / DISTANCE_UNITS[unit];
}

/**
 * Metres as the unit the distance was probably meant in: whole and half miles
 * read as miles, round kilometres as kilometres, anything else as metres.
 */
export function formatDistance(meters: number): string {
  const miles = meters / MILE_METERS;
  if (Math.abs(miles * 2 - Math.round(miles * 2)) < 0.002 && miles >= 0.5) {
    return `${Number(miles.toFixed(2))} mi`;
  }
  const km = meters / 1000;
  if (Math.abs(km - Math.round(km)) < 0.002) {
    return `${Math.round(km)} km`;
  }
  return `${Math.round(meters)} m`;
}

/**
 * "12:15", "1:02:30" or "735" to seconds; null when it is not a time. Typed
 * input rather than a dropdown means parsing it properly is the price.
 */
export function parseClock(text: string): number | null {
  const trimmed = text.trim();
  if (trimmed === "") return null;

  const parts = trimmed.split(":");
  if (parts.length > 3) return null;

  let seconds = 0;
  for (const part of parts) {
    if (!/^\d*\.?\d+$/.test(part.trim())) return null;
    seconds = seconds * 60 + Number(part);
  }

  return Number.isFinite(seconds) && seconds > 0 ? seconds : null;
}

/** Seconds per kilometre expressed per mile, for a runner who thinks in miles. */
export function perMile(secPerKm: number): number {
  return secPerKm * (MILE_METERS / 1000);
}

/** A months-from-now count as a date, for a deadline the athlete set by date. */
export function monthsUntil(isoDate: string, from: Date = new Date()): number {
  const target = new Date(`${isoDate}T00:00:00`);
  return (target.getTime() - from.getTime()) / (1000 * 60 * 60 * 24 * 30.4375);
}

/** An ISO date this many months out, for a deadline the athlete set by duration. */
export function dateInMonths(months: number, from: Date = new Date()): string {
  const date = new Date(from);
  date.setMonth(date.getMonth() + Math.round(months));
  return date.toISOString().slice(0, 10);
}

/** A probability as a percentage, honest about the small ones. */
export function formatChance(probability: number): string {
  if (probability >= 0.995) return ">99%";
  if (probability > 0 && probability < 0.01) return "<1%";
  return `${Math.round(probability * 100)}%`;
}

/** A stable goal key for a distance the athlete named themselves. */
export function goalKey(distanceMeters: number): string {
  return `run-${Math.round(distanceMeters)}m`;
}
