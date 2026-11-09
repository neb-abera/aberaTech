import { describe, expect, it } from "vitest";
import {
  addSolve,
  startDrill,
  stopDrill,
  weakSpots,
  weeklyAverages,
  weekStart,
} from "../log";
import { emptyTraining, normalizeTraining } from "../progress";

const at = (iso: string) => new Date(iso);
const solve = (read: number, think: number, code: number, debug: number) => ({
  problem: "CF1-D2-A",
  read,
  think,
  code,
  debug,
  submissions: 2,
  help: "alone" as const,
});

describe("the solve log", () => {
  it("starts the week on Monday", () => {
    expect(weekStart("2026-11-09")).toBe("2026-11-09");
    expect(weekStart("2026-11-15")).toBe("2026-11-09");
    expect(weekStart("2026-11-16")).toBe("2026-11-16");
  });

  it("averages each column over the week's solves", () => {
    let document = addSolve(
      emptyTraining(),
      solve(4, 30, 12, 0),
      at("2026-11-10T12:00:00Z"),
    );
    document = addSolve(
      document,
      { ...solve(6, 50, 8, 20), help: "hint" },
      at("2026-11-11T12:00:00Z"),
    );
    // Last week: not in this week's row.
    document = addSolve(
      document,
      solve(60, 60, 60, 60),
      at("2026-11-04T12:00:00Z"),
    );
    const [week, last] = weeklyAverages(document.log, "2026-11-12");
    expect(week).toMatchObject({
      week: "2026-11-09",
      solves: 2,
      read: 5,
      think: 40,
      code: 10,
      debug: 10,
      total: 65,
      submissions: 2,
      help: { alone: 1, hint: 1, editorial: 0 },
    });
    expect(last.solves).toBe(1);
    expect(weakSpots(week)).toEqual([]);
    expect(weakSpots(last)).toHaveLength(3);
  });

  it("times a speed drill against its target and logs it apart", () => {
    let document = startDrill(
      emptyTraining(),
      "A",
      "CF2000A",
      at("2026-11-10T12:00:00Z"),
    );
    document = stopDrill(document, at("2026-11-10T12:04:20Z"), 1);
    expect(document.speed).toBeUndefined();
    expect(document.log[0]).toMatchObject({
      drill: "A",
      code: 4,
      problem: "CF2000A",
    });
    document = startDrill(document, "B", "CF2000B", at("2026-11-10T13:00:00Z"));
    document = stopDrill(document, at("2026-11-10T13:12:00Z"), 2);
    const [week] = weeklyAverages(document.log, "2026-11-10");
    expect(week.solves).toBe(0);
    expect(week.drills).toEqual({
      A: { count: 1, onTarget: 1, best: 4 },
      B: { count: 1, onTarget: 0, best: 12 },
    });
  });

  it("survives the server's JSON, and drops what is not a solve", () => {
    const document = addSolve(
      emptyTraining(),
      solve(1, 2, 3, 4),
      at("2026-11-10T12:00:00Z"),
    );
    const back = normalizeTraining(
      JSON.parse(
        JSON.stringify({
          ...document,
          log: [...document.log, { problem: "" }, { id: "x", at: "nope" }],
        }),
      ),
    );
    expect(back.log).toEqual(document.log);
  });
});
