/**
 * The owner's progress through the syllabus, and what it adds up to.
 *
 * One document on the server (key "training") holds what only the owner
 * can say: manual ticks, missed marks, practice paper scores, checkpoint
 * starts, review dates, and notes. What a judge can say, a Codeforces or
 * AtCoder solve, is never stored here. The page asks the server for it at
 * load and passes it in as `Solves`.
 */
import {
  type Attempt,
  type ExternalRecord,
  masteryState,
  placementState,
  quizTaken,
} from "./assessment";
import type { Help, Solve, SpeedDrill } from "./log";
import {
  allItems,
  type Item,
  type Lesson,
  type Syllabus,
  syllabus,
  type Track,
  type Unit,
  unitsOf,
} from "./syllabus";

export const trainingDocumentKey = "training";

export interface ItemRecord {
  /** When it was ticked, an ISO date and time. */
  at: string;
  /** Some of it was missed. It comes back for review. */
  missed?: boolean;
  /** A practice paper's score out of 150. */
  score?: number;
}

export interface CheckpointAttempt {
  /** When Start was pressed, an ISO date and time. */
  started: string;
  /** Set when the owner stopped it before the time ran out. */
  stopped?: string;
}

export interface Note {
  text: string;
  at: string;
}

export interface TrainingDocument {
  version: 1;
  settings: {
    codeforces: string;
    atcoder: string;
    /** Minutes in one session of Next up. Unset: the plan's default. */
    session?: number;
  };
  items: Record<string, ItemRecord>;
  checkpoints: Record<string, CheckpointAttempt[]>;
  /** Review done, by review key (reviews.ts), an ISO date and time. */
  reviews: Record<string, string>;
  /** By lesson id or item id. */
  notes: Record<string, Note>;
  /** The latest source of each drill, by item id. */
  code: Record<string, string>;
  /** FSRS state by review key (reviews.ts), once a key has been reviewed. */
  cards: Record<string, ReviewCard>;
  /** Attempts by check key (assessment.ts): quizzes, pretests, mastery checks. */
  assessments: Record<string, Attempt[]>;
  /** Outside checks recorded by hand, by "placement:<unit>" or "mastery:<unit>". */
  external: Record<string, ExternalRecord>;
  /** The solve log, oldest first (log.ts). */
  log: Solve[];
  /** A speed drill's running timer. */
  speed?: SpeedDrill;
  /** Saad's weekly checklist, by the Monday of the week: yes or no per line. */
  weekly: Record<string, boolean[]>;
}

/**
 * One review key's FSRS card, as ts-fsrs keeps it, with dates as ISO
 * strings. `relearning` is set from a miss until two correct answers on
 * two later days (reviews.ts).
 */
export interface ReviewCard {
  due: string;
  stability: number;
  difficulty: number;
  scheduled_days: number;
  learning_steps: number;
  reps: number;
  lapses: number;
  /** ts-fsrs State: 0 new, 1 learning, 2 review, 3 relearning. */
  state: 0 | 1 | 2 | 3;
  last_review?: string;
  relearning?: {
    /** When it was missed, an ISO date and time. */
    since: string;
    /** Local dates after the miss with a correct answer, one per day. */
    correct: string[];
  };
}

export const emptyTraining = (from: Syllabus = syllabus): TrainingDocument => ({
  version: 1,
  settings: { codeforces: from.codeforcesHandle, atcoder: "" },
  items: {},
  checkpoints: {},
  reviews: {},
  notes: {},
  code: {},
  cards: {},
  assessments: {},
  external: {},
  log: [],
  weekly: {},
});

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const isIso = (value: unknown): value is string =>
  typeof value === "string" && !Number.isNaN(Date.parse(value));

function entries<T>(
  value: unknown,
  keep: (entry: unknown) => T | null,
): Record<string, T> {
  const out: Record<string, T> = {};
  if (!isRecord(value)) return out;
  for (const [key, entry] of Object.entries(value)) {
    const kept = keep(entry);
    if (kept !== null) out[key] = kept;
  }
  return out;
}

/** Whatever came back, made whole: a missing or malformed part is empty. */
export function normalizeTraining(
  value: unknown,
  from: Syllabus = syllabus,
): TrainingDocument {
  const empty = emptyTraining(from);
  if (!isRecord(value)) return empty;
  const settings = isRecord(value.settings) ? value.settings : {};
  return {
    version: 1,
    settings: {
      codeforces:
        typeof settings.codeforces === "string"
          ? settings.codeforces
          : empty.settings.codeforces,
      atcoder: typeof settings.atcoder === "string" ? settings.atcoder : "",
      ...(typeof settings.session === "number" &&
      settings.session >= 10 &&
      settings.session <= 600
        ? { session: Math.round(settings.session) }
        : {}),
    },
    items: entries(value.items, (entry) => {
      if (!isRecord(entry) || !isIso(entry.at)) return null;
      const record: ItemRecord = { at: entry.at };
      if (entry.missed === true) record.missed = true;
      if (typeof entry.score === "number" && Number.isFinite(entry.score))
        record.score = entry.score;
      return record;
    }),
    checkpoints: entries(value.checkpoints, (entry) => {
      if (!Array.isArray(entry)) return null;
      return entry
        .filter(
          (attempt): attempt is CheckpointAttempt =>
            isRecord(attempt) && isIso(attempt.started),
        )
        .map((attempt) =>
          isIso(attempt.stopped)
            ? { started: attempt.started, stopped: attempt.stopped }
            : { started: attempt.started },
        );
    }),
    reviews: entries(value.reviews, (entry) => (isIso(entry) ? entry : null)),
    notes: entries(value.notes, (entry) =>
      isRecord(entry) && typeof entry.text === "string" && isIso(entry.at)
        ? { text: entry.text, at: entry.at }
        : null,
    ),
    code: entries(value.code, (entry) =>
      typeof entry === "string" ? entry : null,
    ),
    cards: entries(value.cards, normalizeCard),
    assessments: entries(value.assessments, (entry) =>
      Array.isArray(entry)
        ? entry.flatMap((attempt) => {
            const kept = normalizeAttempt(attempt);
            return kept ? [kept] : [];
          })
        : null,
    ),
    log: Array.isArray(value.log)
      ? value.log.flatMap((entry) => {
          const kept = normalizeSolve(entry);
          return kept ? [kept] : [];
        })
      : [],
    ...(isRecord(value.speed) &&
    (value.speed.drill === "A" || value.speed.drill === "B") &&
    typeof value.speed.problem === "string" &&
    isIso(value.speed.started)
      ? {
          speed: {
            drill: value.speed.drill,
            problem: value.speed.problem.slice(0, 300),
            started: value.speed.started,
          },
        }
      : {}),
    weekly: entries(value.weekly, (entry) =>
      Array.isArray(entry) ? entry.map((answer) => answer === true) : null,
    ),
    external: entries(value.external, (entry) => {
      if (!isRecord(entry) || !isIso(entry.at)) return null;
      return isNumber(entry.score)
        ? { at: entry.at, score: entry.score }
        : { at: entry.at };
    }),
  };
}

const minutes = (value: unknown): number =>
  isNumber(value) && value >= 0 ? Math.min(Math.round(value), 24 * 60) : 0;

function normalizeSolve(value: unknown): Solve | null {
  if (!isRecord(value) || !isIso(value.at) || typeof value.id !== "string")
    return null;
  if (typeof value.problem !== "string" || value.problem.trim() === "")
    return null;
  const help: Help =
    value.help === "hint" || value.help === "editorial" ? value.help : "alone";
  const solve: Solve = {
    id: value.id.slice(0, 40),
    at: value.at,
    problem: value.problem.slice(0, 300),
    read: minutes(value.read),
    think: minutes(value.think),
    code: minutes(value.code),
    debug: minutes(value.debug),
    submissions: Math.min(minutes(value.submissions), 999),
    help,
  };
  if (typeof value.url === "string" && /^https:\/\//.test(value.url))
    solve.url = value.url.slice(0, 300);
  if (isNumber(value.level)) solve.level = value.level;
  if (value.drill === "A" || value.drill === "B") solve.drill = value.drill;
  return solve;
}

function normalizeAttempt(value: unknown): Attempt | null {
  if (!isRecord(value) || !isIso(value.at)) return null;
  const answers = entries(value.answers, (entry) => {
    if (!isRecord(entry) || typeof entry.correct !== "boolean") return null;
    const answer: Attempt["answers"][string] = { correct: entry.correct };
    if (typeof entry.given === "string")
      answer.given = entry.given.slice(0, 8000);
    if (
      entry.rating === 1 ||
      entry.rating === 2 ||
      entry.rating === 3 ||
      entry.rating === 4
    )
      answer.rating = entry.rating;
    const proof = entry.proof;
    if (isRecord(proof) && Array.isArray(proof.met))
      answer.proof = {
        met: proof.met.map((met) => met === true),
        elegant: proof.elegant === true,
        clear: proof.clear === true,
      };
    return answer;
  });
  return { at: value.at, answers };
}

const isNumber = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value);

const isDate = (value: unknown): value is string =>
  typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value);

function normalizeCard(entry: unknown): ReviewCard | null {
  if (!isRecord(entry) || !isIso(entry.due)) return null;
  const numbers = [
    "stability",
    "difficulty",
    "scheduled_days",
    "learning_steps",
    "reps",
    "lapses",
  ] as const;
  if (!numbers.every((name) => isNumber(entry[name]))) return null;
  if (
    entry.state !== 0 &&
    entry.state !== 1 &&
    entry.state !== 2 &&
    entry.state !== 3
  )
    return null;
  const card: ReviewCard = {
    due: entry.due,
    stability: entry.stability as number,
    difficulty: entry.difficulty as number,
    scheduled_days: entry.scheduled_days as number,
    learning_steps: entry.learning_steps as number,
    reps: entry.reps as number,
    lapses: entry.lapses as number,
    state: entry.state,
  };
  if (isIso(entry.last_review)) card.last_review = entry.last_review;
  const relearning = entry.relearning;
  if (isRecord(relearning) && isIso(relearning.since))
    card.relearning = {
      since: relearning.since,
      correct: Array.isArray(relearning.correct)
        ? relearning.correct.filter(isDate)
        : [],
    };
  return card;
}

/**
 * What the judges say, read live by the server. A judge that could not be
 * read is `null`: its items fall back to manual ticks, labeled as such.
 */
export interface JudgeSolves {
  /** Problem id to the time of the first accepted submission, ISO. */
  solved: Record<string, string>;
  /** Rated contests entered, where the judge reports it. */
  ratedContests?: number;
}

export interface Solves {
  codeforces: JudgeSolves | null;
  atcoder: JudgeSolves | null;
}

export const noSolves: Solves = { codeforces: null, atcoder: null };

export type DoneBy = "manual" | "codeforces" | "atcoder" | "drill";

export interface ItemStatus {
  done: boolean;
  /** When, an ISO date and time. */
  at?: string;
  by?: DoneBy;
  missed: boolean;
  /** Recorded by an API, so no manual tick is offered. */
  verified: boolean;
  score?: number;
}

/** Whether the item is recorded by a judge whose API answered. */
export function judgeFor(
  item: Item,
  solves: Solves,
): { judge: "codeforces" | "atcoder"; data: JudgeSolves } | null {
  if (item.record === "codeforces" || item.record === "codeforces-rated")
    return solves.codeforces
      ? { judge: "codeforces", data: solves.codeforces }
      : null;
  if (item.record === "atcoder")
    return solves.atcoder ? { judge: "atcoder", data: solves.atcoder } : null;
  return null;
}

export function itemStatus(
  item: Item,
  document: TrainingDocument,
  solves: Solves = noSolves,
): ItemStatus {
  const record = document.items[item.id];
  const missed = record?.missed === true;
  const judge = judgeFor(item, solves);
  if (judge && item.problem) {
    const at = judge.data.solved[item.problem.id];
    if (at) return { done: true, at, by: judge.judge, missed, verified: true };
  }
  if (
    judge &&
    item.record === "codeforces-rated" &&
    (judge.data.ratedContests ?? 0) > 0
  )
    return { done: true, by: "codeforces", missed, verified: true };
  if (record)
    return {
      done: true,
      at: record.at,
      by: item.record === "drill" ? "drill" : "manual",
      missed,
      verified: false,
      score: record.score,
    };
  return { done: false, missed, verified: judge !== null && !!item.problem };
}

/**
 * Whether the owner may tick the item by hand. A problem a judge's API
 * answered for counts only when the judge sees it solved. A rated contest
 * may still be an AtCoder one, so it keeps its tick.
 */
export function canTick(item: Item, solves: Solves): boolean {
  if (item.record === "drill") return false;
  if (item.record === "manual" || item.record === "codeforces-rated")
    return true;
  return judgeFor(item, solves) === null;
}

export function toggleItem(
  document: TrainingDocument,
  id: string,
  now: Date,
): TrainingDocument {
  const items = { ...document.items };
  if (items[id]) delete items[id];
  else items[id] = { at: now.toISOString() };
  return { ...document, items };
}

/** Mark an item missed, ticking it done at the same time, or clear the mark. */
export function setMissed(
  document: TrainingDocument,
  id: string,
  missed: boolean,
  now: Date,
): TrainingDocument {
  const current = document.items[id];
  const next: ItemRecord = { ...(current ?? { at: now.toISOString() }) };
  if (missed) next.missed = true;
  else delete next.missed;
  return { ...document, items: { ...document.items, [id]: next } };
}

/** A drill passed every test: done now. */
export function passDrill(
  document: TrainingDocument,
  id: string,
  now: Date,
): TrainingDocument {
  if (document.items[id]) return document;
  return {
    ...document,
    items: { ...document.items, [id]: { at: now.toISOString() } },
  };
}

export function setCode(
  document: TrainingDocument,
  id: string,
  source: string,
): TrainingDocument {
  return { ...document, code: { ...document.code, [id]: source } };
}

export function setScore(
  document: TrainingDocument,
  id: string,
  score: number | null,
  now: Date,
): TrainingDocument {
  const current = document.items[id];
  const next: ItemRecord = { ...(current ?? { at: now.toISOString() }) };
  if (score === null) delete next.score;
  else next.score = score;
  return { ...document, items: { ...document.items, [id]: next } };
}

// ---------------------------------------------------------------- checkpoints

export type CheckpointState =
  | { state: "none" }
  | { state: "running"; started: string; ends: string }
  | { state: "passed"; started: string; at: string }
  | { state: "failed"; started: string };

const plusMinutes = (iso: string, minutes: number): string =>
  new Date(Date.parse(iso) + minutes * 60_000).toISOString();

/**
 * The latest attempt at a unit's checkpoint, judged. It passes when every
 * checkpoint item is done no later than the start plus the stated time. A
 * problem solved on the judge before the start counts: the point is the
 * skill, and the judge's record shows it.
 */
export function checkpointState(
  unit: Unit,
  document: TrainingDocument,
  solves: Solves,
  now: Date,
): CheckpointState {
  const checkpoint = unit.checkpoint;
  const attempts = document.checkpoints[unit.id] ?? [];
  if (!checkpoint || attempts.length === 0) return { state: "none" };

  for (const attempt of [...attempts].reverse()) {
    const ends = plusMinutes(attempt.started, checkpoint.minutes);
    const statuses = checkpoint.items.map((item) =>
      itemStatus(item, document, solves),
    );
    const allIn = statuses.every(
      (status) => status.done && !status.missed && (status.at ?? "") <= ends,
    );
    if (allIn) {
      const at = statuses
        .map((status) => status.at ?? attempt.started)
        .reduce((latest, at) => (at > latest ? at : latest), attempt.started);
      return { state: "passed", started: attempt.started, at };
    }
  }

  const latest = attempts[attempts.length - 1];
  const ends = plusMinutes(latest.started, checkpoint.minutes);
  if (!latest.stopped && now.toISOString() < ends)
    return { state: "running", started: latest.started, ends };
  return { state: "failed", started: latest.started };
}

export function startCheckpoint(
  document: TrainingDocument,
  unitId: string,
  now: Date,
): TrainingDocument {
  return {
    ...document,
    checkpoints: {
      ...document.checkpoints,
      [unitId]: [
        ...(document.checkpoints[unitId] ?? []),
        { started: now.toISOString() },
      ],
    },
  };
}

export function stopCheckpoint(
  document: TrainingDocument,
  unitId: string,
  now: Date,
): TrainingDocument {
  const attempts = [...(document.checkpoints[unitId] ?? [])];
  const last = attempts[attempts.length - 1];
  if (!last || last.stopped) return document;
  attempts[attempts.length - 1] = { ...last, stopped: now.toISOString() };
  return {
    ...document,
    checkpoints: { ...document.checkpoints, [unitId]: attempts },
  };
}

// ---------------------------------------------------------------- units

export interface Count {
  done: number;
  total: number;
  minutesDone: number;
  minutesTotal: number;
}

export function countItems(
  items: Item[],
  document: TrainingDocument,
  solves: Solves,
): Count {
  let done = 0;
  let minutesDone = 0;
  let minutesTotal = 0;
  for (const item of items) {
    minutesTotal += item.minutes;
    if (itemStatus(item, document, solves).done) {
      done += 1;
      minutesDone += item.minutes;
    }
  }
  return { done, total: items.length, minutesDone, minutesTotal };
}

/** Every item done, and its quiz taken when it has one (assessment.ts). */
export const lessonDone = (
  lesson: Lesson,
  document: TrainingDocument,
  solves: Solves,
): boolean =>
  lesson.items.every((item) => itemStatus(item, document, solves).done) &&
  quizTaken(lesson, document);

export type UnitState =
  /** Its lessons are not written yet. */
  | { state: "locked"; reason: string }
  /** The unit before it in its track is not finished. */
  | { state: "waiting"; after: Unit }
  | { state: "open" }
  /** Closed: its lessons done, or its mastery check passed. */
  | { state: "done"; by?: "lessons" | "mastery" }
  | {
      state: "placed";
      at: string;
      started: string;
      by?: "checkpoint" | "pretest" | "placement";
    };

/** Whether the unit counts as finished for what comes after it. */
export const finished = (state: UnitState): boolean =>
  state.state === "done" || state.state === "placed";

/**
 * Every unit's state, in syllabus order. A unit opens when the one before
 * it in its track is done or placed out. A unit placed out stays on the
 * map and its lessons stay open for reference. It is never scheduled.
 */
export function unitStates(
  document: TrainingDocument,
  solves: Solves,
  now: Date,
  from: Syllabus = syllabus,
): Map<string, UnitState> {
  const states = new Map<string, UnitState>();
  const tracks: Track[] = ["math", "programming"];
  for (const track of tracks) {
    let previous: Unit | null = null;
    let previousFinished = true;
    for (const unit of unitsOf(track, from)) {
      let state: UnitState;
      if (unit.locked) state = { state: "locked", reason: unit.locked };
      else {
        const placed = checkpointState(unit, document, solves, now);
        const placement = placementState(unit, document, from);
        const mastery = masteryState(unit, document, from);
        const mastered = mastery.state === "passed";
        const needsMastery =
          (unit.mastery?.length ?? 0) > 0 || !!unit.externalMastery;
        if (placed.state === "passed")
          state = {
            state: "placed",
            at: placed.at,
            started: placed.started,
            by: "checkpoint",
          };
        else if (placement.state === "passed")
          state = {
            state: "placed",
            at: placement.at,
            started: placement.at,
            by: placement.by === "outside" ? "placement" : "pretest",
          };
        else if (mastered) state = { state: "done", by: "mastery" };
        else if (
          !needsMastery &&
          unit.lessons.every((lesson) => lessonDone(lesson, document, solves))
        )
          state = { state: "done", by: "lessons" };
        else if (!previousFinished && previous && !unit.slot)
          state = { state: "waiting", after: previous };
        else state = { state: "open" };
      }
      states.set(unit.id, state);
      if (unit.slot) continue;
      previousFinished = finished(state);
      previous = unit;
    }
  }
  return states;
}

/** Progress over everything written so far, with placed-out units counted whole. */
export function overall(
  document: TrainingDocument,
  solves: Solves,
  states: Map<string, UnitState>,
  from: Syllabus = syllabus,
): Count {
  const total: Count = { done: 0, total: 0, minutesDone: 0, minutesTotal: 0 };
  for (const unit of from.units) {
    const items = unit.lessons.flatMap((lesson) => lesson.items);
    const count = countItems(items, document, solves);
    const placed = states.get(unit.id)?.state === "placed";
    total.total += count.total;
    total.minutesTotal += count.minutesTotal;
    total.done += placed ? count.total : count.done;
    total.minutesDone += placed ? count.minutesTotal : count.minutesDone;
  }
  return total;
}

/** Every item id in the syllabus, checkpoints included. */
export function everyItem(from: Syllabus = syllabus): Item[] {
  return [
    ...allItems(from),
    ...from.units.flatMap((unit) => unit.checkpoint?.items ?? []),
  ];
}
