import type { PlanNote, PlanWorkout, TrainingPlan } from "./api";

/** One date in a week, with what the plan holds on it. */
export interface PlanDayView {
  date: string;
  weekday: string;
  notes: PlanNote[];
  workouts: PlanWorkout[];
}

/** One week of the plan, as the page shows it. */
export interface PlanWeek {
  week: number;
  block?: string;
  label?: string;
  start?: string;
  end?: string;
  days: PlanDayView[];
  minutes: number;
  tss: number;
  /** Minutes by sport, largest first. Sports with no time are left out. */
  bySport: { sport: string; minutes: number }[];
}

const weekdayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

/** The weekday of a yyyy-mm-dd date, read as a calendar date with no zone. */
export function weekdayOf(date: string): string {
  const [y, m, d] = date.split("-").map(Number);
  return weekdayNames[new Date(Date.UTC(y, m - 1, d)).getUTCDay()];
}

/**
 * The plan's rows grouped by its Week column, then by date. A row with no
 * week or no date is left out: the page has no place to put it.
 */
export function planWeeks(plan: TrainingPlan): PlanWeek[] {
  const weeks = new Map<number, PlanWeek>();
  const week = (n: number) => {
    let found = weeks.get(n);
    if (!found) {
      found = { week: n, days: [], minutes: 0, tss: 0, bySport: [] };
      weeks.set(n, found);
    }
    return found;
  };
  const day = (w: PlanWeek, date: string) => {
    let found = w.days.find((d) => d.date === date);
    if (!found) {
      found = { date, weekday: weekdayOf(date), notes: [], workouts: [] };
      w.days.push(found);
    }
    return found;
  };

  for (const note of plan.notes) {
    const { week: n, date } = note.place;
    if (n === undefined || !date) continue;
    const w = week(n);
    w.block ??= note.place.block;
    w.label ??= note.place.weekLabel;
    day(w, date).notes.push(note);
  }

  for (const workout of plan.workouts) {
    const { week: n, date } = workout.place;
    if (n === undefined || !date) continue;
    const w = week(n);
    w.block ??= workout.place.block;
    w.label ??= workout.place.weekLabel;
    day(w, date).workouts.push(workout);
    w.minutes += workout.minutes ?? 0;
    w.tss += workout.tss ?? 0;
  }

  const all = [...weeks.values()].sort((a, b) => a.week - b.week);
  for (const w of all) {
    w.days.sort((a, b) => a.date.localeCompare(b.date));
    for (const d of w.days)
      d.workouts.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
    w.start = w.days[0]?.date;
    w.end = w.days[w.days.length - 1]?.date;
    const sports = new Map<string, number>();
    for (const d of w.days)
      for (const x of d.workouts)
        if (x.minutes)
          sports.set(
            x.sport ?? "Other",
            (sports.get(x.sport ?? "Other") ?? 0) + x.minutes,
          );
    w.bySport = [...sports.entries()]
      .map(([sport, minutes]) => ({ sport, minutes }))
      .sort((a, b) => b.minutes - a.minutes);
    w.tss = Math.round(w.tss * 10) / 10;
  }
  // A week the plan numbers but leaves empty stays in, so the count and
  // the chips match the plan's own week numbers. It is dated as the week
  // after the one before it.
  const filled: PlanWeek[] = [];
  for (const w of all) {
    const last = filled[filled.length - 1];
    for (let n = last ? last.week + 1 : w.week; n < w.week; n++) {
      const before = filled[filled.length - 1];
      const start = before?.start
        ? addDays(mondayOf(before.start), 7)
        : undefined;
      filled.push({
        week: n,
        days: [],
        minutes: 0,
        tss: 0,
        bySport: [],
        start,
        end: start ? addDays(start, 6) : undefined,
      });
    }
    filled.push(w);
  }
  return filled;
}

/**
 * The week to open: the one whose dates hold today, else the first week
 * when the plan has not started and the last when it is over.
 */
export function currentWeek(
  weeks: PlanWeek[],
  today: string,
): number | undefined {
  if (weeks.length === 0) return undefined;
  for (const w of weeks) {
    if (!w.start) continue;
    const monday = mondayOf(w.start);
    if (today >= monday && today <= addDays(monday, 6)) return w.week;
  }
  const first = weeks[0];
  return first.start && today < first.start
    ? first.week
    : weeks[weeks.length - 1].week;
}

function mondayOf(date: string): string {
  const offset = (weekdayNames.indexOf(weekdayOf(date)) + 6) % 7;
  return addDays(date, -offset);
}

export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
}

/** 75 reads "1 h 15 min", 45 reads "45 min", 0 reads "". */
export function formatMinutes(minutes: number | undefined): string {
  if (!minutes) return "";
  const total = Math.round(minutes);
  const h = Math.floor(total / 60);
  const m = total % 60;
  if (h === 0) return `${m} min`;
  return m === 0 ? `${h} h` : `${h} h ${m} min`;
}

const months = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec",
];

/** 2026-11-16 reads "16 Nov 2026". */
export function formatDate(date: string | undefined, withYear = true): string {
  if (!date) return "";
  const [y, m, d] = date.split("-").map(Number);
  return withYear ? `${d} ${months[m - 1]} ${y}` : `${d} ${months[m - 1]}`;
}

/** Today as yyyy-mm-dd in the viewer's own zone. */
export function todayIso(now = new Date()): string {
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, "0");
  const d = String(now.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}
