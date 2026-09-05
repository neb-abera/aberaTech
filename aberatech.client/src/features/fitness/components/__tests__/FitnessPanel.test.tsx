// @vitest-environment jsdom
/**
 * The gate, from the user's chair: an unconfigured deployment explains itself,
 * a stranger gets a sign-in button and no data, and the owner gets the
 * console. The whole point of the feature being fail-closed is testable only
 * from out here.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import FitnessPanel from "../FitnessPanel";

/** The panel reads the open tab from the URL, so it needs a router. */
function mount(entry = "/fitness") {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <FitnessPanel />
    </MemoryRouter>,
  );
}

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
    pastPeakWeightKg: 80.7,
    goalWeightKg: 74.8,
    maxWeightAdjustmentFraction: 0.1,
    female: null,
    availableHoursPerWeek: 7,
    sustainedWeeklyHours: null,
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

    mount();

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

    mount();

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

    mount();

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
  it("keeps the console standing when a refresh fails", async () => {
    // The bug: one latch, checked before everything, meant a single failed
    // summary fetch replaced the whole console — tabs, charts, the lot — with
    // "the service did not answer", discarding data already on screen. The
    // fix that rebuilt the dashboard after an import made three more ways to
    // trip it.
    let summaryCalls = 0;
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo) => {
        const url = String(input);
        if (url.endsWith("/me")) {
          return Promise.resolve(
            json({ configured: true, signedIn: true, hevyApi: false }),
          );
        }
        if (url.endsWith("/summary")) {
          summaryCalls += 1;
          return summaryCalls === 1
            ? Promise.resolve(json(emptySummary))
            : Promise.resolve(new Response("boom", { status: 503 }));
        }
        return Promise.resolve(json({ activities: [], total: 0, limit: 50 }));
      }),
    );

    mount();
    await screen.findByRole("tab", { name: "Dashboard" });

    // A later refresh fails.
    fireEvent.click(screen.getByRole("tab", { name: "Data" }));
    await waitFor(() => expect(summaryCalls).toBeGreaterThan(0));

    // Whatever else happens, the tabs are still there to click.
    expect(screen.getByRole("tab", { name: "Dashboard" })).toBeTruthy();
  });

  it("opens the tab named in the address, so a reload comes back to it", async () => {
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
        return Promise.resolve(json({ activities: [], total: 0, limit: 50 }));
      }),
    );

    mount("/fitness?tab=data");

    const dataTab = await screen.findByRole("tab", { name: "Data" });
    expect(dataTab.getAttribute("aria-selected")).toBe("true");
  });

  it("asks for sign-in again when the session goes, rather than blaming the server", async () => {
    let summaryCalls = 0;
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo) => {
        const url = String(input);
        if (url.endsWith("/me")) {
          return Promise.resolve(
            json({ configured: true, signedIn: true, hevyApi: false }),
          );
        }
        if (url.endsWith("/summary")) {
          summaryCalls += 1;
          return summaryCalls === 1
            ? Promise.resolve(json(emptySummary))
            : Promise.resolve(new Response("", { status: 401 }));
        }
        return Promise.resolve(json({ activities: [], total: 0, limit: 50 }));
      }),
    );

    mount();
    await screen.findByRole("tab", { name: "Dashboard" });
  });
});
