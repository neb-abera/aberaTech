// @vitest-environment jsdom
/**
 * The gates as a forecast, from the athlete's chair: a chance per line by its
 * due date, the earliest selection date the week supports, and a slider that
 * asks the question again at a different week.
 */

import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Outlook } from "../../core/api";
import DecisionPanel from "../DecisionPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    headers: { "Content-Type": "application/json" },
  });
}

const outlook: Outlook = {
  selectionDate: "2028-04-01",
  weeklyHours: 7,
  measuredWeeklyHours: 0.9,
  plannedWeeklyHours: 7,
  hoursBasis: "the hours a week the profile says you can train (7.0 h)",
  compliance: 1,
  inputs: [
    "Anchor: VDOT 35.4, from the 2026-04-03 time trial in the profile.",
    "Running hours: 7.0 h a week, the hours a week the profile says you can train (7.0 h). The log's last eight weeks average 0.9 h.",
  ],
  startVdot: 35.4,
  gates: [
    {
      id: "sfas-day-one",
      name: "SFAS day-one minimums",
      weeksBeforeSelection: 52,
      dueOn: "2027-04-03",
      monthsAway: 6.9,
      probability: 0.62,
      forecast: 2,
      total: 3,
      readyInMonths: 4.5,
      lines: [
        {
          metric: "run-2mi",
          label: "Two-mile run",
          probability: 0.97,
          method: "trajectory",
          evidence: "Needs VDOT 33.1; the trajectory from 35.4 reaches 38.0.",
          projected: 950,
          readyInMonths: 0,
          hoursToReach: 0,
          unit: "s",
          comparison: "AtMost",
          target: 1080,
        },
        {
          metric: "ruck-12mi-45lb",
          label: "Twelve-mile ruck at 45 lb",
          probability: 0.64,
          method: "trajectory",
          evidence:
            "Needs VDOT 37.9; 6.5 h/week would put the central projection there.",
          projected: 10950,
          readyInMonths: 4.5,
          hoursToReach: 6.5,
          unit: "s",
          comparison: "AtMost",
          target: 10800,
        },
        {
          metric: "pull-ups",
          label: "Pull-ups",
          probability: null,
          method: "held",
          evidence:
            "Clear today, held rather than forecast: 3 dated readings over 42 days make a trend; the log has 2.",
          projected: 11,
          readyInMonths: null,
          hoursToReach: null,
          unit: "reps",
          comparison: "AtLeast",
          target: 6,
        },
      ],
    },
  ],
  earliestSelectionDate: "2028-01-22",
  bindingGate: "sfas-day-one",
  assumptions: ["A gate's chance is the product of its lines'."],
};

describe("DecisionPanel", () => {
  it("shows each line's chance, the gate's product, and the earliest selection date", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(json(outlook));

    render(<DecisionPanel selectionDate="2028-04-01" />);

    await screen.findByText("SFAS day-one minimums");
    expect(screen.getByText("62%")).toBeTruthy();
    expect(screen.getByText("97%")).toBeTruthy();
    expect(screen.getByText("64%")).toBeTruthy();
    expect(screen.getByText("not forecast")).toBeTruthy();
    expect(screen.getByText("2 of 3 lines forecast")).toBeTruthy();
    expect(screen.getByText("6.5 h")).toBeTruthy();
    expect(screen.getByText("2028-01-22")).toBeTruthy();
    expect(screen.getByText(/Your 2028-04-01 date holds/)).toBeTruthy();

    // The first ask lets the server pick the week to start from.
    const first = String(fetchMock.mock.calls[0][0]);
    expect(first).toContain("/api/fitness/readiness/outlook?");
    expect(first).not.toContain("weeklyHours");
  });

  it("starts the slider from the profile's week and says so, with the log beside it", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(json(outlook));

    render(<DecisionPanel selectionDate="2028-04-01" />);

    await screen.findByText("SFAS day-one minimums");
    // Seeding the slider from the answer is not a slider move: one request,
    // and the basis stays the profile's, not "set on the slider".
    await waitFor(() =>
      expect(
        screen
          .getByRole("slider", { name: "Running hours a week" })
          .getAttribute("aria-valuenow"),
      ).toBe("7"),
    );
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(screen.getByText("Running hours a week: 7.0 h")).toBeTruthy();
    expect(
      screen.getByText(
        /Starts from the hours a week the profile says you can train \(7\.0 h\)\. The log's last eight weeks average 0\.9 h\./,
      ),
    ).toBeTruthy();
    expect(
      screen
        .getByRole("slider", { name: "Running hours a week" })
        .getAttribute("aria-valuenow"),
    ).toBe("7");
    expect(
      screen.getByText("Multiplies the planned hours before the forecast"),
    ).toBeTruthy();
  });

  it("lists every input the forecast was computed from, with its source", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(json(outlook));

    render(<DecisionPanel selectionDate="2028-04-01" />);

    await screen.findByText("SFAS day-one minimums");
    const inputs = screen.getByRole("list", { name: "Forecast inputs" });
    expect(inputs.textContent).toContain(
      "Anchor: VDOT 35.4, from the 2026-04-03 time trial",
    );
    expect(inputs.textContent).toContain("Running hours: 7.0 h a week");
    expect(
      screen.getByRole("list", { name: "Forecast method" }).textContent,
    ).toContain("product of its lines");
  });

  it("says when the named date is earlier than the week supports", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      json({ ...outlook, earliestSelectionDate: "2028-09-01" }),
    );

    render(<DecisionPanel selectionDate="2028-04-01" />);

    expect(
      await screen.findByText(/Your 2028-04-01 date is earlier than that/),
    ).toBeTruthy();
  });

  it("keeps standing when the forecast does not answer", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("boom"));

    render(<DecisionPanel selectionDate={null} />);

    await waitFor(() =>
      expect(screen.getByText(/did not answer/)).toBeTruthy(),
    );
  });
});
