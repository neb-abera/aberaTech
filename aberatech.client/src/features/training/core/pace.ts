/**
 * Pace: how much self-paced work got done, against the plan's hours, and
 * when each phase's written work would be finished at that pace. Pace is
 * shown, never enforced. It opens and holds back nothing.
 */
import { addDays, localDateOf, weekday } from "./dates";
import {
  everyItem,
  itemStatus,
  type Solves,
  type TrainingDocument,
  type UnitState,
} from "./progress";
import { type Phase, type Syllabus, syllabus } from "./syllabus";
import { weeklySelfPaced } from "./today";

/** The Monday on or before a date. */
export function weekStart(date: string): string {
  return addDays(date, -((weekday(date) + 6) % 7));
}

/** Minutes of work done from `from` to `to`, inclusive, by local date. */
export function minutesDone(
  document: TrainingDocument,
  solves: Solves,
  from: string,
  to: string,
  source: Syllabus = syllabus,
): number {
  let minutes = 0;
  for (const item of everyItem(source)) {
    const status = itemStatus(item, document, solves);
    if (!status.done || !status.at) continue;
    const day = localDateOf(status.at);
    if (day >= from && day <= to) minutes += item.minutes;
  }
  return minutes;
}

/** Days the pace is measured over. */
export const paceWindow = 28;

export interface Projection {
  phase: Phase;
  /** Minutes of written, unfinished work left in the phase's units. */
  remaining: number;
  /** The day it would be done at the measured pace, or null with no pace. */
  done: string | null;
  /** Whether that day is on or before the phase's target end. */
  onTarget: boolean | null;
}

export interface Pace {
  /** This week, Monday to today. */
  weekFrom: string;
  thisWeek: number;
  /** The plan's self-paced minutes a week for the phase. */
  planWeek: number;
  /** Minutes over the last `paceWindow` days. */
  window: number;
  /** Minutes a week, measured. */
  perWeek: number;
  projections: Projection[];
}

export function pace(
  date: string,
  phase: Phase,
  document: TrainingDocument,
  solves: Solves,
  states: Map<string, UnitState>,
  source: Syllabus = syllabus,
): Pace {
  const weekFrom = weekStart(date);
  const thisWeek = minutesDone(document, solves, weekFrom, date, source);
  const window = minutesDone(
    document,
    solves,
    addDays(date, -(paceWindow - 1)),
    date,
    source,
  );
  const perWeek = Math.round((window / paceWindow) * 7);

  const projections: Projection[] = [];
  let carried = 0;
  for (const target of source.phases) {
    let remaining = 0;
    for (const unit of source.units) {
      if (unit.phase !== target.phase || unit.slot || unit.locked) continue;
      const state = states.get(unit.id)?.state;
      if (state === "done" || state === "placed") continue;
      for (const lesson of unit.lessons)
        for (const item of lesson.items)
          if (!itemStatus(item, document, solves).done)
            remaining += item.minutes;
    }
    if (remaining === 0) continue;
    // Phases are worked in order, so a later phase finishes after the
    // earlier ones' work too.
    carried += remaining;
    const days = perWeek > 0 ? Math.ceil((carried / perWeek) * 7) : null;
    const done = days === null ? null : addDays(date, days);
    projections.push({
      phase: target,
      remaining,
      done,
      onTarget: done === null ? null : done <= target.end,
    });
  }

  return {
    weekFrom,
    thisWeek,
    planWeek: weeklySelfPaced(phase),
    window,
    perWeek,
    projections,
  };
}
