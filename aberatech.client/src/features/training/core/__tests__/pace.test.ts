import { describe, expect, it } from "vitest";
import { minutesDone, pace, weekStart } from "../pace";
import {
  emptyTraining,
  noSolves,
  type Solves,
  toggleItem,
  unitStates,
} from "../progress";
import { locate, syllabus } from "../syllabus";

const tick = (id: string, iso: string, document = emptyTraining()) =>
  toggleItem(document, id, new Date(iso));
const minutesOf = (id: string) => locate(id)?.item?.minutes ?? Number.NaN;

describe("pace", () => {
  it("starts the week on Monday", () => {
    expect(weekStart("2026-11-10")).toBe("2026-11-09");
    expect(weekStart("2026-11-09")).toBe("2026-11-09");
    expect(weekStart("2026-11-15")).toBe("2026-11-09");
  });

  it("counts an item's minutes on the day it was done, ticked or judged", () => {
    let document = tick("cp-1-read", "2026-11-10T12:00:00");
    document = tick("cp-2-read", "2026-11-02T12:00:00", document);
    const solves: Solves = {
      codeforces: { solved: { "2263B": "2026-11-11T12:00:00" } },
      atcoder: null,
    };
    expect(minutesDone(document, solves, "2026-11-09", "2026-11-15")).toBe(
      minutesOf("cp-1-read") + minutesOf("cf-2263B"),
    );
  });

  it("compares this week with the plan's self-paced week and projects each phase", () => {
    const date = "2026-11-12";
    // 28 days of work: two readings.
    let document = tick("cp-1-read", "2026-11-10T12:00:00");
    document = tick("cp-2-read", "2026-10-20T12:00:00", document);
    const now = new Date("2026-11-12T12:00:00");
    const states = unitStates(document, noSolves, now);
    const result = pace(date, syllabus.phases[0], document, noSolves, states);

    expect(result.weekFrom).toBe("2026-11-09");
    expect(result.thisWeek).toBe(minutesOf("cp-1-read"));
    expect(result.planWeek).toBe(120);
    const window = minutesOf("cp-1-read") + minutesOf("cp-2-read");
    expect(result.window).toBe(window);
    expect(result.perWeek).toBe(Math.round((window / 28) * 7));

    const first = result.projections[0];
    expect(first.phase.phase).toBe(0);
    const days = Math.ceil((first.remaining / result.perWeek) * 7);
    const expected = new Date(
      Date.parse(`${date}T12:00:00Z`) + days * 86_400_000,
    )
      .toISOString()
      .slice(0, 10);
    expect(first.done).toBe(expected);
    // The next phase's date carries the first phase's work.
    const second = result.projections[1];
    const carried = Math.ceil(
      ((first.remaining + second.remaining) / result.perWeek) * 7,
    );
    expect(second.done).toBe(
      new Date(Date.parse(`${date}T12:00:00Z`) + carried * 86_400_000)
        .toISOString()
        .slice(0, 10),
    );
  });

  it("gives no date with no pace", () => {
    const states = unitStates(emptyTraining(), noSolves, new Date());
    const result = pace(
      "2026-11-12",
      syllabus.phases[0],
      emptyTraining(),
      noSolves,
      states,
    );
    expect(result.perWeek).toBe(0);
    expect(result.projections.every((p) => p.done === null)).toBe(true);
  });
});
