/**
 * Attempts at a gate, and what they add up to.
 *
 * A gate in the plan is a pass or fail test with a clock on it. A checkbox
 * cannot hold a time, so each gate keeps a log of attempts instead: when,
 * whether it passed, how long it took, and a note. The note is where results
 * from outside the site go, a practice exam score or a drill time from one
 * of the linked practice sites, so the log is the one place everything is
 * scored.
 */

export interface Attempt {
  /** Stable across renders and reloads; what removal and React keys go by. */
  id: string;
  /** ISO date, the day of the attempt. */
  on: string;
  passed: boolean;
  /** How long it took, where the gate has a clock. */
  minutes?: number;
  note?: string;
}

export interface Summary {
  attempts: number;
  passes: number;
  latest: Attempt | null;
  /** The fastest pass, where any pass carried a time. */
  best: Attempt | null;
}

export function summarize(attempts: Attempt[]): Summary {
  const passes = attempts.filter((attempt) => attempt.passed);
  const timed = passes.filter(
    (attempt): attempt is Attempt & { minutes: number } =>
      typeof attempt.minutes === "number" && attempt.minutes > 0,
  );
  const best = timed.reduce<Attempt | null>(
    (fastest, attempt) =>
      fastest === null || attempt.minutes < (fastest.minutes ?? Infinity)
        ? attempt
        : fastest,
    null,
  );
  return {
    attempts: attempts.length,
    passes: passes.length,
    latest: attempts[attempts.length - 1] ?? null,
    best,
  };
}

/** Today as an ISO date in local time, for the form's default. */
export function isoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

/** An id for a new attempt: time plus noise, unique enough for one browser. */
export function newAttemptId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

/**
 * What storage hands back, kept only if it is the shape written. An entry
 * saved before ids existed is given one from its position, so it keeps
 * working rather than being dropped.
 */
export function asAttemptLog(value: unknown): Record<string, Attempt[]> | null {
  if (typeof value !== "object" || value === null || Array.isArray(value))
    return null;
  const log: Record<string, Attempt[]> = {};
  for (const [key, entries] of Object.entries(value)) {
    if (!Array.isArray(entries)) continue;
    log[key] = entries
      .filter(
        (entry): entry is Omit<Attempt, "id"> & { id?: unknown } =>
          typeof entry === "object" &&
          entry !== null &&
          typeof (entry as Attempt).on === "string" &&
          typeof (entry as Attempt).passed === "boolean",
      )
      .map((entry, index) => ({
        ...entry,
        id: typeof entry.id === "string" ? entry.id : `${key}-legacy-${index}`,
      }));
  }
  return log;
}
