import { describe, expect, it } from "vitest";
import type { PlanNote, PlanWorkout, TrainingPlan } from "../api";
import {
  addDays,
  currentWeek,
  formatDate,
  formatMinutes,
  planWeeks,
  weekdayOf,
} from "../weeks";

const workout = (
  title: string,
  date: string,
  week: number,
  extra: Partial<PlanWorkout> = {},
): PlanWorkout => ({
  id: title,
  title,
  place: { date, week },
  description: "",
  comments: "",
  structure: "",
  changed: false,
  ...extra,
});

const note = (
  title: string,
  date: string,
  week: number,
  extra: Partial<PlanNote> = {},
): PlanNote => ({
  id: title,
  title,
  place: { date, week },
  description: "",
  files: [],
  changed: false,
  ...extra,
});

const plan = (
  workouts: PlanWorkout[],
  notes: PlanNote[] = [],
): TrainingPlan => ({
  id: "p",
  title: "Plan",
  sections: [],
  files: [],
  workouts,
  notes,
});

describe("planWeeks", () => {
  it("groups rows by week, then by date, with workouts in their order on the day", () => {
    const weeks = planWeeks(
      plan(
        [
          workout("Run", "2026-11-02", 1, {
            order: 1,
            sport: "Run",
            minutes: 45,
            tss: 33,
          }),
          workout("Warmup", "2026-11-02", 1, {
            order: 0,
            sport: "Other",
            minutes: 5,
          }),
          workout("Lift", "2026-11-04", 1, {
            sport: "Strength",
            minutes: 60,
            tss: 20.25,
          }),
          workout("Test", "2026-11-09", 2, { minutes: 75 }),
        ],
        [
          note("Intro", "2026-11-02", 1, {
            place: {
              date: "2026-11-02",
              week: 1,
              block: "Block 1",
              weekLabel: "Block 1 Week 1",
            },
          }),
        ],
      ),
    );

    expect(weeks.map((w) => w.week)).toEqual([1, 2]);
    const [first, second] = weeks;
    expect(first.block).toBe("Block 1");
    expect(first.label).toBe("Block 1 Week 1");
    expect(first.start).toBe("2026-11-02");
    expect(first.end).toBe("2026-11-04");
    expect(first.days.map((d) => d.date)).toEqual(["2026-11-02", "2026-11-04"]);
    expect(first.days[0].weekday).toBe("Mon");
    expect(first.days[0].notes.map((n) => n.title)).toEqual(["Intro"]);
    expect(first.days[0].workouts.map((w) => w.title)).toEqual([
      "Warmup",
      "Run",
    ]);
    expect(first.minutes).toBe(110);
    expect(first.tss).toBe(53.3);
    expect(first.bySport).toEqual([
      { sport: "Strength", minutes: 60 },
      { sport: "Run", minutes: 45 },
      { sport: "Other", minutes: 5 },
    ]);
    // A workout with no sport counts as Other.
    expect(second.bySport).toEqual([{ sport: "Other", minutes: 75 }]);
  });

  it("leaves out a row with no week or no date", () => {
    const weeks = planWeeks(
      plan([
        workout("No week", "2026-11-02", 1, { place: { date: "2026-11-02" } }),
        workout("No date", "", 1, { place: { week: 1 } }),
      ]),
    );
    expect(weeks).toEqual([]);
  });
});

describe("currentWeek", () => {
  const weeks = planWeeks(
    plan([
      workout("a", "2026-11-03", 1),
      workout("b", "2026-11-09", 2),
      workout("c", "2026-11-22", 3),
    ]),
  );

  it("opens the week whose Monday to Sunday holds today", () => {
    expect(currentWeek(weeks, "2026-11-02")).toBe(1);
    expect(currentWeek(weeks, "2026-11-08")).toBe(1);
    expect(currentWeek(weeks, "2026-11-15")).toBe(2);
    // Week 3's first row is a Sunday: its Monday is 16 Nov.
    expect(currentWeek(weeks, "2026-11-16")).toBe(3);
  });

  it("opens the first week before the plan and the last after it", () => {
    expect(currentWeek(weeks, "2026-10-01")).toBe(1);
    expect(currentWeek(weeks, "2027-01-01")).toBe(3);
    expect(currentWeek([], "2026-11-02")).toBeUndefined();
  });
});

describe("formats", () => {
  it("reads minutes, dates and weekdays the way the page shows them", () => {
    expect(formatMinutes(75)).toBe("1 h 15 min");
    expect(formatMinutes(60)).toBe("1 h");
    expect(formatMinutes(45)).toBe("45 min");
    expect(formatMinutes(0)).toBe("");
    expect(formatMinutes(undefined)).toBe("");
    expect(formatDate("2026-11-16")).toBe("16 Nov 2026");
    expect(formatDate("2026-11-16", false)).toBe("16 Nov");
    expect(formatDate(undefined)).toBe("");
    expect(weekdayOf("2027-08-15")).toBe("Sun");
    expect(addDays("2026-12-30", 3)).toBe("2027-01-02");
  });
});
