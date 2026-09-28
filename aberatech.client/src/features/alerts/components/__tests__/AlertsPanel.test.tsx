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
  render(<AlertsPanel />);
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
      /again every minute until you acknowledge it in the Pushover app, for up to 50 minutes/,
    );
    expect(paragraph.textContent).toContain(
      "An alarm is one Pushover message.",
    );
    expect(paragraph.textContent).toContain(
      "A notification is one message with one sound, as the phone's Pushover settings allow.",
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
        /A notification is one message with one sound, through Pushover's quiet hours\. An event with no mark and no type set here sends a notification\./,
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
      "Alarm: from #critical in the calendar. The alarm settings apply.",
    );
    expect(quiet.textContent).toContain(
      "Sends nothing: the default for unmarked events. Send test is off until you choose Notification or Alarm.",
    );
    const at = "Review at Wed, Oct 28, 2:00 PM EDT";
    expect(
      screen
        .getByRole("button", { name: `Set ${at} to None` })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(
      screen
        .getByRole("button", { name: `Set ${at} to Alarm` })
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
      screen.getByRole("button", { name: `Set ${at} to Notification` }),
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
      screen.getByText("Review is set to Notification, every occurrence."),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "Notification: set here. The notification settings apply.",
      ),
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

  it("sends a test notification and says so", async () => {
    mount(respond(200, state()), respond(200, { sent: true }));
    await settle();

    fireEvent.click(
      screen.getByRole("button", { name: "Send test notification" }),
    );
    await settle();

    expect(posts()).toEqual([["/api/alerts/test-notification", undefined]]);
    expect(
      screen.getByText(
        "Test notification sent. Check the phone for one sound.",
      ),
    ).toBeTruthy();
  });

  it("describes what the saved settings do, not a fixed priority", async () => {
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
      screen.getByText(/again every 2 minutes .* for up to 30 minutes/),
    ).toBeTruthy();

    cleanup();
    mount(respond(200, state({ settings: { ...settings, priority: 0 } })));
    await settle();
    expect(
      screen.getByText(/with one sound\. The phone's Pushover/),
    ).toBeTruthy();
    expect(
      screen.queryByText(/until you acknowledge it in the Pushover app/),
    ).toBeNull();
  });

  it("saves the settings, then shows what the server stored", async () => {
    mount(
      respond(200, state()),
      respond(
        200,
        state({
          settings: { ...settings, priority: 1 },
        }),
      ),
    );
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "High" }));
    fireEvent.click(screen.getByRole("button", { name: "Save settings" }));
    await settle();

    const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(put?.[0]).toBe("/api/alerts/settings");
    expect(JSON.parse(put?.[1].body)).toEqual({ ...settings, priority: 1 });
    expect(screen.getByText("Settings saved.")).toBeTruthy();
    expect(
      screen.getByText(/plays through Pushover's quiet hours/),
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

    fireEvent.click(screen.getByRole("button", { name: "Send test alert" }));
    await settle();
    expect(posts()[0]).toEqual(["/api/alerts/test", undefined]);
    expect(screen.getByText(/Test alert sent as an alarm/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Send test alert" }));
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
        'Test of Standup sent, titled "Test: Standup", as an alarm.',
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
      name: "How events become alarms",
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
      "None: nothing is sent. The event is still listed above.",
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
      screen.getByRole("region", { name: "How events become alarms" })
        .textContent ?? "";
    expect(on).toContain("in the next 1 hour is planned");
    expect(on).toContain("Every other event gets one notification");
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
