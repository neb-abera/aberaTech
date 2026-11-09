/**
 * The competitive math and programming syllabus, as data.
 *
 * The page coordinates other systems. It writes no lesson text a source
 * already teaches: each item names the system, the exact place in it, what
 * to do there, how long it takes, and how it is recorded. The order, the
 * week, and the rules for when to read an editorial are the page's own.
 *
 * data/syllabus.json was built on 9 Nov 2026 from the plan in Notion and
 * from each source: the AoPS tables of contents for chapters and pages,
 * AoPS's videos page for the videos and YouTube for their lengths, the
 * USACO Guide repository for module ids and titles, the CSES problem list,
 * the Codeforces API for problem ratings, and AtCoder Problems for
 * difficulties. data/link-check.json records how every address was opened.
 */
import raw from "../data/syllabus.json";

export type Track = "math" | "programming";
export type ItemKind = "read" | "watch" | "do" | "code";

/** How an item is recorded as done. */
export type Recording =
  | "manual"
  | "codeforces"
  | "atcoder"
  /** Done once the Codeforces API lists a rated contest for the handle. */
  | "codeforces-rated"
  /** Done when its code passes every test on the page. */
  | "drill";

export interface Video {
  title: string;
  youtube: string;
  /** The video's page on AoPS. */
  url: string;
  seconds: number;
}

export interface Problem {
  judge: "codeforces" | "atcoder" | "cses";
  /** "2263B" on Codeforces, "abc482_c" on AtCoder, "1068" on CSES. */
  id: string;
  /** Codeforces problem rating, from its API. */
  rating?: number;
  /** AtCoder Problems difficulty, its own scale. */
  difficulty?: number;
}

export interface Item {
  /** Unique across the syllabus: the key progress and notes are kept under. */
  id: string;
  kind: ItemKind;
  /** A key into `systems`. */
  system: string;
  title: string;
  url?: string;
  /** What to do there. */
  what: string;
  minutes: number;
  /** Where the minutes come from, when they are not the source's own. */
  minutesBasis?: string;
  record: Recording;
  problem?: Problem;
  videos?: Video[];
  links?: { label: string; url: string }[];
  /** The item takes a score, a practice paper out of 150. */
  score?: boolean;
  /** A drill coded on the page and checked against its tests. */
  drill?: Drill;
}

export interface Drill {
  starter: string;
  tests: { stdin: string; stdout: string }[];
}

/** Where a wrong answer sends you: the exact place that teaches it. */
export interface Reference {
  label: string;
  url: string;
  /** Seconds into a video. */
  at?: number;
}

interface QuestionBase {
  id: string;
  prompt: string;
  /** Why the answer is right, shown after any answer. */
  why: string;
  ref: Reference;
}

/**
 * The ways the page tests you. Choice and number answers are graded by the
 * page. A proof is graded by you against its rubric. A card is recalled
 * and rated. Code runs against its tests.
 */
export type Question =
  | (QuestionBase & { type: "choice"; choices: string[]; answer: number })
  | (QuestionBase & {
      type: "number";
      /** The answer, as an integer, a decimal, or a fraction a/b. */
      answer: string;
    })
  | (QuestionBase & { type: "proof"; rubric: string[] })
  | (QuestionBase & { type: "card"; back: string })
  | (QuestionBase & {
      type: "code";
      starter?: string;
      tests: { stdin: string; stdout: string }[];
    });

export interface Lesson {
  id: string;
  title: string;
  items: Item[];
  /** Questions that end the lesson. */
  questions?: Question[];
}

/**
 * A test-out: a few problems in a stated time. Passing it places the unit
 * out and opens the next one. The unit stays on the map for reference.
 */
export interface Checkpoint {
  minutes: number;
  /** What passing means, in one or two sentences. */
  rule: string;
  items: Item[];
}

export interface Unit {
  id: string;
  phase: number;
  track: Track;
  title: string;
  /** Where in the plan the unit comes from. */
  why?: string;
  lessons: Lesson[];
  /** Set for a unit whose lessons are not written yet, with when they will be. */
  locked?: string;
  checkpoint?: Checkpoint;
  /**
   * "contest": worked on contest days rather than in its track's turn, and
   * outside the track's order, so it holds nothing up.
   */
  slot?: "contest";
  /** Question ids, from the unit's lessons, asked before it starts. */
  pretest?: string[];
  /** Question ids asked to close the unit. 90% closes it. */
  mastery?: string[];
}

/** Monday 1 to Sunday 0, as Date.getDay() counts. */
export type Weekday = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export type BlockKind = "contest" | "upsolve" | "math" | "programming";

export interface WeekBlock {
  day: Weekday;
  minutes: number;
  block: BlockKind;
  /** Every Nth week of the phase this block is a mixed review set instead. */
  mixedEvery?: number;
}

export interface Phase {
  phase: number;
  title: string;
  /** ISO dates, inclusive. */
  start: string;
  end: string;
  hoursPerWeek: number;
  week: WeekBlock[];
  /** Where the split by day comes from. */
  weekBasis?: string;
  goals: string[];
  /** The Codeforces rating the phase ends at, where it sets one. */
  ratingGoal?: number;
}

export interface System {
  id: string;
  name: string;
  url: string;
  /** What it is used for here. */
  for: string;
  /** How work there is recorded. */
  records: string;
}

export interface Syllabus {
  source: string;
  builtOn: string;
  codeforcesHandle: string;
  phases: Phase[];
  systems: System[];
  units: Unit[];
}

export const syllabus = raw as unknown as Syllabus;

export const tracks: Track[] = ["math", "programming"];

export const trackLabel: Record<Track, string> = {
  math: "Math",
  programming: "Programming",
};

export const kindLabel: Record<ItemKind, string> = {
  read: "Read",
  watch: "Watch",
  do: "Do",
  code: "Code",
};

export function systemOf(item: Item, from: Syllabus = syllabus): System {
  const found = from.systems.find((system) => system.id === item.system);
  if (!found) throw new Error(`${item.id}: no system ${item.system}`);
  return found;
}

export function allItems(from: Syllabus = syllabus): Item[] {
  return from.units.flatMap((unit) =>
    unit.lessons.flatMap((lesson) => lesson.items),
  );
}

export function allLessons(from: Syllabus = syllabus): Lesson[] {
  return from.units.flatMap((unit) => unit.lessons);
}

/** The unit and lesson an id belongs to, by lesson id or item id. */
export function locate(
  id: string,
  from: Syllabus = syllabus,
): { unit: Unit; lesson?: Lesson; item?: Item } | null {
  for (const unit of from.units) {
    const check = unit.checkpoint?.items.find((item) => item.id === id);
    if (check) return { unit, item: check };
    for (const lesson of unit.lessons) {
      if (lesson.id === id) return { unit, lesson };
      const item = lesson.items.find((candidate) => candidate.id === id);
      if (item) return { unit, lesson, item };
    }
  }
  return null;
}

export const lessonMinutes = (lesson: Lesson): number =>
  lesson.items.reduce((sum, item) => sum + item.minutes, 0);

export const unitMinutes = (unit: Unit): number =>
  unit.lessons.reduce((sum, lesson) => sum + lessonMinutes(lesson), 0);

/** The units of one track, in the order they are worked. */
export const unitsOf = (track: Track, from: Syllabus = syllabus): Unit[] =>
  from.units.filter((unit) => unit.track === track);

/** The phase a date falls in, or the nearest: before the first, the first. */
export function phaseOn(date: string, from: Syllabus = syllabus): Phase {
  const found = from.phases.find(
    (phase) => phase.start <= date && date <= phase.end,
  );
  if (found) return found;
  return date < from.phases[0].start
    ? from.phases[0]
    : from.phases[from.phases.length - 1];
}

/** "1 h 30 min", "45 min", "2 h". */
export function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return `${rest} min`;
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}

/** "1 lesson", "4 lessons". */
export const countOf = (n: number, one: string, many = `${one}s`): string =>
  `${n} ${n === 1 ? one : many}`;

/** "3 problems in 60 minutes" or, in math, "2 problem sets in 50 minutes". */
export function checkpointLabel(unit: Unit): string {
  const checkpoint = unit.checkpoint;
  if (!checkpoint) return "";
  const what = unit.track === "math" ? "problem set" : "problem";
  return `${countOf(checkpoint.items.length, what)} in ${checkpoint.minutes} minutes`;
}
