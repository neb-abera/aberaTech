/**
 * Mostafa Saad's supervision pool, his level table, and his practice mix.
 *
 * His sheet rates each problem's level from 1 to 10 against Codeforces
 * problem letters. His table, on the sheet's front tab:
 *
 *   Div. 2 A 1 to 2, B 1.5 to 3, C 3 to 5.5, D 5 to 6.5, E 6 to 7.5.
 *
 * A rating maps to a letter by the median Codeforces rating of each letter
 * in Div. 2 rounds, read from the Codeforces API (problemset.problems and
 * contest.list) on 9 Nov 2026: A 800, B 1200, C 1600, D 1900, E 2300. The
 * level of a rating is the midpoint of that letter's range, and between two
 * letters it is interpolated in a straight line.
 *
 * His mix, from the sheet's "How to train": 10% of problems below your
 * level, 20% at it, 60% slightly above, and 10% far above. "If the
 * trainee's level is 6.5, I sample problems of level 6.25 to 6.75 of
 * P5/P4/P3." The widths of the other three bands are the page's own.
 */

export const poolSheet =
  "https://docs.google.com/spreadsheets/d/1-n9Fnvhsnvsqh-IerE_yyIshw5RUNer_7EjwF_GW-TA";

export interface PoolProblem {
  code: string;
  level: number;
  /** 5 for P5, important, to 3 for P3, interesting. */
  importance: number;
  url: string | null;
  /** The Codeforces problem id, when it is one. */
  codeforces: string | null;
  editorial: boolean;
}

/** His table: each Div. 2 letter's range of levels. */
export const letterLevels = [
  { letter: "A", from: 1, to: 2 },
  { letter: "B", from: 1.5, to: 3 },
  { letter: "C", from: 3, to: 5.5 },
  { letter: "D", from: 5, to: 6.5 },
  { letter: "E", from: 6, to: 7.5 },
] as const;

/** Median Codeforces rating of each Div. 2 letter, from the API on 9 Nov 2026. */
export const letterRatings: Record<string, number> = {
  A: 800,
  B: 1200,
  C: 1600,
  D: 1900,
  E: 2300,
};

export const tableSource =
  "Levels: Mostafa Saad's table (Div. 2 A 1 to 2, B 1.5 to 3, C 3 to 5.5, D 5 to 6.5, E 6 to 7.5). Ratings: the median Codeforces rating of each Div. 2 letter, from the Codeforces API on 9 Nov 2026 (A 800, B 1200, C 1600, D 1900, E 2300).";

const points = letterLevels.map((range) => ({
  rating: letterRatings[range.letter],
  level: (range.from + range.to) / 2,
}));

/** A Codeforces rating as a level on Saad's scale, 1 to 9.75. */
export function levelOf(rating: number): number {
  const first = points[0];
  if (rating <= first.rating) return first.level;
  for (let i = 1; i < points.length; i += 1) {
    const a = points[i - 1];
    const b = points[i];
    if (rating <= b.rating)
      return (
        a.level +
        ((rating - a.rating) * (b.level - a.level)) / (b.rating - a.rating)
      );
  }
  // Past E: E's slope from D, capped at the top of his scale.
  const d = points[points.length - 2];
  const e = points[points.length - 1];
  const slope = (e.level - d.level) / (e.rating - d.rating);
  return Math.min(9.75, e.level + (rating - e.rating) * slope);
}

export type Band = "below" | "at" | "above" | "far";

/** His mix, out of ten problems, and each band's levels around yours. */
export const mix: {
  band: Band;
  label: string;
  count: number;
  from: number;
  to: number;
}[] = [
  {
    band: "below",
    label: "Below your level, for speed",
    count: 1,
    from: -1.5,
    to: -0.25,
  },
  { band: "at", label: "At your level", count: 2, from: -0.25, to: 0.25 },
  {
    band: "above",
    label: "Slightly above, for growth",
    count: 6,
    from: 0.25,
    to: 1,
  },
  { band: "far", label: "Far above", count: 1, from: 1.5, to: 3 },
];

/**
 * A Codeforces problemset problem in the owner's band, from the
 * problemset.problems API. Its tags are never sent: the plan's practice in
 * a band is untagged.
 */
export interface BandProblem {
  /** The Codeforces problem id, "2263B". */
  id: string;
  name: string;
  rating: number;
  url: string;
}

/**
 * An AtCoder problem in the owner's band, from AtCoder Problems'
 * problems.json and problem-models.json. AtCoder problems carry no tags.
 */
export interface AtCoderProblem {
  /** The AtCoder task id, "abc482_c". */
  id: string;
  /** The contest id, "abc482". */
  contest: string;
  /** The task letter, "C". */
  index: string;
  name: string;
  /** The difficulty as AtCoder Problems shows it, on AtCoder's scale. */
  difficulty: number;
  url: string;
}

/** The AtCoder half of the band: shown difficulty `from` to `to`. */
export interface AtCoderList {
  ok: boolean;
  error?: string | null;
  from: number;
  to: number;
  problems: AtCoderProblem[];
}

/** The band list the server read: ratings `from` to `to`. */
export interface BandList {
  from: number;
  to: number;
  problems: BandProblem[];
  /** Absent from a server older than the AtCoder half. */
  atcoder?: AtCoderList;
}

export const atcoderScaleSource =
  "AtCoder difficulty from a Codeforces rating: the straight line in silverfoxxxy's Rating converter (silverfoxxxy.github.io/rating-converter, read 11 Nov 2026), on which Codeforces 0 is AtCoder -940 and Codeforces 3900 is AtCoder 4215.";

export type Pick =
  | { source: "saad"; problem: PoolProblem; band: Band }
  | {
      source: "codeforces";
      problem: BandProblem;
      /** The slot of his mix it fills, since his pool had none to fit. */
      band: Band;
    }
  | { source: "atcoder"; problem: AtCoderProblem; band: Band };

export type Judge = "codeforces" | "atcoder";

/**
 * Which judge fills each of `count` band slots. The plan names AtCoder
 * Problems by difficulty and the Codeforces problemset by rating side by
 * side and states no ratio, so the slots alternate, Codeforces first. A
 * judge with no problems left gives its turns to the other.
 */
export function splitSlots(
  count: number,
  codeforces: number,
  atcoder: number,
): Judge[] {
  const left = { codeforces, atcoder };
  const order: Judge[] = [];
  let turn: Judge = "codeforces";
  while (order.length < count && left.codeforces + left.atcoder > 0) {
    const judge: Judge =
      left[turn] > 0 ? turn : turn === "codeforces" ? "atcoder" : "codeforces";
    order.push(judge);
    left[judge] -= 1;
    turn = turn === "codeforces" ? "atcoder" : "codeforces";
  }
  return order;
}

/** A stable shuffle key: the same order all week, a new one the next. */
function hash(text: string): number {
  let h = 2166136261;
  for (let i = 0; i < text.length; i += 1) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

/**
 * Ten problems by Saad's mix around `level`, none already solved and none
 * without a working link (his UVA, TJU, LiveArchive and TopCoder rows).
 * Within a band, higher importance first, then a shuffle that holds for
 * the week. A band his pool cannot fill takes Codeforces and AtCoder
 * problems in the owner's band (`splitSlots`), never a level outside it: a far-above problem in a
 * "slightly above" slot breaks his mix.
 */
export function pickMix(
  pool: PoolProblem[],
  level: number,
  week: string,
  solved: Set<string>,
  band: BandProblem[] = [],
  atcoder: AtCoderProblem[] = [],
): Pick[] {
  const open = openPool(pool, solved);
  const order = (a: PoolProblem, b: PoolProblem) =>
    b.importance - a.importance ||
    hash(`${week}:${a.code}`) - hash(`${week}:${b.code}`);
  const taken = new Set<string>();
  const picks: Pick[] = [];
  const short: Band[] = [];
  for (const slot of mix) {
    const lo = level + slot.from;
    const hi = level + slot.to;
    const inside = open
      .filter(
        (problem) =>
          !taken.has(problem.code) &&
          problem.level >= lo &&
          (slot.band === "at" ? problem.level <= hi : problem.level < hi),
      )
      .sort(order)
      .slice(0, slot.count);
    for (const problem of inside) {
      taken.add(problem.code);
      picks.push({ source: "saad", problem, band: slot.band });
    }
    for (let i = inside.length; i < slot.count; i += 1) short.push(slot.band);
  }
  const weekly = (a: { id: string }, b: { id: string }) =>
    hash(`${week}:${a.id}`) - hash(`${week}:${b.id}`);
  const codeforces = band
    .filter(
      (problem) =>
        !solved.has(problem.id) &&
        !open.some((each) => each.codeforces === problem.id),
    )
    .sort(weekly);
  const fromAtCoder = atcoder
    .filter((problem) => !solved.has(problem.id))
    .sort(weekly);
  splitSlots(short.length, codeforces.length, fromAtCoder.length).forEach(
    (judge, i) => {
      if (judge === "codeforces") {
        const problem = codeforces.shift();
        if (problem) picks.push({ source: judge, problem, band: short[i] });
      } else {
        const problem = fromAtCoder.shift();
        if (problem) picks.push({ source: judge, problem, band: short[i] });
      }
    },
  );
  return picks;
}

/** His problems still open to pick: unsolved, with a page to open. */
function openPool(pool: PoolProblem[], solved: Set<string>): PoolProblem[] {
  return pool.filter(
    (problem) =>
      problem.url !== null &&
      !solved.has(problem.code) &&
      !(problem.codeforces && solved.has(problem.codeforces)),
  );
}

/** A level on Saad's scale as a Codeforces rating: the inverse of levelOf. */
export function ratingOf(level: number): number {
  const first = points[0];
  if (level <= first.level) return first.rating;
  for (let i = 1; i < points.length; i += 1) {
    const a = points[i - 1];
    const b = points[i];
    if (level <= b.level)
      return (
        a.rating +
        ((level - a.level) * (b.rating - a.rating)) / (b.level - a.level)
      );
  }
  const d = points[points.length - 2];
  const e = points[points.length - 1];
  return (
    e.rating + ((level - e.level) * (e.rating - d.rating)) / (e.level - d.level)
  );
}

/**
 * Where his pool starts for the owner: the lowest level of an open, linked
 * problem, the Div. 2 letter whose range it opens, and the rating that
 * level maps to, to the nearest 50.
 */
export function poolStart(
  pool: PoolProblem[],
  solved: Set<string>,
): { level: number; letter: string; rating: number } | null {
  const levels = openPool(pool, solved).map((problem) => problem.level);
  if (levels.length === 0) return null;
  const level = Math.min(...levels);
  const letter =
    [...letterLevels].reverse().find((range) => range.from <= level)?.letter ??
    "A";
  return { level, letter, rating: Math.round(ratingOf(level) / 50) * 50 };
}

/**
 * His weekly checklist, from the Junior Training Sheet's Checklist tab, as
 * questions where yes means no work is needed there.
 */
export const weeklyChecklist: { area: string; question: string }[] = [
  { area: "Reading", question: "Read short problems within 3 to 5 minutes?" },
  {
    area: "Reading",
    question: "Never doubted later that you understood a problem?",
  },
  {
    area: "Thinking",
    question: "Ready and in a challenging mood before starting?",
  },
  { area: "Thinking", question: "Fought each problem for a reasonable time?" },
  {
    area: "Thinking",
    question:
      "Verified each solution (test cases, extra cases, correctness, time and memory) before coding?",
  },
  {
    area: "Coding",
    question: "Sketched the code's big picture before typing?",
  },
  { area: "Coding", question: "Coded within 10 minutes?" },
  { area: "Coding", question: "Little copy and paste?" },
  {
    area: "Debugging",
    question: "Chose printing or a debugger by which finds the bug faster?",
  },
  { area: "Debugging", question: "Fixed bugs within 10 minutes?" },
  {
    area: "Code is ready",
    question:
      "Submitted only when almost sure it passes, as in a real contest?",
  },
  {
    area: "Code failed",
    question:
      "Stayed calm, revised the idea, and traced samples for 15 minutes or more before looking at tests?",
  },
  { area: "Got it accepted", question: "Read and understood the editorial?" },
  {
    area: "Got it accepted",
    question: "Checked 1 to 3 other accepted solutions?",
  },
  {
    area: "Got it accepted",
    question: "Tried a shorter or faster version of your code?",
  },
  { area: "Speed", question: "Div. 2 A in 5 minutes and B in 10?" },
  { area: "Contests", question: "Entered 1 or 2 contests this week?" },
  {
    area: "Sheet stats",
    question: "Recorded every solve's times, and read where the time goes?",
  },
  {
    area: "Training time",
    question: "Trained the hours you planned, on a regular schedule?",
  },
  {
    area: "Psychology",
    question:
      "Kept from comparing yourself with others, and from negative talk about yourself?",
  },
];

export const checklistSource =
  "Mostafa Saad's weekly checklist, from the Checklist tab of his Junior Training Sheet.";

export const juniorSheet =
  "https://docs.google.com/spreadsheets/d/1iJZWP2nS_OB3kCTjq8L6TrJJ4o-5lhxDOyTaocSYc-k";

/**
 * The rating to pick around: the open band unit's ("band-1000" is 1000),
 * else the Codeforces rating, else the first band, 800. With its source.
 */
export function currentBand(
  openUnits: { id: string; title: string }[],
  rating: number | null | undefined,
): { rating: number; source: string } {
  for (const unit of openUnits) {
    const match = /^band-(\d+)$/.exec(unit.id);
    if (match)
      return {
        rating: Number(match[1]),
        source: `the open band unit, ${unit.title}`,
      };
  }
  if (typeof rating === "number")
    return { rating, source: "your Codeforces rating, from its API" };
  return {
    rating: 800,
    source: "no band open and no rating yet, so Div. 2 A's 800",
  };
}
