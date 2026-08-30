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
