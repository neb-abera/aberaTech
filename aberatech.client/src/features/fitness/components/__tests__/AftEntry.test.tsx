// @vitest-environment jsdom
/**
 * Recording a fitness test and naming a ruck's load, from the user's chair.
 * Both are numbers the athlete types that no device knows, and both go to
 * the server in the unit the log keeps (kilograms) whatever unit they were
 * typed in.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { SettingsDto } from "../../core/api";
import DataPanel from "../DataPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const settings: SettingsDto = {
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
  selectionDate: null,
};

function json(body: unknown, ok = true): Response {
  return new Response(JSON.stringify(body), {
    status: ok ? 200 : 400,
    headers: { "Content-Type": "application/json" },
  });
}

function stubFetch(activities: unknown[]) {
  const calls: Request[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo, init?: RequestInit) => {
      const request = new Request(
        typeof input === "string" ? `http://localhost${input}` : input,
        init,
      );
      calls.push(request);
      if (request.url.endsWith("/api/fitness/activities")) {
        return Promise.resolve(
          json({ activities, total: activities.length, limit: 50 }),
        );
      }
      if (request.url.endsWith("/api/fitness/aft")) {
        return Promise.resolve(
          json({
            id: "a1",
            date: "2026-09-07",
            total: 402,
            ageBand: "32-36",
            meetsCombatStandard: true,
            events: [],
            steps: [],
          }),
        );
      }
      return Promise.resolve(new Response(null, { status: 204 }));
    }),
  );
  return calls;
}

describe("AftEntry", () => {
  it("posts the five results in the log's units and reports the score", async () => {
    const calls = stubFetch([]);
    const onDataChanged = vi.fn();
    render(
      <DataPanel
        hevyApi={false}
        settings={settings}
        onDataChanged={onDataChanged}
      />,
    );

    fireEvent.change(screen.getByLabelText("Deadlift 3RM (lb)"), {
      target: { value: "300" },
    });
    fireEvent.change(screen.getByLabelText("Hand-release push-ups"), {
      target: { value: "45" },
    });
    fireEvent.change(screen.getByLabelText("Sprint-drag-carry (m:ss)"), {
      target: { value: "2:00" },
    });
    fireEvent.change(screen.getByLabelText("Plank (m:ss)"), {
      target: { value: "3:00" },
    });
    fireEvent.change(screen.getByLabelText("Two-mile run (m:ss)"), {
      target: { value: "15:30" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() =>
      expect(screen.getByText(/Scored 402 on the 32-36 band/)).toBeTruthy(),
    );

    const post = calls.find((c) => c.url.endsWith("/api/fitness/aft"));
    if (!post) throw new Error("no POST to /api/fitness/aft");
    expect(post.method).toBe("POST");
    const body = JSON.parse(await post.text()) as Record<string, number>;
    expect(body.deadliftKg).toBeCloseTo(136.08, 1);
    expect(body.handReleasePushUps).toBe(45);
    expect(body.sprintDragCarrySeconds).toBe(120);
    expect(body.plankSeconds).toBe(180);
    expect(body.twoMileSeconds).toBe(930);
    expect(onDataChanged).toHaveBeenCalled();
  });

  it("refuses a test whose times are not times", async () => {
    stubFetch([]);
    render(
      <DataPanel
        hevyApi={false}
        settings={settings}
        onDataChanged={() => {}}
      />,
    );

    fireEvent.change(screen.getByLabelText("Deadlift 3RM (lb)"), {
      target: { value: "300" },
    });
    fireEvent.change(screen.getByLabelText("Hand-release push-ups"), {
      target: { value: "45" },
    });
    fireEvent.change(screen.getByLabelText("Two-mile run (m:ss)"), {
      target: { value: "fifteen" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() =>
      expect(screen.getByText(/are times like 2:10/)).toBeTruthy(),
    );
  });
});

describe("LoadField", () => {
  const ruck = {
    id: "r1",
    source: "garmin-export",
    startedAt: "2026-08-02T08:00:00Z",
    sport: "ruck",
    name: "Morning Ruck",
    distanceMeters: 12874,
    durationSeconds: 7200,
    averageHr: 142,
    loadKg: null,
  };

  it("sends a load typed in pounds as kilograms", async () => {
    const calls = stubFetch([ruck]);
    const onDataChanged = vi.fn();
    render(
      <DataPanel
        hevyApi={false}
        settings={settings}
        onDataChanged={onDataChanged}
      />,
    );

    const field = await screen.findByLabelText(
      "Load for Morning Ruck of 2026-08-02",
    );
    fireEvent.change(field, { target: { value: "45" } });
    fireEvent.blur(field);

    await waitFor(() =>
      expect(
        calls.some((c) => c.url.endsWith("/api/fitness/activities/r1/load")),
      ).toBe(true),
    );
    const put = calls.find((c) => c.url.endsWith("/activities/r1/load"));
    if (!put) throw new Error("no PUT to /activities/r1/load");
    expect(put.method).toBe("PUT");
    const body = JSON.parse(await put.text()) as { loadKg: number };
    expect(body.loadKg).toBeCloseTo(20.41, 1);
    expect(onDataChanged).toHaveBeenCalled();
  });

  it("switches the unit and converts what was typed", async () => {
    stubFetch([ruck]);
    render(
      <DataPanel
        hevyApi={false}
        settings={settings}
        onDataChanged={() => {}}
      />,
    );

    const field = (await screen.findByLabelText(
      "Load for Morning Ruck of 2026-08-02",
    )) as HTMLInputElement;
    fireEvent.change(field, { target: { value: "44" } });
    fireEvent.click(screen.getByRole("button", { name: "Switch load unit" }));

    expect(field.value).toBe("20");
  });
});
