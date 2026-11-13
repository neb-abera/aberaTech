import { describe, expect, it } from "vitest";
import type { PlanWorkout } from "../../../training-plan/core/api";
import { measureCompliance } from "../../core/compliance";

const workout = (
  date: string,
  sport: string,
  minutes: number,
  title = "Run (MAF intensity)",
): PlanWorkout => ({
  id: `${date}-${sport}-${title}`,
  title,
  place: { date },
  sport,
  minutes,
  description: "",
  comments: "",
  structure: "",
  changed: false,
});

describe("measureCompliance", () => {
  const plan = [
    workout("2026-11-02", "Run", 30),
    workout("2026-11-04", "Run", 45),
    workout("2026-11-04", "Swim", 30, "Optional Swim"),
    workout("2026-11-05", "Strength", 60, "Pull One"),
    workout("2026-11-07", "Walk", 70, "Ruck Walk"),
    workout("2026-11-08", "Run", 40, "Optional Run"),
    workout("2026-11-13", "Run", 50),
  ];

  it("counts the plan's running and rucking through yesterday, without optional sessions", () => {
    const daily = [
      { date: "2026-11-01", minutes: 60 },
      { date: "2026-11-02", minutes: 30 },
      { date: "2026-11-04", minutes: 40 },
      { date: "2026-11-09", minutes: 20 },
      { date: "2026-11-13", minutes: 50 },
    ];
    const measured = measureCompliance(plan, daily, "2026-11-13");
    expect(measured).toEqual({
      share: 90 / 145,
      loggedMinutes: 90,
      plannedMinutes: 145,
      from: "2026-11-02",
      through: "2026-11-12",
    });
  });

  it("caps at 100% and waits for a planned day to pass", () => {
    expect(
      measureCompliance(
        plan,
        [{ date: "2026-11-02", minutes: 500 }],
        "2026-11-03",
      )?.share,
    ).toBe(1);
    expect(measureCompliance(plan, [], "2026-11-02")).toBeNull();
  });
});
