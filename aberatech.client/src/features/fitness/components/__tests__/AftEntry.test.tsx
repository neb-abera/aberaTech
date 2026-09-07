// @vitest-environment jsdom
/**
 * Recording a fitness test and naming a ruck's load, from the user's chair.
 * Both are numbers the athlete types that no device knows, and both go to
 * the server in the unit the log keeps (kilograms) whatever unit they were
 * typed in.
 *
 * Each card is rendered on its own. Mounting the whole data panel to type
 * into one of its cards paid for the profile card's dozen fields and its
 * Select on every test — 0.7s a test alone, and past the 15s budget under
 * coverage with the server build running alongside.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import AftEntry from "../AftEntry";
import LoadField from "../LoadField";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function json(body: unknown, ok = true): Response {
  return new Response(JSON.stringify(body), {
    status: ok ? 200 : 400,
    headers: { "Content-Type": "application/json" },
  });
}

/** Scores any test it is sent, and accepts anything else with a 204. */
function stubFetch() {
  const calls: Request[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo, init?: RequestInit) => {
      const request = new Request(
        typeof input === "string" ? `http://localhost${input}` : input,
        init,
      );
      calls.push(request);
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

// Same rehearsal as DataPanel.test: the worker's first MUI mount, first label
// query and first click each pay a one-time emotion/jsdom cost that under
// coverage instrumentation blew the first test's budget. The empty save
// renders the Alert too, so its styles are compiled here as well.
beforeAll(() => {
  stubFetch();
  render(<AftEntry onSaved={() => {}} />);
  fireEvent.click(screen.getByRole("button", { name: "Save test" }));
  screen.getByText(/Deadlift is pounds/);
  cleanup();
  vi.restoreAllMocks();
}, 30_000);

describe("AftEntry", () => {
  it("posts the five results in the log's units and reports the score", async () => {
    const calls = stubFetch();
    const onSaved = vi.fn();
    render(<AftEntry onSaved={onSaved} />);

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

    expect(
      await screen.findByText(/Scored 402 on the 32-36 band/),
    ).toBeTruthy();

    const post = calls.find((c) => c.url.endsWith("/api/fitness/aft"));
    if (!post) throw new Error("no POST to /api/fitness/aft");
    expect(post.method).toBe("POST");
    const body = JSON.parse(await post.text()) as Record<string, number>;
    expect(body.deadliftKg).toBeCloseTo(136.08, 1);
    expect(body.handReleasePushUps).toBe(45);
    expect(body.sprintDragCarrySeconds).toBe(120);
    expect(body.plankSeconds).toBe(180);
    expect(body.twoMileSeconds).toBe(930);
    expect(onSaved).toHaveBeenCalled();
  });

  it("refuses a test whose times are not times", () => {
    const calls = stubFetch();
    render(<AftEntry onSaved={() => {}} />);

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

    // Refused on the page, before anything is sent.
    expect(screen.getByText(/are times like 2:10/)).toBeTruthy();
    expect(calls).toHaveLength(0);
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
    const calls = stubFetch();
    const onSaved = vi.fn();
    const onError = vi.fn();
    render(<LoadField activity={ruck} onSaved={onSaved} onError={onError} />);

    const field = screen.getByLabelText("Load for Morning Ruck of 2026-08-02");
    fireEvent.change(field, { target: { value: "45" } });
    fireEvent.blur(field);

    // onSaved fires once the server has answered, so by then the request is
    // fully on record; waiting on the call itself was a race with it.
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    const put = calls.find((c) => c.url.endsWith("/activities/r1/load"));
    if (!put) throw new Error("no PUT to /activities/r1/load");
    expect(put.method).toBe("PUT");
    const body = JSON.parse(await put.text()) as { loadKg: number };
    expect(body.loadKg).toBeCloseTo(20.41, 1);
    expect(onError).not.toHaveBeenCalled();
  });

  it("switches the unit and converts what was typed", () => {
    stubFetch();
    render(<LoadField activity={ruck} onSaved={() => {}} onError={() => {}} />);

    const field = screen.getByLabelText(
      "Load for Morning Ruck of 2026-08-02",
    ) as HTMLInputElement;
    fireEvent.change(field, { target: { value: "44" } });
    fireEvent.click(screen.getByRole("button", { name: "Switch load unit" }));

    expect(field.value).toBe("20");
  });
});
