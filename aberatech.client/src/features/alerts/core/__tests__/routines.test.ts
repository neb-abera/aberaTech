/**
 * Routine alarms from the page's side: the day summaries, the time on a
 * 24-hour clock, and each request's answers.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import { respond } from "../../../../test/fakeFetch";
import {
  createRoutine,
  daySummary,
  deleteRoutine,
  routineTime,
  updateRoutine,
} from "../routines";

afterEach(() => {
  vi.unstubAllGlobals();
});

const routine = {
  id: "5f0c1d7e-8a1b-4c2d-9e3f-0a1b2c3d4e5f",
  label: "Wake up",
  hour: 6,
  minute: 30,
  days: [1, 2, 3, 4, 5],
  enabled: true,
  snoozeMinutes: 9,
  updatedAt: "2026-10-28T12:00:00+00:00",
};

const state = {
  configured: true,
  timeZone: "America/New_York",
  pollMinutes: 5,
  defaultLeadMinutes: 10,
  settings,
  bounds,
  mutedUntil: null,
  lastFetchAt: null,
  lastFetchError: null,
  lastSuccessAt: null,
  lastSend: null,
  alerts: [],
  routines: [routine],
};

const fields = {
  label: "Wake up",
  hour: 6,
  minute: 30,
  days: [1, 2, 3, 4, 5],
  enabled: true,
  snoozeMinutes: 9,
};

const stub = (answer: unknown) => {
  const fetchMock = vi.fn().mockResolvedValue(answer);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
};

describe("daySummary", () => {
  it.each([
    [[], "No repeat"],
    [[1, 2, 3, 4, 5, 6, 7], "Every day"],
    [[1, 2, 3, 4, 5], "Weekdays"],
    [[6, 7], "Weekends"],
    [[1, 3, 5], "Mon Wed Fri"],
    [[7], "Sun"],
    [[5, 1, 3], "Mon Wed Fri"],
    [[1, 2, 3, 4, 5, 6], "Mon Tue Wed Thu Fri Sat"],
  ])("%j is %s", (days, summary) => {
    expect(daySummary(days)).toBe(summary);
  });
});

describe("routineTime", () => {
  it.each([
    [6, 30, "06:30"],
    [0, 0, "00:00"],
    [23, 59, "23:59"],
    [13, 5, "13:05"],
  ])("%i:%i is %s", (hour, minute, text) => {
    expect(routineTime(hour, minute)).toBe(text);
  });
});

describe("requests", () => {
  it("create posts the fields and answers the state", async () => {
    const fetchMock = stub(respond(201, state));

    const result = await createRoutine(fields);

    const { configured: _, ...rest } = state;
    expect(result).toEqual({ ok: true, state: rest });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/alerts/routines",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify(fields),
      }),
    );
  });

  it("update puts the whole body under the id", async () => {
    const fetchMock = stub(respond(200, state));

    expect((await updateRoutine(routine.id, fields)).ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/alerts/routines/${routine.id}`,
      expect.objectContaining({ method: "PUT", body: JSON.stringify(fields) }),
    );
  });

  it("delete sends no body", async () => {
    const fetchMock = stub(respond(200, state));

    expect((await deleteRoutine(routine.id)).ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/alerts/routines/${routine.id}`,
      { method: "DELETE", credentials: "same-origin" },
    );
  });

  it("names each refusal", async () => {
    stub(respond(409, { detail: "At most 50 routine alarms." }));
    expect(await createRoutine(fields)).toEqual({
      ok: false,
      reason: "full",
      detail: "At most 50 routine alarms.",
    });

    stub(respond(400, { errors: { hour: ["0 to 23."] } }));
    expect(await createRoutine(fields)).toEqual({
      ok: false,
      reason: "invalid",
      errors: { hour: ["0 to 23."] },
    });

    stub(respond(404));
    expect(await deleteRoutine(routine.id)).toEqual({
      ok: false,
      reason: "missing",
    });

    stub(respond(401));
    expect(await deleteRoutine(routine.id)).toEqual({
      ok: false,
      reason: "visitor",
    });

    stub(respond(429));
    expect(await updateRoutine(routine.id, fields)).toEqual({
      ok: false,
      reason: "throttled",
    });

    stub(respond(500));
    expect(await updateRoutine(routine.id, fields)).toEqual({
      ok: false,
      reason: "refused",
    });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("down")));
    expect(await createRoutine(fields)).toEqual({
      ok: false,
      reason: "network",
    });
  });
});
