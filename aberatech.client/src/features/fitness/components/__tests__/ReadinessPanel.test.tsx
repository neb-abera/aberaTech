// @vitest-environment jsdom
/**
 * The readouts behind the goals: rucking, calisthenics, body composition and
 * the scored fitness tests.
 */

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { Readiness } from "../../core/api";
import ReadinessPanel from "../ReadinessPanel";

afterEach(cleanup);

const readiness: Readiness = {
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
});
