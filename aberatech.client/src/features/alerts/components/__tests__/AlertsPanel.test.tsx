// @vitest-environment jsdom
/**
 * The /alerts page from every button on it. A visitor gets a sign-in that
 * comes back here. A deployment missing a secret names it and says how to
 * set it. The owner sees the state and the next alerts, and each button
 * posts once and shows what the server stored.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import { respond } from "../../../../test/fakeFetch";
import AlertsPanel from "../AlertsPanel";
import type { PhonesApi } from "../PhonesSection";

/** No phones, and no request for them: PhonesSection.test.tsx covers the list. */
const noPhones: PhonesApi = {
  list: async () => ({ ok: true, devices: [] }),
  pair: async () => ({ ok: false, reason: "refused" }),
  revoke: async () => ({ ok: true }),
};

let fetchMock: ReturnType<typeof vi.fn>;

const standup = {
  key: "standup@google.com|20261028T130000Z",
  title: "Standup",
  location: "Room 1",
  startsAt: "2026-10-28T13:00:00+00:00",
  alertAt: "2026-10-28T12:45:00+00:00",
  source: "reminder",
  skipped: false,
  muted: false,
  critical: true,
  type: "alarm",
  typeFrom: "critical",
  acknowledged: false,
  acknowledgedAt: null,
  acknowledgedVia: null,
};

const review = {
  key: "review@google.com|20261028T180000Z",
  title: "Review",
  location: null,
  startsAt: "2026-10-28T18:00:00+00:00",
  alertAt: "2026-10-28T17:50:00+00:00",
  source: "default",
  skipped: false,
  muted: false,
  critical: false,
  type: "none",
  typeFrom: "default",
  acknowledged: false,
  acknowledgedAt: null,
  acknowledgedVia: null,
};

const state = (over: Record<string, unknown> = {}) => ({
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
  alerts: [standup, review],
  ...over,
});

const settle = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
  });
};

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

function mount(...answers: unknown[]) {
  fetchMock = vi.fn();
  for (const answer of answers) fetchMock.mockResolvedValueOnce(answer);
  fetchMock.mockResolvedValue(answers[answers.length - 1]);
  vi.stubGlobal("fetch", fetchMock);
  render(<AlertsPanel phones={noPhones} />);
}

const posts = () =>
  fetchMock.mock.calls
    .filter(([, init]) => init?.method === "POST")
    .map(([url, init]) => [url, init.body]);

describe("a visitor", () => {
  it("is offered sign-in that comes back here, and nothing is posted", async () => {
    mount(respond(401));
    await settle();

    expect(
      screen
        .getByRole("link", { name: "Sign in with Google" })
        .getAttribute("href"),
    ).toBe("/api/scheduling/admin/sign-in?returnUrl=/alerts");
    expect(screen.queryByRole("button", { name: "Mute 1 hour" })).toBeNull();
    expect(posts()).toHaveLength(0);
  });
});

describe("phone pushes", () => {
  it("names the missing push secrets under Phones and nothing when pushes are on", async () => {
    mount(
      respond(
        200,
        state({
          push: { on: false, missing: ["Alerts__ApnsKeyId"] },
        }),
      ),
    );
    await settle();

    expect(
      screen.getByText(
        "Pushes are off. The server is missing Alerts__ApnsKeyId.",
      ),
    ).toBeTruthy();
    cleanup();

    mount(respond(200, state({ push: { on: true, missing: [] } })));
    await settle();
    expect(screen.queryByText(/Pushes are off/)).toBeNull();
  });
});

describe("a deployment missing a secret", () => {
  it("names each missing setting and gives the steps to set it", async () => {
    mount(
      respond(200, {
        configured: false,
        missing: ["Alerts__PushoverAppToken", "Alerts__PushoverUserKey"],
      }),
    );
    await settle();

    const missing = screen.getByRole("list", { name: "Missing settings" });
    expect(
      within(missing)
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual(["Alerts__PushoverAppToken", "Alerts__PushoverUserKey"]);
    const steps = screen.getByRole("list", { name: "Steps to switch on" });
    expect(steps.tagName).toBe("OL");
    expect(within(steps).getAllByRole("listitem")).toHaveLength(3);
    expect(screen.queryByRole("button", { name: "Mute 1 hour" })).toBeNull();
  });

  it("says so plainly when the deployment has no owner sign-in", async () => {
    mount(respond(200, { configured: false, missing: [] }));
    await settle();

    expect(screen.getByText(/no owner sign-in/)).toBeTruthy();
  });
});

describe("the owner", () => {
  it("sees the state, the last read and the next alerts in the calendar's zone", async () => {
    mount(respond(200, state()));
    await settle();

    expect(screen.getByLabelText("Alert state: active")).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Unmute" }).hasAttribute("disabled"),
    ).toBe(true);
    expect(
      screen.getByText(/Calendar read Wed, Oct 28, 8:00 AM EDT/),
    ).toBeTruthy();
    expect(
      screen.getByText(/Calendar read .*, every 5 minutes\./),
    ).toBeTruthy();

    const list = screen.getByRole("list", { name: "Next alerts" });
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain("Standup");
    expect(items[0].textContent).toContain("Alert Wed, Oct 28, 8:45 AM EDT");
    expect(items[0].textContent).toContain("starts Wed, Oct 28, 9:00 AM EDT");
    expect(items[0].textContent).toContain("Room 1");
    expect(items[0].textContent).toContain(
      "Time from the event's notification.",
    );
    expect(items[1].textContent).toContain(
      "No notification in the feed: 10 minutes before, the default lead.",
    );
  });

  it("says what an alarm, a notification and an unmarked event each do", async () => {
    mount(respond(200, state()));
    await settle();

    const paragraph = screen.getByText(
      /It rings every 60 s until you acknowledge it on the phone or here, and stops after 50 min\./,
    );
    expect(paragraph.textContent).toContain(
      "Ring until stopped is one Pushover message.",
    );
    expect(paragraph.textContent).toContain(
      "Ring once is one message with one sound. It follows the phone's settings.",
    );
    expect(paragraph.textContent).toContain(
      "An event with no mark and no type set here sends nothing.",
    );

    cleanup();
    mount(
      respond(
        200,
        state({
          settings: {
            ...settings,
            notificationPriority: 1,
            defaultType: "notification",
          },
        }),
      ),
    );
    await settle();
    expect(
      screen.getByText(
        /Ring once is one message with one sound, even during Pushover's quiet hours\. An event with no mark and no type set here rings once\./,
      ),
    ).toBeTruthy();
  });

  it("marks the alarms Critical and says where each alert's type came from", async () => {
    mount(respond(200, state()));
    await settle();

    const list = screen.getByRole("list", { name: "Next alerts" });
    const [loud, quiet] = within(list).getAllByRole("listitem");
    expect(within(loud).getByText("Critical")).toBeTruthy();
    expect(within(quiet).queryByText("Critical")).toBeNull();
    expect(loud.textContent).toContain(
      "Ring until stopped: from #critical in the calendar. The Ring until stopped settings apply.",
    );
    expect(quiet.textContent).toContain(
      "Sends nothing: the default for unmarked events. Send test is off until you choose Ring once or Ring until stopped.",
    );
    const at = "Review at Wed, Oct 28, 2:00 PM EDT";
    expect(
      screen
        .getByRole("button", { name: `Set ${at} to Off` })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(
      screen
        .getByRole("button", { name: `Set ${at} to Ring until stopped` })
        .getAttribute("aria-pressed"),
    ).toBe("false");
    // An event that sends nothing has nothing to test.
    expect(
      screen
        .getByRole("button", { name: `Send test of ${at}` })
        .hasAttribute("disabled"),
    ).toBe(true);
    expect(
      screen
        .getByRole("button", {
          name: "Send test of Standup at Wed, Oct 28, 9:00 AM EDT",
        })
        .hasAttribute("disabled"),
    ).toBe(false);
    expect(
      screen.queryByRole("button", { name: `Use the default type for ${at}` }),
    ).toBeNull();
  });

  it("sets an event's type, then offers the default back", async () => {
    const chosen = { ...review, type: "notification", typeFrom: "set" };
    mount(
      respond(200, state()),
      respond(200, state({ alerts: [standup, chosen] })),
      respond(200, state()),
    );
    await settle();
    const at = "Review at Wed, Oct 28, 2:00 PM EDT";

    fireEvent.click(
      screen.getByRole("button", { name: `Set ${at} to Ring once` }),
    );
    await settle();

    const puts = () =>
      fetchMock.mock.calls
        .filter(([, init]) => init?.method === "PUT")
        .map(([url, init]) => [url, JSON.parse(init.body)]);
    expect(puts()).toEqual([
      ["/api/alerts/event-type", { key: review.key, type: "notification" }],
    ]);
    expect(
      screen.getByText("Review is set to Ring once, every occurrence."),
    ).toBeTruthy();
    expect(
      screen.getByText("Ring once: set here. The Ring once settings apply."),
    ).toBeTruthy();
    expect(
      screen
        .getByRole("button", { name: `Send test of ${at}` })
        .hasAttribute("disabled"),
    ).toBe(false);

    fireEvent.click(
      screen.getByRole("button", { name: `Use the default type for ${at}` }),
    );
    await settle();
    expect(puts()[1]).toEqual([
      "/api/alerts/event-type",
      { key: review.key, type: "default" },
    ]);
    expect(
      screen.getByText("Review follows the calendar and the default again."),
    ).toBeTruthy();
  });

  it("warns when Google Calendar was not changed, keeps the warning past the next refresh, and closes it", async () => {
    const chosen = { ...review, type: "alarm", typeFrom: "set" };
    mount(
      respond(200, state()),
      respond(
        200,
        state({
          alerts: [standup, chosen],
          calendarWrite: "Google Calendar is not connected with edit access.",
        }),
      ),
      respond(200, state({ alerts: [standup, chosen] })),
    );
    await settle();
    expect(
      screen.getByText(
        "Each type holds for every occurrence of the event. Ring until stopped also adds #critical to the event in Google Calendar. Off and Ring once remove it.",
      ),
    ).toBeTruthy();
    expect(screen.queryByText("Google Calendar was not changed")).toBeNull();

    fireEvent.click(
      screen.getByRole("button", {
        name: "Set Review at Wed, Oct 28, 2:00 PM EDT to Ring until stopped",
      }),
    );
    await settle();

    const warning = screen
      .getByText("Google Calendar was not changed")
      .closest('[role="alert"]') as HTMLElement;
    expect(warning.textContent).toContain(
      "Google Calendar is not connected with edit access. The choice is saved here and alerts follow it.",
    );

    await act(async () => {
      vi.advanceTimersByTime(60_000);
    });
    await settle();
    expect(screen.getByText("Google Calendar was not changed")).toBeTruthy();

    fireEvent.click(within(warning).getByRole("button", { name: "Close" }));
    expect(screen.queryByText("Google Calendar was not changed")).toBeNull();
  });

  it("shows no calendar warning when the write worked", async () => {
    mount(respond(200, state()), respond(200, state({ calendarWrite: null })));
    await settle();
    fireEvent.click(
      screen.getByRole("button", {
        name: "Set Review at Wed, Oct 28, 2:00 PM EDT to Ring until stopped",
      }),
    );
    await settle();
    expect(screen.queryByText("Google Calendar was not changed")).toBeNull();
  });

  it("adds a new event and lists it from the answer", async () => {
    const dentist = {
      ...review,
      key: "created1@google.com|20261028T150000Z",
      title: "Dentist",
      type: "alarm",
      typeFrom: "set",
      critical: true,
    };
    mount(
      respond(200, state()),
      respond(201, state({ alerts: [standup, dentist, review] })),
    );
    await settle();

    fireEvent.change(screen.getByLabelText(/^Title/), {
      target: { value: "Dentist" },
    });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Add event" }));
    });
    await settle();

    expect(posts()[0][0]).toBe("/api/alerts/events");
    expect(JSON.parse(posts()[0][1] as string).title).toBe("Dentist");
    expect(
      within(screen.getByRole("list", { name: "Next alerts" })).getByText(
        "Dentist",
      ),
    ).toBeTruthy();
  });

  it("sends a test notification and says so", async () => {
    mount(respond(200, state()), respond(200, { sent: true }));
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Test: ring once" }));
    await settle();

    expect(posts()).toEqual([["/api/alerts/test-notification", undefined]]);
    expect(
      screen.getByText("Ring once test sent. Check the phone for one sound."),
    ).toBeTruthy();
  });

  it("describes what the saved settings do, with the saved numbers", async () => {
    mount(
      respond(
        200,
        state({
          settings: { ...settings, repeatSeconds: 120, stopAfterMinutes: 30 },
        }),
      ),
    );
    await settle();
    expect(
      screen.getByText(
        "Ring until stopped: rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText(
        /It rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min\./,
      ),
    ).toBeTruthy();
  });

  it("says Emergency nowhere on the page", async () => {
    mount(respond(200, state()));
    await settle();

    expect(screen.getByRole("heading", { name: "Settings" })).toBeTruthy();
    expect(document.body.textContent).not.toMatch(/emergency/i);
    expect(document.body.textContent).not.toMatch(/priority 2/i);
  });

  it("names the types Ring until stopped, Ring once and Off, and no button or heading Alarm, Notification or None", async () => {
    mount(respond(200, state()));
    await settle();

    for (const label of ["Ring until stopped", "Ring once", "Off"])
      expect(
        screen.getAllByText(label, { exact: true }).length,
      ).toBeGreaterThan(0);
    expect(
      screen.getByRole("button", { name: "Test: ring until stopped" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Test: ring once" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("heading", { name: "Ring until stopped settings" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("heading", { name: "Ring once settings" }),
    ).toBeTruthy();

    const old = /\b(Alarms?|Notifications?|None)\b/;
    const named = [
      ...screen.getAllByRole("button"),
      ...screen.getAllByRole("heading"),
    ].flatMap((element) => [
      element.getAttribute("aria-label") ?? "",
      element.textContent ?? "",
    ]);
    expect(named.filter((name) => old.test(name))).toEqual([]);
  });

  it("saves the settings, then shows what the server stored", async () => {
    mount(
      respond(200, state()),
      respond(
        200,
        state({
          settings: { ...settings, repeatSeconds: 120 },
        }),
      ),
    );
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Repeat every 2 min" }));
    fireEvent.click(screen.getByRole("button", { name: "Save settings" }));
    await settle();

    const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(put?.[0]).toBe("/api/alerts/settings");
    expect(JSON.parse(put?.[1].body)).toEqual({
      ...settings,
      repeatSeconds: 120,
    });
    expect(screen.getByText("Settings saved.")).toBeTruthy();
    expect(
      screen.getByText(
        "Ring until stopped: rings every 120 s until you acknowledge it on the phone or here, and stops after 100 min.",
      ),
    ).toBeTruthy();
  });

  it("mutes for an hour and shows until when", async () => {
    mount(
      respond(200, state()),
      respond(
        200,
        state({
          mutedUntil: "2026-10-28T13:00:00+00:00",
          alerts: [{ ...standup, muted: true }, review],
        }),
      ),
    );
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Mute 1 hour" }));
    await settle();

    expect(posts()).toEqual([
      ["/api/alerts/mute", JSON.stringify({ until: "hour" })],
    ]);
    expect(screen.getByLabelText("Alert state: muted").textContent).toContain(
      "Muted until Wed, Oct 28, 9:00 AM EDT",
    );
    expect(
      screen.getByRole("button", { name: "Unmute" }).hasAttribute("disabled"),
    ).toBe(false);
    const first = within(
      screen.getByRole("list", { name: "Next alerts" }),
    ).getAllByRole("listitem")[0];
    expect(first.textContent).toContain("Muted");
  });

  it("mutes until six tomorrow morning", async () => {
    mount(
      respond(200, state()),
      respond(200, state({ mutedUntil: "2026-10-29T10:00:00+00:00" })),
    );
    await settle();

    fireEvent.click(
      screen.getByRole("button", { name: "Mute until tomorrow 06:00" }),
    );
    await settle();

    expect(posts()).toEqual([
      ["/api/alerts/mute", JSON.stringify({ until: "morning" })],
    ]);
    expect(screen.getByLabelText("Alert state: muted").textContent).toContain(
      "Muted until Thu, Oct 29, 6:00 AM EDT",
    );
  });

  it("unmutes", async () => {
    mount(
      respond(200, state({ mutedUntil: "2026-10-28T13:00:00+00:00" })),
      respond(200, state()),
    );
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Unmute" }));
    await settle();

    expect(posts()).toEqual([["/api/alerts/unmute", undefined]]);
    expect(screen.getByLabelText("Alert state: active")).toBeTruthy();
  });

  it("skips one occurrence and can undo it", async () => {
    mount(
      respond(200, state()),
      respond(200, state({ alerts: [{ ...standup, skipped: true }, review] })),
      respond(200, state()),
    );
    await settle();

    fireEvent.click(
      screen.getByRole("button", {
        name: "Skip Standup at Wed, Oct 28, 9:00 AM EDT",
      }),
    );
    await settle();

    expect(posts()).toEqual([
      ["/api/alerts/skip", JSON.stringify({ key: standup.key })],
    ]);
    const undo = screen.getByRole("button", {
      name: "Undo skip of Standup at Wed, Oct 28, 9:00 AM EDT",
    });
    expect(
      within(screen.getByRole("list", { name: "Next alerts" })).getAllByRole(
        "listitem",
      )[0].textContent,
    ).toContain("Skipped");

    fireEvent.click(undo);
    await settle();
    expect(posts()[1]).toEqual([
      "/api/alerts/unskip",
      JSON.stringify({ key: standup.key }),
    ]);
    expect(
      screen.getByRole("button", {
        name: "Skip Standup at Wed, Oct 28, 9:00 AM EDT",
      }),
    ).toBeTruthy();
  });

  it("sends a test alert and says so, or says what Pushover answered", async () => {
    mount(respond(200, state()), respond(200, { sent: true }), {
      ...respond(502),
      text: async () => "HTTP 400",
    });
    await settle();

    fireEvent.click(
      screen.getByRole("button", { name: "Test: ring until stopped" }),
    );
    await settle();
    expect(posts()[0]).toEqual(["/api/alerts/test", undefined]);
    expect(
      screen.getByText(
        "Ring until stopped test sent. It rings every 60 s until you acknowledge it on the phone or here, and stops after 50 min.",
      ),
    ).toBeTruthy();

    fireEvent.click(
      screen.getByRole("button", { name: "Test: ring until stopped" }),
    );
    await settle();
    expect(
      screen.getByText(/Pushover refused the test: HTTP 400/),
    ).toBeTruthy();
  });

  it("says to wait when the presses run out", async () => {
    mount(respond(200, state()), respond(429));
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Mute 1 hour" }));
    await settle();

    expect(screen.getByText("Too many presses. Wait a minute.")).toBeTruthy();
    expect(screen.getByLabelText("Alert state: active")).toBeTruthy();
  });

  it("puts a failed calendar read at the top: the error, the last good read, and where the list is from", async () => {
    mount(
      respond(
        200,
        state({
          lastFetchAt: "2026-10-28T12:05:00+00:00",
          lastFetchError: "HTTP 404",
        }),
      ),
    );
    await settle();

    const banner = screen.getAllByRole("alert")[0];
    expect(banner.className).toContain("MuiAlert-colorError");
    expect(banner.textContent).toContain("The calendar cannot be read");
    expect(banner.textContent).toContain(
      "The last read, Wed, Oct 28, 8:05 AM EDT, failed: HTTP 404.",
    );
    expect(banner.textContent).toContain(
      "Google answers 404 when the secret address is wrong or was reset.",
    );
    expect(banner.textContent).toContain(
      "The last good read was Wed, Oct 28, 8:00 AM EDT. The alerts below are from that read.",
    );
    // Above every button, so it is the first thing on the page.
    const mute = screen.getByRole("button", { name: "Mute 1 hour" });
    expect(
      banner.compareDocumentPosition(mute) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("says no alerts are planned when no read has worked, and names no 404 cause for another error", async () => {
    mount(
      respond(
        200,
        state({
          alerts: [],
          lastFetchAt: "2026-10-28T12:05:00+00:00",
          lastFetchError: "The feed is not a calendar.",
          lastSuccessAt: null,
        }),
      ),
    );
    await settle();

    const banner = screen.getAllByRole("alert")[0];
    expect(banner.textContent).toContain(
      "No read has worked since the server started, so no alerts are planned.",
    );
    expect(banner.textContent).not.toContain("404");
    expect(screen.queryByText(/Calendar read /)).toBeNull();
    expect(
      screen.getByText(/No read of the calendar has worked yet/),
    ).toBeTruthy();
  });

  it("shows no banner when the last read worked", async () => {
    mount(respond(200, state()));
    await settle();

    expect(screen.queryByText(/The calendar cannot be read/)).toBeNull();
  });

  it("sends a test of one listed alert and says what the phone shows, or what Pushover answered", async () => {
    mount(respond(200, state()), respond(200, { sent: true }), {
      ...respond(502),
      text: async () => "HTTP 400",
    });
    await settle();

    const press = () =>
      fireEvent.click(
        screen.getByRole("button", {
          name: "Send test of Standup at Wed, Oct 28, 9:00 AM EDT",
        }),
      );
    press();
    await settle();
    expect(posts()).toEqual([
      ["/api/alerts/test-event", JSON.stringify({ key: standup.key })],
    ]);
    expect(
      screen.getByText(
        'Test of Standup sent, titled "Test: Standup", set to ring until stopped.',
      ),
    ).toBeTruthy();

    press();
    await settle();
    expect(
      screen.getByText(/Pushover refused the test: HTTP 400/),
    ).toBeTruthy();
  });

  it("explains which events alert and when, with the saved values and Google's menu names", async () => {
    mount(respond(200, state()));
    await settle();

    const how = screen.getByRole("region", {
      name: "How events alert",
    });
    const text = how.textContent ?? "";
    expect(text).toContain(
      "Every event with a start time in the next 48 hours is planned and listed above.",
    );
    expect(text).toContain(
      "has #critical as a word of its own, in any case. The mark is left off the title the phone shows.",
    );
    expect(text).toContain(
      "Every other event sends nothing, the default for unmarked events under Settings.",
    );
    expect(text).toContain("The choice holds for every occurrence");
    expect(text).toContain(
      "Off: nothing is sent. The event is still listed above.",
    );
    expect(text).toContain("All-day events are left out.");
    expect(text).toContain(
      "Cancelled events and invitations you declined are left out.",
    );
    expect(text).toContain(
      "An event with no notification in the feed alerts 10 minutes before it starts, the default lead.",
    );
    expect(text).toContain("click Edit event");
    expect(text).toContain("Add notification");
    expect(text).toContain("Settings for my calendars");
    expect(text).toContain("Event notifications");

    cleanup();
    mount(
      respond(
        200,
        state({
          settings: {
            ...settings,
            includeAllDay: true,
            lookaheadHours: 1,
            defaultType: "notification",
          },
        }),
      ),
    );
    await settle();
    const on =
      screen.getByRole("region", { name: "How events alert" }).textContent ??
      "";
    expect(on).toContain("in the next 1 hour is planned");
    expect(on).toContain("Every other event rings once");
    expect(on).toContain("All-day events alert too");
  });

  it("says when there is nothing coming and when the calendar has not been read", async () => {
    mount(
      respond(
        200,
        state({ alerts: [], lastFetchAt: null, lastSuccessAt: null }),
      ),
    );
    await settle();

    expect(screen.getByText(/No alerts coming up/)).toBeTruthy();
    expect(screen.getByText(/not read yet/)).toBeTruthy();
    expect(screen.queryByText(/The calendar cannot be read/)).toBeNull();
  });

  it("shows the last send", async () => {
    mount(
      respond(
        200,
        state({
          lastSend: {
            at: "2026-10-28T12:45:00+00:00",
            title: "Standup",
            outcome: "sent",
          },
        }),
      ),
    );
    await settle();

    expect(
      screen.getByText("Last send: Standup, Wed, Oct 28, 8:45 AM EDT, sent."),
    ).toBeTruthy();
  });

  it("asks again every minute without being pressed", async () => {
    mount(respond(200, state()), respond(200, state({ alerts: [review] })));
    await settle();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      vi.advanceTimersByTime(60_000);
    });
    await settle();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(
      within(screen.getByRole("list", { name: "Next alerts" })).getAllByRole(
        "listitem",
      ),
    ).toHaveLength(1);
  });
});

describe("an acknowledged alert", () => {
  it("says where and when it was acknowledged, in the calendar's zone on a 24-hour clock", async () => {
    mount(
      respond(
        200,
        state({
          alerts: [
            {
              ...standup,
              acknowledged: true,
              acknowledgedAt: "2026-10-28T12:47:00+00:00",
              acknowledgedVia: "phone",
            },
            {
              ...review,
              acknowledged: true,
              acknowledgedAt: "2026-10-28T17:52:00+00:00",
              acknowledgedVia: "browser",
            },
          ],
        }),
      ),
    );
    await settle();

    const list = screen.getByRole("list", { name: "Next alerts" });
    expect(
      within(list).getByText("Acknowledged on phone at 08:47"),
    ).toBeTruthy();
    expect(
      within(list).getByText("Acknowledged in a browser at 13:52"),
    ).toBeTruthy();
  });

  it("says nothing for one that is not", async () => {
    mount(respond(200, state()));
    await settle();

    expect(screen.queryByText(/^Acknowledged /)).toBeNull();
  });
});

describe("routine alarms", () => {
  const wake = {
    id: "5f0c1d7e-8a1b-4c2d-9e3f-0a1b2c3d4e5f",
    label: "Wake up",
    hour: 6,
    minute: 30,
    days: [1, 2, 3, 4, 5],
    enabled: true,
    snoozeMinutes: 9,
    updatedAt: "2026-10-28T12:00:00+00:00",
  };

  it("lists the status's routines, and the switch puts the change and shows what the server stored", async () => {
    mount(
      respond(200, state({ routines: [wake] })),
      respond(200, state({ routines: [{ ...wake, enabled: false }] })),
    );
    await settle();

    const list = screen.getByRole("list", { name: "Routine alarms" });
    expect(within(list).getByText("06:30")).toBeTruthy();
    expect(within(list).getByText("Weekdays, Wake up")).toBeTruthy();

    await act(async () => {
      fireEvent.click(screen.getByRole("switch", { name: "Wake up at 06:30" }));
    });
    await settle();

    const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(put?.[0]).toBe(`/api/alerts/routines/${wake.id}`);
    expect(JSON.parse(put?.[1].body as string)).toEqual({
      label: "Wake up",
      hour: 6,
      minute: 30,
      days: [1, 2, 3, 4, 5],
      enabled: false,
      snoozeMinutes: 9,
    });
    expect(
      (
        screen.getByRole("switch", {
          name: "Wake up at 06:30",
        }) as HTMLInputElement
      ).checked,
    ).toBe(false);
  });

  it("says there are none when the status has none", async () => {
    mount(respond(200, state({ routines: [] })));
    await settle();

    expect(screen.getByText("No routine alarms.")).toBeTruthy();
    expect(
      screen.getByRole("heading", { name: "Routine alarms" }),
    ).toBeTruthy();
  });
});
