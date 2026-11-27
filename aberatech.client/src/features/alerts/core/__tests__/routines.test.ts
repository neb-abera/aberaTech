/**
 * Routine alarms from the page's side: the day summaries, the words for
 * each repeat, the checks the form makes before it sends, the time on a
 * 24-hour clock, and each request's answers.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import { respond } from "../../../../test/fakeFetch";
import {
  createRoutine,
  daySummary,
  deleteRoutine,
  repeatErrors,
  repeatOf,
  repeatSummary,
  routineTime,
  startDay,
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

describe("repeatSummary", () => {
  const weekdays = (days: number[], every = 1) => ({
    days,
    repeat: "weekdays" as const,
    every,
    unit: "weeks" as const,
    startsOn: every > 1 ? "2026-11-25" : null,
  });
  const interval = (every: number, unit: string) => ({
    days: [],
    repeat: "interval" as const,
    every,
    unit,
    startsOn: "2026-11-25",
  });

  it.each([
    // Ring once.
    [weekdays([]), "No repeat"],
    // Every week: as before.
    [weekdays([1, 2, 3, 4, 5, 6, 7]), "Every day"],
    [weekdays([1, 2, 3, 4, 5]), "Weekdays"],
    [weekdays([6, 7]), "Weekends"],
    [weekdays([1, 3, 5]), "Mon Wed Fri"],
    // Every N weeks names the days.
    [weekdays([1, 3, 5], 2), "Every 2 weeks on Mon Wed Fri"],
    [weekdays([5, 1, 3], 2), "Every 2 weeks on Mon Wed Fri"],
    [weekdays([1, 3], 2), "Every 2 weeks on Mon Wed"],
    [weekdays([1, 2, 3, 4, 5], 3), "Every 3 weeks on Mon Tue Wed Thu Fri"],
    [weekdays([7], 999), "Every 999 weeks on Sun"],
    // An interval: singular and no number at 1.
    [interval(97, "minutes"), "Every 97 minutes"],
    [interval(1, "minutes"), "Every minute"],
    [interval(1, "hours"), "Every hour"],
    [interval(2, "hours"), "Every 2 hours"],
    [interval(1, "days"), "Every day"],
    [interval(3, "days"), "Every 3 days"],
    [interval(1, "weeks"), "Every week"],
    [interval(2, "weeks"), "Every 2 weeks"],
    [interval(1, "months"), "Every month"],
    [interval(6, "months"), "Every 6 months"],
    [interval(1, "years"), "Every year"],
    [interval(2, "years"), "Every 2 years"],
  ])("%j is %s", (routine, summary) => {
    expect(repeatSummary(routine)).toBe(summary);
  });

  it("reads a routine from an older server as weekdays every week", () => {
    expect(repeatSummary({ days: [1, 3, 5] })).toBe("Mon Wed Fri");
    expect(repeatSummary({ days: [] })).toBe("No repeat");
    expect(repeatOf({ days: [1] })).toEqual({
      repeat: "weekdays",
      every: 1,
      unit: "weeks",
      startsOn: null,
    });
  });

  it("reads an unknown repeat or unit as the defaults", () => {
    expect(
      repeatOf({ days: [], repeat: "lunar", every: 3, unit: "moons" }),
    ).toEqual({ repeat: "weekdays", every: 3, unit: "weeks", startsOn: null });
    expect(
      repeatSummary({ days: [], repeat: "interval", every: 3, unit: "moons" }),
    ).toBe("Every 3 weeks");
  });
});

describe("startDay", () => {
  it.each([
    ["2026-11-25", "Wed 25 Nov"],
    ["2026-01-01", "Thu 1 Jan"],
    ["2027-03-14", "Sun 14 Mar 2027"],
    ["2024-02-29", "Thu 29 Feb 2024"],
  ])("%s is %s in 2026", (date, text) => {
    expect(startDay(date, 2026)).toBe(text);
  });

  it("is empty for no date or a wrong one", () => {
    expect(startDay(null, 2026)).toBe("");
    expect(startDay("2026-02-30", 2026)).toBe("");
    expect(startDay("tomorrow", 2026)).toBe("");
  });
});

describe("repeatErrors", () => {
  const good = {
    days: [],
    repeat: "interval" as const,
    every: 97,
    unit: "minutes" as const,
    startsOn: "2026-11-25",
  };

  it("passes what the server takes", () => {
    expect(repeatErrors(good)).toEqual({});
    expect(repeatErrors({ ...good, every: 1 })).toEqual({});
    expect(repeatErrors({ ...good, every: 999 })).toEqual({});
    expect(
      repeatErrors({
        days: [1, 3],
        repeat: "weekdays",
        every: 2,
        unit: "weeks",
        startsOn: "2026-11-25",
      }),
    ).toEqual({});
    expect(
      repeatErrors({
        days: [],
        repeat: "weekdays",
        every: 1,
        unit: "weeks",
        startsOn: null,
      }),
    ).toEqual({});
  });

  it.each([[0], [1000], [-3], [1.5], [Number.NaN]])(
    "every %d is out of bounds",
    (every) => {
      expect(repeatErrors({ ...good, every })).toEqual({
        every: ["A whole number, 1 to 999."],
      });
    },
  );

  it("an interval needs a start date", () => {
    expect(repeatErrors({ ...good, startsOn: null })).toEqual({
      startsOn: ["A start date."],
    });
    expect(repeatErrors({ ...good, startsOn: "2026-02-30" })).toEqual({
      startsOn: ["A start date."],
    });
  });

  it("weekdays every 2 weeks or more need a day and a start date", () => {
    expect(
      repeatErrors({
        days: [],
        repeat: "weekdays",
        every: 2,
        unit: "weeks",
        startsOn: null,
      }),
    ).toEqual({
      days: ["Pick at least one day."],
      startsOn: ["A start date."],
    });
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

    // A server error may pass: the page keeps the change and sends it again.
    stub(respond(500));
    expect(await updateRoutine(routine.id, fields)).toEqual({
      ok: false,
      reason: "unavailable",
    });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("down")));
    expect(await createRoutine(fields)).toEqual({
      ok: false,
      reason: "network",
    });
  });
});
