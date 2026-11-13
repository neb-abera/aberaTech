/** The distances a race or time trial can be entered at, and their names. */
export const MILE = 1609.344;

export const FEET_PER_METER = 3.28084;

export const RACE_DISTANCES: { label: string; meters: number }[] = [
  { label: "1 mile", meters: MILE },
  { label: "1.5 mile", meters: 1.5 * MILE },
  { label: "2 mile", meters: 2 * MILE },
  { label: "3 mile", meters: 3 * MILE },
  { label: "5K", meters: 5000 },
  { label: "5 mile", meters: 5 * MILE },
  { label: "10K", meters: 10_000 },
  { label: "10 mile", meters: 10 * MILE },
  { label: "Half marathon", meters: 21_097.5 },
  { label: "Marathon", meters: 42_195 },
];

/** "16:49" or "1:32:10" to seconds; anything else is null. */
export function parseRaceTime(value: string): number | null {
  const parts = value.trim().split(":").map(Number);
  if (
    parts.length < 2 ||
    parts.length > 3 ||
    parts.some((p) => !Number.isFinite(p) || p < 0) ||
    parts.slice(1).some((p) => p >= 60)
  ) {
    return null;
  }
  const seconds = parts.reduce((total, part) => total * 60 + part, 0);
  return seconds > 0 ? seconds : null;
}

export function distanceLabel(meters: number | null): string {
  return (
    RACE_DISTANCES.find(
      (d) => meters !== null && Math.abs(d.meters - meters) < 1,
    )?.label ?? `${((meters ?? 0) / 1000).toFixed(2)} km`
  );
}

/** "1,186 m (3,891 ft)". */
export function formatAltitude(meters: number): string {
  return `${Math.round(meters).toLocaleString("en-US")} m (${Math.round(meters * FEET_PER_METER).toLocaleString("en-US")} ft)`;
}
