import type { PlanWorkout } from "../../training-plan/core/api";
import type { DayVolume } from "./api";

/** How much of the written plan's running and rucking the log shows was done. */
export interface MeasuredCompliance {
  /** Logged minutes over planned minutes, at most 1. */
  share: number;
  loggedMinutes: number;
  plannedMinutes: number;
  /** The plan's first day with running or rucking, yyyy-mm-dd. */
  from: string;
  /** Yesterday: today is not over. */
  through: string;
}

/** The sports the log's daily minutes count: running and rucking. */
const ENDURANCE = new Set(["Run", "Walk"]);

function dayBefore(iso: string): string {
  const date = new Date(`${iso}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() - 1);
  return date.toISOString().slice(0, 10);
}

/**
 * Compliance measured instead of guessed: the running and rucking minutes
 * the log holds against the minutes the plan asked for, from its first day
 * through yesterday. Optional sessions are left out, since skipping one is
 * the plan. Null until a planned day has passed. Neb, 2026-11-13.
 */
export function measureCompliance(
  workouts: PlanWorkout[],
  daily: DayVolume[],
  today: string,
): MeasuredCompliance | null {
  const through = dayBefore(today);
  const planned = workouts.filter(
    (w) =>
      w.sport !== undefined &&
      ENDURANCE.has(w.sport) &&
      (w.minutes ?? 0) > 0 &&
      !/optional/i.test(w.title) &&
      w.place.date !== undefined &&
      w.place.date <= through,
  );
  if (planned.length === 0) return null;

  const from = planned
    .map((w) => w.place.date as string)
    .reduce((a, b) => (a < b ? a : b));
  const plannedMinutes = planned.reduce((sum, w) => sum + (w.minutes ?? 0), 0);
  const loggedMinutes = daily
    .filter((d) => d.date >= from && d.date <= through)
    .reduce((sum, d) => sum + d.minutes, 0);

  return {
    share: Math.min(1, loggedMinutes / plannedMinutes),
    loggedMinutes,
    plannedMinutes,
    from,
    through,
  };
}
