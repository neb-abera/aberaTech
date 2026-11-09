/**
 * Next up: what to work on now, at your own pace.
 *
 * Self-paced work is never tied to a weekday. Any day, the list is the next
 * unfinished work in syllabus order across both tracks, interleaved by the
 * plan's split of self-paced time between math and programming, and filled
 * to one session's length. "Keep going" adds the next item past that, with
 * no limit. Nothing piles up: a missed week leaves the same list, starting
 * from the first unfinished item.
 *
 * Contests, the upsolve that follows one, and anything led by a person are
 * synchronous. They live in the Scheduled panel and never appear here.
 */
import { daysBetween } from "./dates";
import {
  checkpointState,
  itemStatus,
  type Solves,
  type TrainingDocument,
  type UnitState,
  unitStates,
} from "./progress";
import {
  type BlockKind,
  type Item,
  type Lesson,
  type Phase,
  phaseOn,
  type Syllabus,
  syllabus,
  type Track,
  type Unit,
  unitsOf,
} from "./syllabus";

/** A review due now, as the review queue (reviews.ts) hands it over. */
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
  | { kind: "item"; minutes: number; unit: Unit; lesson: Lesson; item: Item };

/** Blocks that are self-paced. Contests and upsolves are synchronous. */
export const selfPaced = (block: BlockKind): block is Track =>
  block === "math" || block === "programming";

/** Minutes a week of self-paced work in each track, from the plan's week. */
export function trackMinutes(phase: Phase): Record<Track, number> {
  const minutes: Record<Track, number> = { math: 0, programming: 0 };
  for (const block of phase.week)
    if (selfPaced(block.block)) minutes[block.block] += block.minutes;
  return minutes;
}

/** Self-paced minutes a week in the plan. */
export const weeklySelfPaced = (phase: Phase): number => {
  const minutes = trackMinutes(phase);
  return minutes.math + minutes.programming;
};

/**
 * One session's default length: the self-paced minutes a week divided by
 * the self-paced sessions in the plan's week. Phase 1: 180 / 3 = 60.
 */
export function defaultSession(phase: Phase): number {
  const sessions = phase.week.filter((block) => selfPaced(block.block)).length;
  if (sessions === 0) return 60;
  return Math.round(weeklySelfPaced(phase) / sessions);
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

/** The track's remaining work in its open unit, in order. */
function trackQueue(
  unit: Unit | null,
  document: TrainingDocument,
  solves: Solves,
  now: Date,
): Entry[] {
  if (!unit) return [];
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

/**
 * Two queues merged so each track's share of minutes stays near its weight:
 * the next entry comes from the track furthest below its share.
 */
export function interleave(
  queues: Record<Track, Entry[]>,
  weights: Record<Track, number>,
): Entry[] {
  const order: Track[] = ["math", "programming"];
  const used: Record<Track, number> = { math: 0, programming: 0 };
  const at: Record<Track, number> = { math: 0, programming: 0 };
  const out: Entry[] = [];
  for (;;) {
    const open = order.filter((track) => at[track] < queues[track].length);
    if (open.length === 0) return out;
    const next = open.reduce((best, track) => {
      const share = (t: Track) => used[t] / Math.max(weights[t], 1);
      return share(track) < share(best) ? track : best;
    });
    const entry = queues[next][at[next]];
    at[next] += 1;
    used[next] += entry.minutes;
    out.push(entry);
  }
}

export interface NextUp {
  date: string;
  phase: Phase;
  /** 1-based week of the phase's target dates. */
  week: number;
  weeks: number;
  /** The session's length, in minutes. */
  session: number;
  entries: Entry[];
  /** Minutes of the entries. */
  planned: number;
  /** Entries left after these. */
  later: number;
  /** Tracks whose open unit is the last one written. */
  lastWritten: Track[];
  /** Minutes of review due before `date`. */
  overdue: number;
  /**
   * More than a session of review is overdue, so new material waits and
   * the list holds reviews alone.
   */
  newWaits: boolean;
}

/**
 * The next session's work. `more` is how many entries "Keep going" has
 * added past the session's length. `date` is the owner's local date and
 * only names the phase whose split and session length apply: no date opens
 * or holds back any unit.
 */
export function nextUp(
  date: string,
  document: TrainingDocument,
  solves: Solves,
  now: Date,
  options: { session?: number; more?: number; reviews?: DueReview[] } = {},
  from: Syllabus = syllabus,
): NextUp {
  const phase = phaseOn(date, from);
  const session = options.session ?? defaultSession(phase);
  const states = unitStates(document, solves, now, from);

  const weights = trackMinutes(phase);
  const queues: Record<Track, Entry[]> = {
    math: trackQueue(currentUnit("math", states, from), document, solves, now),
    programming: trackQueue(
      currentUnit("programming", states, from),
      document,
      solves,
      now,
    ),
  };
  const reviews: Entry[] = (options.reviews ?? []).map((review) => ({
    kind: "review",
    minutes: review.minutes,
    review,
  }));
  // Review comes first. While more than a session's worth is overdue, it
  // is all there is: new material waits until the backlog is under a day.
  const overdue = (options.reviews ?? [])
    .filter((review) => review.due < date)
    .reduce((sum, review) => sum + review.minutes, 0);
  const newWaits = overdue > session;
  const all = newWaits ? reviews : [...reviews, ...interleave(queues, weights)];

  let planned = 0;
  let count = 0;
  for (const entry of all) {
    // The first entry always shows, even when it alone is longer than the
    // session: a 75 minute paper in a 60 minute session is still next.
    if (count > 0 && planned + entry.minutes > session) break;
    planned += entry.minutes;
    count += 1;
  }
  const extra = Math.min(options.more ?? 0, all.length - count);
  const entries = all.slice(0, count + extra);
  planned = entries.reduce((sum, entry) => sum + entry.minutes, 0);

  const lastWritten = (["math", "programming"] as Track[]).filter((track) => {
    const open = currentUnit(track, states, from);
    if (!open) return false;
    const units = unitsOf(track, from).filter((unit) => !unit.slot);
    const after = units[units.indexOf(open) + 1];
    return !after || !!after.locked;
  });

  return {
    date,
    phase,
    week: weekOfPhase(phase, date),
    weeks: weeksIn(phase),
    session,
    entries,
    planned,
    later: all.length - entries.length,
    lastWritten,
    overdue,
    newWaits,
  };
}
