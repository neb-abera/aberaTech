/**
 * The alerts API from the page's side: a visitor is a status and not an
 * error, a deployment missing a secret names it, the owner gets the list,
 * and every button reports why it did not work.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import {
  fetchAlerts,
  formatWhen,
  muteAlerts,
  sendTestAlert,
  skipAlert,
  unmuteAlerts,
  unskipAlert,
} from "../api";

afterEach(() => {
  vi.unstubAllGlobals();
});

const state = {
  configured: true,
  timeZone: "America/New_York",
  pollMinutes: 5,
  defaultLeadMinutes: 10,
  mutedUntil: null,
  lastFetchAt: "2026-10-28T12:00:00+00:00",
  lastFetchError: null,
  lastSuccessAt: "2026-10-28T12:00:00+00:00",
  lastSend: null,
  alerts: [],
};

const stub = (answer: unknown) => {
  const fetchMock = vi.fn().mockResolvedValue(answer);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
};

describe("fetchAlerts", () => {
  it("gives the owner the state", async () => {
    stub(respond(200, state));

    const { configured: _, ...rest } = state;
    expect(await fetchAlerts()).toEqual({ status: "owner", state: rest });
  });

  it("treats a 401 and a 403 as a visitor", async () => {
    for (const status of [401, 403]) {
      stub(respond(status));
      expect(await fetchAlerts()).toEqual({ status: "visitor" });
    }
  });

  it("names what a deployment is missing", async () => {
    stub(
      respond(200, {
        configured: false,
        missing: ["Alerts__PushoverAppToken"],
      }),
    );

    expect(await fetchAlerts()).toEqual({
      status: "unconfigured",
      missing: ["Alerts__PushoverAppToken"],
    });
  });

  it("is an error when the server fails or nothing answers", async () => {
    stub(respond(500));
    expect(await fetchAlerts()).toEqual({ status: "error" });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("down")));
    expect(await fetchAlerts()).toEqual({ status: "error" });
  });

  it("is an error when the answer is the page shell rather than JSON", async () => {
    stub({ ...respond(200), headers: { get: () => "text/html" } });

    expect(await fetchAlerts()).toEqual({ status: "error" });
  });
});

describe("the buttons", () => {
  it("post what the server expects and hand back the new state", async () => {
    const fetchMock = stub(respond(200, state));

    await muteAlerts("hour");
    await muteAlerts("morning");
    await unmuteAlerts();
    await skipAlert("k|20261028T130000Z");
    const last = await unskipAlert("k|20261028T130000Z");

    const calls = fetchMock.mock.calls.map(([url, init]) => [
      url,
      init.method,
      init.body,
    ]);
    expect(calls).toEqual([
      ["/api/alerts/mute", "POST", JSON.stringify({ until: "hour" })],
      ["/api/alerts/mute", "POST", JSON.stringify({ until: "morning" })],
      ["/api/alerts/unmute", "POST", undefined],
      [
        "/api/alerts/skip",
        "POST",
        JSON.stringify({ key: "k|20261028T130000Z" }),
      ],
      [
        "/api/alerts/unskip",
        "POST",
        JSON.stringify({ key: "k|20261028T130000Z" }),
      ],
    ]);
    expect(last.ok && last.state?.timeZone).toBe("America/New_York");
  });

  it("say why a press did not work", async () => {
    stub(respond(429));
    expect(await unmuteAlerts()).toEqual({ ok: false, reason: "throttled" });

    stub(respond(401));
    expect(await unmuteAlerts()).toEqual({ ok: false, reason: "visitor" });

    stub(respond(404));
    expect(await skipAlert("gone")).toEqual({ ok: false, reason: "refused" });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("down")));
    expect(await unmuteAlerts()).toEqual({ ok: false, reason: "network" });
  });

  it("report what Pushover said when the test alert fails", async () => {
    stub({ ...respond(502), text: async () => "HTTP 400" });

    expect(await sendTestAlert()).toEqual({
      ok: false,
      reason: "pushover",
      detail: "HTTP 400",
    });

    stub(respond(200, { sent: true }));
    expect(await sendTestAlert()).toEqual({ ok: true });
  });
});

describe("formatWhen", () => {
  it("writes an instant in the calendar's zone, with the zone", () => {
    expect(formatWhen("2026-10-28T13:00:00Z", "America/New_York")).toBe(
      "Wed, Oct 28, 9:00 AM EDT",
    );
    expect(formatWhen("2026-11-02T14:00:00Z", "America/New_York")).toBe(
      "Mon, Nov 2, 9:00 AM EST",
    );
    expect(formatWhen("2026-10-28T13:00:00Z", "Asia/Amman")).toBe(
      "Wed, Oct 28, 4:00 PM GMT+3",
    );
  });

  it("falls back to UTC for a zone the browser does not know", () => {
    expect(formatWhen("2026-10-28T13:00:00Z", "Not/AZone")).toBe(
      "Wed, Oct 28, 1:00 PM UTC",
    );
  });
});
