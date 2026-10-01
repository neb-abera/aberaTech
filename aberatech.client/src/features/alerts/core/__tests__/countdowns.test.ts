/** Countdowns from the page's side: each request and each refusal. */

import { afterEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import { respond } from "../../../../test/fakeFetch";
import {
  createCountdown,
  deleteCountdown,
  updateCountdown,
} from "../countdowns";

afterEach(() => {
  vi.unstubAllGlobals();
});

const countdown = {
  id: "3d2c1b0a-9f8e-4d7c-8b6a-5f4e3d2c1b0a",
  label: "Home",
  targetAt: "2027-03-01T06:00:00+00:00",
  timeZone: "Asia/Amman",
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
  routines: [],
  countdowns: [countdown],
};

const fields = {
  label: "Home",
  targetAt: "2027-03-01T09:00:00+03:00",
  timeZone: "Asia/Amman",
};

const stub = (answer: unknown) => {
  const fetchMock = vi.fn().mockResolvedValue(answer);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
};

describe("countdown requests", () => {
  it("create posts the fields and answers the state", async () => {
    const fetchMock = stub(respond(201, state));

    const { configured: _, ...rest } = state;
    expect(await createCountdown(fields)).toEqual({ ok: true, state: rest });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/alerts/countdowns",
      expect.objectContaining({ method: "POST", body: JSON.stringify(fields) }),
    );
  });

  it("update puts the whole body under the id", async () => {
    const fetchMock = stub(respond(200, state));

    expect((await updateCountdown(countdown.id, fields)).ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/alerts/countdowns/${countdown.id}`,
      expect.objectContaining({ method: "PUT", body: JSON.stringify(fields) }),
    );
  });

  it("delete sends no body", async () => {
    const fetchMock = stub(respond(200, state));

    expect((await deleteCountdown(countdown.id)).ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/alerts/countdowns/${countdown.id}`,
      {
        method: "DELETE",
        credentials: "same-origin",
      },
    );
  });

  it.each([
    [
      409,
      { detail: "At most 50 countdowns." },
      { ok: false, reason: "full", detail: "At most 50 countdowns." },
    ],
    [
      400,
      { errors: { timeZone: ["A time zone."] } },
      { ok: false, reason: "invalid", errors: { timeZone: ["A time zone."] } },
    ],
    [404, null, { ok: false, reason: "missing" }],
    [401, null, { ok: false, reason: "visitor" }],
    [403, null, { ok: false, reason: "visitor" }],
    [429, null, { ok: false, reason: "throttled" }],
    [500, null, { ok: false, reason: "refused" }],
  ])("names a %i", async (status, body, expected) => {
    stub(respond(status, body));
    expect(await createCountdown(fields)).toEqual(expected);
  });

  it("names a lost connection", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    expect(await deleteCountdown(countdown.id)).toEqual({
      ok: false,
      reason: "network",
    });
  });
});
