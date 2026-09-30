/**
 * The alerts API from the page's side: a visitor is a status and not an
 * error, a deployment missing a secret names it, the owner gets the list,
 * and every button reports why it did not work.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import { respond } from "../../../../test/fakeFetch";
import {
  acknowledgeAlert,
  createEvent,
  deleteEvent,
  editEvent,
  fetchAlerts,
  formatClock,
  formatWhen,
  listDevices,
  muteAlerts,
  pairDevice,
  revokeDevice,
  saveAlertSettings,
  sendEventTest,
  sendTestAlert,
  sendTestNotification,
  setEventType,
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
  settings,
  bounds,
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

describe("sendEventTest", () => {
  it("posts the alert's key, and says what Pushover answered on a 502", async () => {
    const fetchMock = stub(respond(200, { sent: true }));
    expect(await sendEventTest("standup|20261028T130000Z")).toEqual({
      ok: true,
    });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/alerts/test-event",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ key: "standup|20261028T130000Z" }),
      }),
    );

    stub({ ...respond(502), text: async () => "HTTP 400" });
    expect(await sendEventTest("standup|20261028T130000Z")).toEqual({
      ok: false,
      reason: "pushover",
      detail: "HTTP 400",
    });

    stub(respond(404));
    expect(await sendEventTest("gone")).toEqual({
      ok: false,
      reason: "refused",
    });
  });
});

describe("sendTestNotification and setEventType", () => {
  it("post the notification test and put an event's type, handing back the state", async () => {
    const fetchMock = stub(respond(200, { sent: true }));
    expect(await sendTestNotification()).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/alerts/test-notification",
      expect.objectContaining({ method: "POST" }),
    );

    const put = stub(respond(200, state));
    const answer = await setEventType("review|20261028T180000Z", "alarm");
    expect(answer.ok && answer.state?.timeZone).toBe("America/New_York");
    expect(put).toHaveBeenCalledWith(
      "/api/alerts/event-type",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ key: "review|20261028T180000Z", type: "alarm" }),
      }),
    );

    stub(respond(404));
    expect(await setEventType("gone", "default")).toEqual({
      ok: false,
      reason: "refused",
    });
  });
});

describe("saveAlertSettings", () => {
  it("puts the whole form and hands back the stored state", async () => {
    const saved = { ...settings, sound: "siren" };
    const fetchMock = stub(respond(200, { ...state, settings: saved }));

    const result = await saveAlertSettings(saved);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/alerts/settings");
    expect(init.method).toBe("PUT");
    expect(init.headers).toEqual({ "Content-Type": "application/json" });
    expect(JSON.parse(init.body)).toEqual(saved);
    expect(result.ok && result.state?.settings).toEqual(saved);
  });

  it("hands back the server's message for each refused field", async () => {
    stub(
      respond(400, {
        title: "One or more validation errors occurred.",
        errors: { repeatSeconds: ["Between 30 and 10800 seconds."] },
      }),
    );

    expect(await saveAlertSettings({ ...settings, repeatSeconds: 5 })).toEqual({
      ok: false,
      reason: "invalid",
      errors: { repeatSeconds: ["Between 30 and 10800 seconds."] },
    });
  });

  it("treats a 400 without field errors as a refusal", async () => {
    stub({ ...respond(400), json: async () => ({}) });
    expect(await saveAlertSettings(settings)).toEqual({
      ok: false,
      reason: "refused",
    });

    stub({ ...respond(400), headers: { get: () => "text/plain" } });
    expect(await saveAlertSettings(settings)).toEqual({
      ok: false,
      reason: "refused",
    });
  });

  it("says when the presses ran out or the session ended", async () => {
    stub(respond(429));
    expect(await saveAlertSettings(settings)).toEqual({
      ok: false,
      reason: "throttled",
    });

    stub(respond(403));
    expect(await saveAlertSettings(settings)).toEqual({
      ok: false,
      reason: "visitor",
    });
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

const text = (status: number, body: string) => ({
  ...respond(status),
  text: async () => body,
});

describe("the phone and the ring", () => {
  it("acknowledges as the browser in a JSON body and answers with the state", async () => {
    const fetchMock = stub(respond(200, state));

    const { configured: _, ...rest } = state;
    expect(await acknowledgeAlert("k|1", "browser")).toEqual({
      ok: true,
      state: rest,
    });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/alerts/ack");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ key: "k|1", via: "browser" });
  });

  it("lists the phones, and a 401 or 403 is a visitor", async () => {
    stub(
      respond(200, [
        { id: "a", name: "P", createdAt: "x", lastSeenAt: null, push: true },
      ]),
    );
    expect(await listDevices()).toEqual({
      ok: true,
      devices: [
        { id: "a", name: "P", createdAt: "x", lastSeenAt: null, push: true },
      ],
    });
    stub(respond(403));
    expect(await listDevices()).toEqual({ ok: false, reason: "visitor" });
    stub(respond(500));
    expect(await listDevices()).toEqual({ ok: false, reason: "refused" });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    expect(await listDevices()).toEqual({ ok: false, reason: "network" });
  });

  it("pairs with the name in the body, never the query, and reads each refusal", async () => {
    const device = {
      id: "a",
      name: "P",
      createdAt: "x",
      token: "aat_t",
      pairUrl: "aberaalarms://pair#token=aat_t",
    };
    const fetchMock = stub(respond(201, device));
    expect(await pairDevice("P")).toEqual({ ok: true, device });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/alerts/devices");
    expect(JSON.parse(init.body)).toEqual({ name: "P" });

    stub(text(409, "At most 5 phones. Revoke one first."));
    expect(await pairDevice("P")).toEqual({
      ok: false,
      reason: "full",
      detail: "At most 5 phones. Revoke one first.",
    });
    for (const [status, reason] of [
      [400, "invalid"],
      [401, "visitor"],
      [429, "throttled"],
      [500, "refused"],
    ] as const) {
      stub(respond(status));
      expect(await pairDevice("P")).toEqual({ ok: false, reason });
    }
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    expect(await pairDevice("P")).toEqual({ ok: false, reason: "network" });
  });

  it("revokes by id, and a phone already gone counts as revoked", async () => {
    const fetchMock = stub(respond(204));
    expect(await revokeDevice("a b")).toEqual({ ok: true });
    expect(fetchMock.mock.calls[0][0]).toBe("/api/alerts/devices/a%20b");
    expect(fetchMock.mock.calls[0][1].method).toBe("DELETE");
    stub(respond(404));
    expect(await revokeDevice("a")).toEqual({ ok: true });
    stub(respond(429));
    expect(await revokeDevice("a")).toEqual({ ok: false, reason: "throttled" });
    stub(respond(401));
    expect(await revokeDevice("a")).toEqual({ ok: false, reason: "visitor" });
    stub(respond(500));
    expect(await revokeDevice("a")).toEqual({ ok: false, reason: "refused" });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    expect(await revokeDevice("a")).toEqual({ ok: false, reason: "network" });
  });

  it("writes a time of day on a 24-hour clock in the zone given", () => {
    expect(formatClock("2026-10-28T17:52:00Z", "America/New_York")).toBe(
      "13:52",
    );
    expect(formatClock("2026-10-28T17:52:00Z", "Not/AZone")).toBe("17:52");
  });
});

describe("createEvent", () => {
  const event = {
    title: "Dentist",
    startsAt: "2026-10-28T15:00:00.000Z",
    durationMinutes: 30,
    location: null,
    type: "alarm" as const,
    leadMinutes: 20,
  };

  it("posts the event in a JSON body and hands back the state on a 201", async () => {
    const fetchMock = stub(respond(201, state));

    const { configured: _, ...rest } = state;
    expect(await createEvent(event)).toEqual({ ok: true, state: rest });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/alerts/events");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual(event);
  });

  it("reads each refusal: fields, no calendar, Google, the session, the limit, and no answer", async () => {
    stub(respond(400, { errors: { title: ["1 to 200 characters."] } }));
    expect(await createEvent(event)).toEqual({
      ok: false,
      reason: "invalid",
      errors: { title: ["1 to 200 characters."] },
    });

    stub(
      respond(409, {
        detail: "The connected calendar is not the one alerts read.",
      }),
    );
    expect(await createEvent(event)).toEqual({
      ok: false,
      reason: "conflict",
      detail: "The connected calendar is not the one alerts read.",
    });

    stub(respond(502, { detail: "Google refused the change (HTTP 400)." }));
    expect(await createEvent(event)).toEqual({
      ok: false,
      reason: "google",
      detail: "Google refused the change (HTTP 400).",
    });

    stub(respond(403));
    expect(await createEvent(event)).toEqual({ ok: false, reason: "visitor" });
    stub(respond(429));
    expect(await createEvent(event)).toEqual({
      ok: false,
      reason: "throttled",
    });
    stub(respond(400, { title: "no field errors" }));
    expect(await createEvent(event)).toEqual({ ok: false, reason: "refused" });
    stub({ ...respond(500), headers: { get: () => "text/plain" } });
    expect(await createEvent(event)).toEqual({ ok: false, reason: "refused" });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    expect(await createEvent(event)).toEqual({ ok: false, reason: "network" });
  });
});

describe("editEvent and deleteEvent", () => {
  const edit = {
    key: "standup@google.com|20261028T130000Z",
    scope: "occurrence" as const,
    title: "Standup",
    startsAt: "2026-10-28T13:30:00.000Z",
    location: null,
  };

  it("send the key in the body, never the address, and answer with the state", async () => {
    const fetchMock = stub(respond(200, state));

    const edited = await editEvent(edit);
    const deleted = await deleteEvent(edit.key, "series");

    const { configured: _, ...rest } = state;
    expect(edited).toEqual({ ok: true, state: rest });
    expect(deleted).toEqual({ ok: true, state: rest });
    expect(
      fetchMock.mock.calls.map(([url, init]) => [url, init.method]),
    ).toEqual([
      ["/api/alerts/events", "PUT"],
      ["/api/alerts/events/delete", "POST"],
    ]);
    expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({
      key: edit.key,
      scope: "series",
    });
    for (const [url] of fetchMock.mock.calls)
      expect(url).not.toContain("standup");
  });

  it("report a gone event, a refusal, Google's failure and refused fields", async () => {
    stub(respond(404));
    expect(await editEvent(edit)).toEqual({ ok: false, reason: "gone" });
    stub(respond(409, { detail: "Only the organizer can change this event." }));
    expect(await deleteEvent(edit.key, "occurrence")).toEqual({
      ok: false,
      reason: "conflict",
      detail: "Only the organizer can change this event.",
    });
    stub(respond(502, { detail: "Google Calendar did not answer." }));
    expect(await editEvent(edit)).toEqual({
      ok: false,
      reason: "google",
      detail: "Google Calendar did not answer.",
    });
    stub(respond(400, { errors: { scope: ['"occurrence" or "series".'] } }));
    expect(await deleteEvent(edit.key, "occurrence")).toEqual({
      ok: false,
      reason: "invalid",
      errors: { scope: ['"occurrence" or "series".'] },
    });
    stub(respond(429));
    expect(await editEvent(edit)).toEqual({ ok: false, reason: "throttled" });
    stub(respond(401));
    expect(await editEvent(edit)).toEqual({ ok: false, reason: "visitor" });
  });

  it("a 404 to a new event is a refusal, never gone", async () => {
    stub(respond(404));
    expect(
      await createEvent({
        title: "A",
        startsAt: "2026-10-28T15:00:00.000Z",
        durationMinutes: 30,
        location: null,
        type: "alarm",
      }),
    ).toEqual({ ok: false, reason: "refused" });
  });
});
