/**
 * What to do today, in order, with minutes.
 *
 * The plan's week says which kind of block falls on which day and how
 * long it is. A math or programming block takes the reviews due in that
 * track first, then the track's next work: the open unit's checkpoint if
 * it was never tried, otherwise its next unfinished items in order. The
 * list stops at the block's minutes. Whatever does not fit is simply the
 * next block's work, since the next block starts from the first
 * unfinished item again: a missed day moves forward instead of piling up.
 */
import { addDays, daysBetween, weekday } from "./dates";
import {
  checkpointState,
  itemStatus,
  type Solves,
  type TrainingDocument,
  type UnitState,
  unitStates,
} from "./progress";
import {
  type Item,
  type Lesson,
  type Phase,
  phaseOn,
  type Syllabus,
  syllabus,
  type Track,
  type Unit,
  unitsOf,
  type WeekBlock,
} from "./syllabus";

/** A review due today, as the review queue hands it over. */
export interface DueReview {
  key: string;
  track: Track;
  title: string;
  minutes: number;
  unit: Unit;
  lesson?: Lesson;
  item?: Item;
  due: string;
  from: string;
}

export type Entry =
  | { kind: "review"; minutes: number; review: DueReview }
  | {
      kind: "checkpoint";
      minutes: number;
      unit: Unit;
      running?: { ends: string };
    }
  | { kind: "item"; minutes: number; unit: Unit; lesson: Lesson; item: Item }
  | { kind: "contest"; minutes: number }
  | { kind: "upsolve"; minutes: number }
  | { kind: "mixed"; minutes: number; week: number };

export interface Day {
  date: string;
  phase: Phase;
  /** 1-based week of the phase. */
  week: number;
  weeks: number;
  block: WeekBlock | null;
  entries: Entry[];
  /** Minutes of the entries. */
  planned: number;
  /** Items left in the unit after today's, which move to the next block. */
  later: number;
}

export interface Today extends Day {
  /** On a day with no block, the next day that has one. */
  next: Day | null;
}

export function weekOfPhase(phase: Phase, date: string): number {
  return Math.max(1, Math.floor(daysBetween(phase.start, date) / 7) + 1);
}

export const weeksIn = (phase: Phase): number =>
  Math.ceil((daysBetween(phase.start, phase.end) + 1) / 7);

/** The open unit of a track that is next to work, if any. */
export function currentUnit(
  track: Track,
  states: Map<string, UnitState>,
  from: Syllabus = syllabus,
): Unit | null {
  return (
    unitsOf(track, from).find(
      (unit) => !unit.slot && states.get(unit.id)?.state === "open",
    ) ?? null
  );
}

function fill(
  candidates: Entry[],
  minutes: number,
): { entries: Entry[]; planned: number; rest: number } {
  const entries: Entry[] = [];
  let planned = 0;
  let index = 0;
  for (; index < candidates.length; index++) {
    const entry = candidates[index];
    // The first entry always shows, even when it alone is longer than the
    // block: a 75 minute paper on a 60 minute day is still today's work.
    if (entries.length > 0 && planned + entry.minutes > minutes) break;
    entries.push(entry);
    planned += entry.minutes;
  }
  return { entries, planned, rest: candidates.length - index };
}

function trackWork(
  unit: Unit,
  document: TrainingDocument,
  solves: Solves,
  now: Date,
): Entry[] {
  const check = checkpointState(unit, document, solves, now);
  if (unit.checkpoint && check.state === "none")
    return [{ kind: "checkpoint", minutes: unit.checkpoint.minutes, unit }];
  if (unit.checkpoint && check.state === "running")
    return [
      {
        kind: "checkpoint",
        minutes: unit.checkpoint.minutes,
        unit,
        running: { ends: check.ends },
      },
    ];
  const entries: Entry[] = [];
  for (const lesson of unit.lessons)
    for (const item of lesson.items)
      if (!itemStatus(item, document, solves).done)
        entries.push({
          kind: "item",
          minutes: item.minutes,
          unit,
          lesson,
          item,
        });
  return entries;
}

function plan(
  date: string,
  phase: Phase,
  block: WeekBlock | null,
  document: TrainingDocument,
  solves: Solves,
  now: Date,
  reviews: DueReview[],
  from: Syllabus,
): Day {
  const week = weekOfPhase(phase, date);
  const day: Day = {
    date,
    phase,
    week,
    weeks: weeksIn(phase),
    block,
    entries: [],
    planned: 0,
    later: 0,
  };
  if (!block) return day;
  const states = unitStates(document, solves, now, from);

  let candidates: Entry[] = [];
  if (block.block === "contest") {
    // The first rated contest is its own unit until it is done.
    const first = from.units.find(
      (unit) =>
        unit.slot === "contest" && states.get(unit.id)?.state === "open",
    );
    candidates = first
      ? trackWork(first, document, solves, now)
      : [{ kind: "contest", minutes: block.minutes }];
  } else if (block.block === "upsolve") {
    candidates = [{ kind: "upsolve", minutes: block.minutes }];
  } else {
    const track: Track = block.block;
    candidates = reviews
      .filter((review) => review.track === track)
      .map((review) => ({
        kind: "review" as const,
        minutes: review.minutes,
        review,
      }));
    if (block.mixedEvery && week % block.mixedEvery === 0)
      candidates.push({ kind: "mixed", minutes: block.minutes, week });
    else {
      const unit = currentUnit(track, states, from);
      if (unit) candidates.push(...trackWork(unit, document, solves, now));
    }
  }
  const { entries, planned, rest } = fill(candidates, block.minutes);
  return { ...day, entries, planned, later: rest };
}

export function blockOn(phase: Phase, date: string): WeekBlock | null {
  return phase.week.find((block) => block.day === weekday(date)) ?? null;
}

/**
 * Today's work. `date` is the owner's local calendar date. On a day with
 * no block, `next` is the next day that has one, planned as it stands now.
 */
export function today(
  date: string,
  document: TrainingDocument,
  solves: Solves,
  now: Date,
  reviews: DueReview[] = [],
  from: Syllabus = syllabus,
): Today {
  const phase = phaseOn(date, from);
  const block = blockOn(phase, date);
  const day = plan(date, phase, block, document, solves, now, reviews, from);
  if (block) return { ...day, next: null };

  for (let ahead = 1; ahead <= 7; ahead++) {
    const later = addDays(date, ahead);
    const laterPhase = phaseOn(later, from);
    const laterBlock = blockOn(laterPhase, later);
    if (laterBlock)
      return {
        ...day,
        next: plan(
          later,
          laterPhase,
          laterBlock,
          document,
          solves,
          now,
          reviews,
          from,
        ),
      };
  }
  return { ...day, next: null };
}

/** The phase's hours this week, from the plan. */
export const hoursThisWeek = (phase: Phase): number => phase.hoursPerWeek;
