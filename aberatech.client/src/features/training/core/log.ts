/**
 * The solve log, in Mostafa Saad's columns: per solve, the minutes spent
 * reading, thinking, coding, and debugging, the submissions, and whether it
 * was solved alone, with a hint, or after the editorial. Weekly averages
 * show where the time goes, which he names as the way to find a weak skill.
 *
 * Speed drills time a Div. 2 A against 5 minutes and a B against 10, his
 * targets for a "semisenior".
 */
import { addDays, localDate, localDateOf, weekday } from "./dates";
import type { TrainingDocument } from "./progress";

export type Help = "alone" | "hint" | "editorial";

export const helpLabel: Record<Help, string> = {
  alone: "Alone",
  hint: "With a hint",
  editorial: "After the editorial",
};

export type Drill = "A" | "B";

/** His targets: Div. 2 A in 5 minutes, B in 10. */
export const drillTarget: Record<Drill, number> = { A: 5, B: 10 };

export interface Solve {
  id: string;
  /** When it was logged, an ISO date and time. */
  at: string;
  /** A pool code, a judge's id, or the problem's address. */
  problem: string;
  url?: string;
  /** Saad's level, when the problem is from his pool. */
  level?: number;
  read: number;
  think: number;
  code: number;
  debug: number;
  submissions: number;
  help: Help;
  /** A speed drill, and its timed minutes. */
  drill?: Drill;
}

export interface SpeedDrill {
  drill: Drill;
  problem: string;
  started: string;
}

/** Entries kept. The whole document is capped at 256 KB on the server. */
export const logLimit = 1500;

export const totalOf = (solve: Solve): number =>
  solve.read + solve.think + solve.code + solve.debug;

export function addSolve(
  document: TrainingDocument,
  solve: Omit<Solve, "id" | "at">,
  now: Date,
): TrainingDocument {
  const entry: Solve = {
    ...solve,
    id: now.getTime().toString(36),
    at: now.toISOString(),
  };
  const log = [...document.log, entry].slice(-logLimit);
  return { ...document, log };
}

export function removeSolve(
  document: TrainingDocument,
  id: string,
): TrainingDocument {
  return { ...document, log: document.log.filter((solve) => solve.id !== id) };
}

export function startDrill(
  document: TrainingDocument,
  drill: Drill,
  problem: string,
  now: Date,
): TrainingDocument {
  return { ...document, speed: { drill, problem, started: now.toISOString() } };
}

/** Stop the timer and log the drill: its minutes go under coding. */
export function stopDrill(
  document: TrainingDocument,
  now: Date,
  submissions: number,
): TrainingDocument {
  const speed = document.speed;
  if (!speed) return document;
  const minutes = Math.max(
    1,
    Math.round((now.getTime() - Date.parse(speed.started)) / 60_000),
  );
  const { speed: _, ...rest } = addSolve(
    document,
    {
      problem: speed.problem,
      url: speed.problem.startsWith("http") ? speed.problem : undefined,
      read: 0,
      think: 0,
      code: minutes,
      debug: 0,
      submissions,
      help: "alone",
      drill: speed.drill,
    },
    now,
  );
  return rest as TrainingDocument;
}

/** The Monday a local date's week starts on. */
export function weekStart(date: string): string {
  const day = weekday(date);
  return addDays(date, day === 0 ? -6 : 1 - day);
}

export interface WeekAverage {
  week: string;
  solves: number;
  read: number;
  think: number;
  code: number;
  debug: number;
  total: number;
  submissions: number;
  help: Record<Help, number>;
  drills: Record<Drill, { count: number; onTarget: number; best?: number }>;
}

const average = (values: number[]): number =>
  values.length === 0
    ? 0
    : Math.round(
        (values.reduce((sum, value) => sum + value, 0) / values.length) * 10,
      ) / 10;

/** Averages for each of the last `weeks` weeks, newest first, from `today`. */
export function weeklyAverages(
  log: Solve[],
  today: string,
  weeks = 4,
): WeekAverage[] {
  const out: WeekAverage[] = [];
  const first = weekStart(today);
  for (let i = 0; i < weeks; i += 1) {
    const week = addDays(first, -7 * i);
    const end = addDays(week, 7);
    const inWeek = log.filter((solve) => {
      const date = localDateOf(solve.at);
      return date >= week && date < end;
    });
    const solves = inWeek.filter((solve) => !solve.drill);
    const drills = (drill: Drill) => {
      const timed = inWeek
        .filter((solve) => solve.drill === drill)
        .map(totalOf);
      return {
        count: timed.length,
        onTarget: timed.filter((minutes) => minutes <= drillTarget[drill])
          .length,
        ...(timed.length ? { best: Math.min(...timed) } : {}),
      };
    };
    out.push({
      week,
      solves: solves.length,
      read: average(solves.map((solve) => solve.read)),
      think: average(solves.map((solve) => solve.think)),
      code: average(solves.map((solve) => solve.code)),
      debug: average(solves.map((solve) => solve.debug)),
      total: average(solves.map(totalOf)),
      submissions: average(solves.map((solve) => solve.submissions)),
      help: {
        alone: solves.filter((solve) => solve.help === "alone").length,
        hint: solves.filter((solve) => solve.help === "hint").length,
        editorial: solves.filter((solve) => solve.help === "editorial").length,
      },
      drills: { A: drills("A"), B: drills("B") },
    });
  }
  return out;
}

/**
 * Where his checklist says the time is too long: reading over 5 minutes
 * on average, coding over 10, debugging over 10.
 */
export function weakSpots(week: WeekAverage): string[] {
  if (week.solves === 0) return [];
  const out: string[] = [];
  if (week.read > 5)
    out.push(
      `Reading averaged ${week.read} min. His mark is 3 to 5 for a short problem.`,
    );
  if (week.code > 10)
    out.push(`Coding averaged ${week.code} min. His mark is 10.`);
  if (week.debug > 10)
    out.push(`Debugging averaged ${week.debug} min. His mark is 10.`);
  return out;
}

/** This week's checklist answers. */
export function setChecklist(
  document: TrainingDocument,
  week: string,
  index: number,
  value: boolean,
  size: number,
): TrainingDocument {
  const current =
    document.weekly[week] ?? Array.from({ length: size }, () => false);
  const next = current.map((answer, i) => (i === index ? value : answer));
  return { ...document, weekly: { ...document.weekly, [week]: next } };
}

export const todayOf = (now: Date): string => localDate(now);
