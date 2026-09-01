// @vitest-environment jsdom
/**
 * The goal tool from the athlete's chair. The regression it guards is the one
 * that started this: a target no human has run used to come back with a
 * training plan, and every ambitious target came back with the same sentence.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import GoalSeek from "../GoalSeek";
import { parseDistance } from "../ProjectionPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const dose = {
  easyHours: 6.4,
  thresholdHours: 1.0,
  intervalHours: 0.6,
  strengthHours: 0,
  runningHours: 8,
  strain: 11.6,
  easyShare: 0.8,
  zones: [
    { zone: "Easy", hours: 6.4, strain: 6.4, marginalVdotPerHour: 1.24 },
    { zone: "Threshold", hours: 1.0, strain: 2.5, marginalVdotPerHour: 1.24 },
    { zone: "Interval", hours: 0.6, strain: 2.7, marginalVdotPerHour: 1.24 },
    { zone: "Strength", hours: 0, strain: 0, marginalVdotPerHour: 1.08 },
  ],
};

const base = {
  distanceMeters: 8046.72,
  targetSeconds: 1200,
  monthsAvailable: 24,
  targetVdot: 89.8,
  startVdot: 37,
  grade: 1.03,
  gradeBand: "past the world record",
  recordEquivalentSeconds: 1315,
  recordHolder: "Jacob Kiplimo",
  ceilingReachable: 64.4,
  prescription: null,
  monthsAtHoursAvailable: null,
  earliestMonths: null,
  probabilityByDate: 0,
  monthsForEvenOdds: null,
  achievableSecondsByDate: 1315,
  steps: [
    {
      label: "Human ceiling",
      expression: "Kiplimo's half marathon 56:42 scored by the same equations",
      value: "VDOT 87.0",
      citationId: "daniels-vdot",
    },
  ],
};

const impossible = {
  ...base,
  verdict: "PastTheWorldRecord",
  headline:
    "No human has run this. 20:00 for 5-mile scores VDOT 89.8; the best performance on record scores 87.0.",
  detail:
    "The target is 3% beyond the record book, so no training answer exists.",
  bindingConstraint: "the world record — VDOT 87.0",
};

const reachable = {
  ...base,
  verdict: "Reachable",
  targetVdot: 44.9,
  grade: 0.52,
  gradeBand: "recreational",
  headline: "Reachable on 4.8 h/week of running — 50% by your date.",
  detail: "3.6 h easy, 0.8 h threshold and 0.5 h intervals.",
  bindingConstraint: "training time — 4.8 h/week, 6.9 strain units",
  prescription: {
    dose,
    hourPrice: 1.24,
    strainPrice: 0,
    rampMonths: 2.4,
    weeklyMiles: 31,
  },
  probabilityByDate: 0.5,
  monthsForEvenOdds: 24,
  earliestMonths: 6,
  monthsAtHoursAvailable: 24,
};

function answerWith(body: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async () =>
        new Response(JSON.stringify(body), {
          headers: { "Content-Type": "application/json" },
        }),
    ),
  );
}

function ask() {
  fireEvent.click(screen.getByRole("button", { name: "Work it out" }));
}

describe("GoalSeek", () => {
  it("names the record book rather than quoting a training plan for it", async () => {
    answerWith(impossible);
    render(<GoalSeek availableHours={7} onGoalSaved={() => {}} />);
    ask();

    await waitFor(() =>
      expect(screen.getByText(/No human has run this/)).toBeTruthy(),
    );
    expect(
      screen.getByText(/Binding constraint: the world record/),
    ).toBeTruthy();

    // The thing that used to appear here — a weekly dose — must not.
    expect(screen.queryByText("The week it needs")).toBeNull();
    expect(screen.queryByText(/h\/week of running/)).toBeNull();
  });

  it("answers a reachable goal with hours by zone and a probability", async () => {
    answerWith(reachable);
    render(<GoalSeek availableHours={7} onGoalSaved={() => {}} />);
    ask();

    await waitFor(() =>
      expect(screen.getByText("The week it needs")).toBeTruthy(),
    );
    expect(screen.getByText("Easy")).toBeTruthy();
    expect(screen.getByText("6.40")).toBeTruthy();
    expect(screen.getByText(/Chance by your date: 50%/)).toBeTruthy();
    expect(screen.getByText(/31 miles a week/)).toBeTruthy();
  });

  it("ships the arithmetic with every answer", async () => {
    answerWith(impossible);
    render(<GoalSeek availableHours={7} onGoalSaved={() => {}} />);
    ask();

    await waitFor(() =>
      expect(screen.getByText("Show the maths")).toBeTruthy(),
    );
    fireEvent.click(screen.getByText("Show the maths"));
    expect(screen.getByText(/scored by the same equations/)).toBeTruthy();
  });

  it("asks the server for the distance and date that were typed", async () => {
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL) =>
        new Response(JSON.stringify({ ...reachable, url: String(input) }), {
          headers: { "Content-Type": "application/json" },
        }),
    );
    vi.stubGlobal("fetch", fetchMock);

    render(<GoalSeek availableHours={7} onGoalSaved={() => {}} />);
    fireEvent.change(screen.getByLabelText("Distance"), {
      target: { value: "10" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Work it out" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const url = String(fetchMock.mock.calls[0][0]);
    expect(url).toContain("distanceMeters=16093.44");
    expect(url).toContain("targetSeconds=2160");
  });

  it("refuses a target time it cannot read instead of sending nonsense", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    render(<GoalSeek availableHours={7} onGoalSaved={() => {}} />);
    fireEvent.change(screen.getByLabelText("Target time"), {
      target: { value: "soonish" },
    });
    ask();

    expect(screen.getByText(/should look like 36:00/)).toBeTruthy();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("parseDistance", () => {
  it("takes a distance in the units it gets typed in", () => {
    expect(parseDistance("5")).toBeCloseTo(8046.72);
    expect(parseDistance("10 km")).toBe(10000);
    expect(parseDistance("10k")).toBe(10000);
    expect(parseDistance("3000 m")).toBe(3000);
    expect(parseDistance("1.5 mi")).toBeCloseTo(2414.016);
  });

  it("rejects what is not a distance", () => {
    expect(parseDistance("")).toBeNull();
    expect(parseDistance("a bit")).toBeNull();
    expect(parseDistance("-3")).toBeNull();
  });
});
