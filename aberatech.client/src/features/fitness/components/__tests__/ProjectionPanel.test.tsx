// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Summary } from "../../core/api";
import ProjectionPanel from "../ProjectionPanel";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const summary = {
  settings: {
    startVdot: 41,
    vdotMeasuredOn: null,
    currentWeightKg: null,
    goalWeightKg: null,
    pastPeakWeightKg: null,
    maxWeightAdjustmentFraction: 0.1,
  },
  measuredDose: {
    runningHours: 1.5,
    easyHours: 1.5,
    thresholdHours: 0,
    intervalHours: 0,
    strengthHours: 1.2,
  },
  measuredDoseSteps: [],
} as unknown as Summary;

/** The compliance each prediction request asked for, newest last. */
function stubPredictions(plan?: unknown): () => string | null {
  const urls: string[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/training-plan")) {
        return plan === undefined
          ? new Promise(() => undefined)
          : Promise.resolve(
              new Response(JSON.stringify(plan), {
                status: 200,
                headers: { "content-type": "application/json" },
              }),
            );
      }
      urls.push(url);
      return new Promise(() => undefined);
    }),
  );
  return () => {
    const last = urls[urls.length - 1];
    return last
      ? new URL(last, "https://abera.tech").searchParams.get("compliance")
      : null;
  };
}

describe("ProjectionPanel compliance", () => {
  it("applies no compliance to a week stated as hours", async () => {
    const compliance = stubPredictions();
    render(<ProjectionPanel summary={summary} onGoalsChanged={() => {}} />);

    expect(screen.queryByText(/Compliance/)).toBeNull();
    await waitFor(() => expect(compliance()).toBe("1"));
  });

  it("offers compliance for the Evoke plan, from 100%, and drops it on an edit", async () => {
    const compliance = stubPredictions();
    render(<ProjectionPanel summary={summary} onGoalsChanged={() => {}} />);

    fireEvent.click(screen.getByRole("button", { name: /Evoke plan/ }));
    expect(screen.getByText(/Compliance/).textContent).toContain("100%");
    await waitFor(() => expect(compliance()).toBe("1"));

    const slider = screen.getByRole("slider", { name: "Compliance percent" });
    fireEvent.keyDown(slider, { key: "ArrowLeft" });
    expect(screen.getByText(/Compliance/).textContent).toContain("99%");
    await waitFor(() => expect(compliance()).toBe("0.99"));

    fireEvent.keyDown(
      screen.getByRole("slider", { name: "Weekly running hours" }),
      { key: "ArrowRight" },
    );
    expect(screen.queryByText(/Compliance/)).toBeNull();
    await waitFor(() => expect(compliance()).toBe("1"));
  });

  it("opens the Evoke plan on the compliance the log measures", async () => {
    const yesterday = new Date(Date.now() - 86_400_000).toLocaleDateString(
      "en-CA",
    );
    const compliance = stubPredictions({
      configured: true,
      plan: {
        workouts: [
          {
            id: "w1",
            title: "Run (MAF intensity)",
            place: { date: yesterday },
            sport: "Run",
            minutes: 60,
            description: "",
            comments: "",
            structure: "",
            changed: false,
          },
        ],
      },
    });
    render(
      <ProjectionPanel
        summary={{
          ...summary,
          dailyVolume: [{ date: yesterday, minutes: 45 }],
        }}
        onGoalsChanged={() => {}}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /Evoke plan/ }));

    expect(
      await screen.findByText(/your log holds 45 of the plan/),
    ).toBeTruthy();
    expect(screen.getByText(/Compliance/).textContent).toContain("75%");
    await waitFor(() => expect(compliance()).toBe("0.75"));
  });
});
