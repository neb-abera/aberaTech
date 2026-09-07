// @vitest-environment jsdom
/**
 * The gates, from the athlete's chair: each published standard with its
 * date, what the log says against it, and whether that number was measured
 * or modelled — because a gate scored from an estimate is a different thing
 * from one scored from a stopwatch, and the page has to say which.
 */

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { Readiness } from "../../core/api";
import ReadinessPanel from "../ReadinessPanel";

afterEach(cleanup);

const readiness: Readiness = {
  selectionDate: "2028-04-01",
  gates: [
    {
      id: "sfas-day-one",
      name: "SFAS day-one minimums",
      purpose:
        "Under any of these on the first morning and the course is over.",
      weeksBeforeSelection: 52,
      dueOn: "2027-04-02",
      status: "Fail",
      passed: 1,
      known: 2,
      requirements: [
        {
          metric: "run-2mi",
          label: "Two-mile run",
          comparison: "AtMost",
          target: 912,
          unit: "s",
          citationId: "sfas-day-one",
          status: "Fail",
          current: {
            metric: "run-2mi",
            value: 970,
            basis: "Modeled",
            evidence: "modelled from your VDOT 37.0 anchor",
            on: null,
          },
          gap: "short by 0:58",
        },
        {
          metric: "pull-ups",
          label: "Pull-ups",
          comparison: "AtLeast",
          target: 6,
          unit: "reps",
          citationId: "sfas-day-one",
          status: "Pass",
          current: {
            metric: "pull-ups",
            value: 11,
            basis: "Measured",
            evidence: "best set in the strength log, 2026-08-20",
            on: "2026-08-20",
          },
          gap: "clear by 5 reps",
        },
        {
          metric: "hand-release-push-ups",
          label: "Hand-release push-ups",
          comparison: "AtLeast",
          target: 28,
          unit: "reps",
          citationId: "sfas-day-one",
          status: "Unknown",
          current: null,
          gap: "nothing in the log scores this yet",
        },
      ],
      untracked: [],
    },
  ],
  ruck: {
    referenceLoadKg: 20.41,
    ruckEfficiency: 0.67,
    trend: [],
    marches: [
      {
        date: "2026-08-02",
        distanceMeters: 12874.752,
        seconds: 7200,
        loadKg: 15.9,
        averageHr: 142,
        impliedVdot: 39.4,
      },
    ],
    predictedTwelveMileAt45Seconds: 11254,
    predictedTwelveMileAt35Seconds: 10500,
    rucksWithoutLoad: 2,
    steps: [],
  },
  calisthenics: {
    latest: [
      { date: "2026-08-20", metric: "pull-ups", value: 11 },
      { date: "2026-08-19", metric: "plank", value: 150 },
    ],
    history: [],
  },
  body: {
    points: [],
    latestBodyFatPercent: 18.2,
    latestLeanMassKg: 70.1,
    cohortRateByBodyFat: 0.37,
    cohortRateByLeanMass: 0.53,
  },
  aftResults: [
    {
      id: "a1",
      date: "2026-04-05",
      deadliftKg: 136,
      handReleasePushUps: 45,
      sprintDragCarrySeconds: 120,
      plankSeconds: 180,
      twoMileSeconds: 930,
      total: 428,
      lowestEvent: 72,
      meetsCombatStandard: true,
      ageBand: "32-36",
      ageAssumed: false,
      events: [
        { event: "Deadlift", name: "3-rep max deadlift", raw: 300, points: 91 },
        { event: "TwoMileRun", name: "Two-mile run", raw: 930, points: 89 },
      ],
      steps: [],
    },
  ],
};

describe("ReadinessPanel", () => {
  it("scores each gate line and says whether the number was measured or modelled", () => {
    render(<ReadinessPanel readiness={readiness} />);

    expect(screen.getByText("SFAS day-one minimums")).toBeTruthy();
    expect(screen.getByText("due 2027-04-02")).toBeTruthy();
    expect(screen.getByText("1 of 3 clear")).toBeTruthy();

    // The miss, with its gap and the fact that it is an estimate.
    expect(screen.getByText("short by 0:58")).toBeTruthy();
    expect(
      screen.getByText(/modelled — modelled from your VDOT 37.0/),
    ).toBeTruthy();

    // The pass, from a real set.
    expect(screen.getByText("clear by 5 reps")).toBeTruthy();
    expect(
      screen.getByText(/measured — best set in the strength log/),
    ).toBeTruthy();

    // The line nothing scores yet is said out loud, not dropped.
    expect(screen.getByText("nothing in the log scores this yet")).toBeTruthy();
    expect(screen.getByText("not in the log")).toBeTruthy();
  });

  it("shows the ruck estimate in both units and flags rucks without a load", () => {
    render(<ReadinessPanel readiness={readiness} />);

    expect(screen.getByText("3:07:34")).toBeTruthy();
    expect(screen.getByText(/2 rucks have no load recorded/)).toBeTruthy();
    // 15.9 kg is 35 lb; both are shown.
    expect(screen.getByText("35 lb (15.9 kg)")).toBeTruthy();
  });

  it("shows the scored fitness test and the body-composition cohort rates", () => {
    render(<ReadinessPanel readiness={readiness} />);

    expect(screen.getByText("2026-04-05: 428 points")).toBeTruthy();
    expect(screen.getByText("combat standard met")).toBeTruthy();
    expect(screen.getByText(/cohort selection rate 37%/)).toBeTruthy();
    expect(screen.getByText(/155 lb \(70.1 kg\)/)).toBeTruthy();
  });

  it("asks for a selection date when none is set", () => {
    render(
      <ReadinessPanel readiness={{ ...readiness, selectionDate: null }} />,
    );

    expect(screen.getByText(/No selection date set/)).toBeTruthy();
  });
});
