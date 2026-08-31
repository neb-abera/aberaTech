// @vitest-environment jsdom
/**
 * The gate, from the user's chair: an unconfigured deployment explains itself,
 * a stranger gets a sign-in button and no data, and the owner gets the
 * console. The whole point of the feature being fail-closed is testable only
 * from out here.
 */

import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import FitnessPanel from "../FitnessPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    headers: { "Content-Type": "application/json" },
  });
}

const emptySummary = {
  settings: {
    referenceHr: 152,
    ltSecondsPerKm: 340,
    planMinutesPerWeek: 160,
    startVdot: 37,
    vdotMeasuredOn: null,
    currentWeightKg: 78.9,
    birthYear: 1993,
    pastPeakDistanceMeters: 3218.688,
    pastPeakSeconds: 765,
    pastPeakYear: 2019,
    homeAltitudeMeters: 1190,
    female: null,
    availableHoursPerWeek: 7,
  },
  aerobicTrend: [
    { month: "2026-07", medianSecPerKm: 447, runs: 2 },
    { month: "2026-08", medianSecPerKm: 410, runs: 11 },
  ],
  weeklyVolume: [{ weekStart: "2026-08-24", minutes: 85 }],
  strengthTrend: [
    { date: "2026-08-19", exercise: "Bench Press (Barbell)", e1RmKg: 89 },
  ],
  trainingPaces: [
    {
      zone: "E",
      name: "Easy",
      purpose: "aerobic base",
      fastSecPerKm: 373,
      slowSecPerKm: 446,
    },
  ],
  highlights: [
    {
      kind: "aerobic-gain",
      headline: "Aerobic base up 8% month over month",
      evidence: "Median HR-normalized pace 7:27/km -> 6:50/km (2 -> 11 runs).",
      positive: true,
    },
  ],
  measuredDose: {
    easyHours: 3.1,
    thresholdHours: 0.3,
    intervalHours: 0.1,
    strengthHours: 1.2,
    runningHours: 3.5,
    strain: 5.9,
    easyShare: 0.886,
    zones: [
      { zone: "Easy", hours: 3.1, strain: 3.1, marginalVdotPerHour: 1.58 },
      {
        zone: "Threshold",
        hours: 0.3,
        strain: 0.75,
        marginalVdotPerHour: 2.22,
      },
      { zone: "Interval", hours: 0.1, strain: 0.45, marginalVdotPerHour: 2.85 },
      { zone: "Strength", hours: 1.2, strain: 1.8, marginalVdotPerHour: 0.48 },
    ],
  },
  measuredDoseSteps: [
    {
      label: "Your current week, from the log",
      expression: "18 sessions over 8 weeks",
      value: "3.1 h easy, 0.3 h threshold",
      citationId: "daniels-vdot",
    },
  ],
  deficiencySpread: 0.147,
  activityCount: 25,
};

describe("FitnessPanel", () => {
  it("explains an unconfigured deployment instead of erroring", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          json({ configured: false, signedIn: false, hevyApi: false }),
        ),
    );

    render(<FitnessPanel />);

    await screen.findByText(/not set up on this deployment/i);
  });

  it("offers sign-in and nothing else to the signed-out", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          json({ configured: true, signedIn: false, hevyApi: false }),
        ),
    );

    render(<FitnessPanel />);

    const button = await screen.findByRole("link", {
      name: /sign in with google/i,
    });
    expect(button.getAttribute("href")).toContain("returnUrl=/fitness");
    // No data was fetched for a stranger.
    expect(vi.mocked(fetch)).toHaveBeenCalledTimes(1);
  });

  it("shows the owner highlights and the deficiency verdict", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: RequestInfo | URL) => {
        const path = String(url);
        if (path.includes("/api/fitness/me")) {
          return Promise.resolve(
            json({ configured: true, signedIn: true, hevyApi: false }),
          );
        }
        if (path.includes("/api/fitness/summary")) {
          return Promise.resolve(json(emptySummary));
        }
        return Promise.reject(new Error(`unexpected ${path}`));
      }),
    );

    render(<FitnessPanel />);

    await screen.findByText(/aerobic base up 8%/i);
    await screen.findByText(/training paces \(daniels\)/i);
    await screen.findByText(/7:26\/km – 6:13\/km/);
    await screen.findByText(/over the 10% deficiency line/i);

    await waitFor(() => {
      // The console's two prediction surfaces: the what-if workbench and the
      // training plan behind it.
      expect(screen.getByRole("tab", { name: /solve/i })).toBeDefined();
      expect(screen.getByRole("tab", { name: /plan/i })).toBeDefined();
    });
  });
});
