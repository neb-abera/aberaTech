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
};

const state = (over: Record<string, unknown> = {}) => ({
  configured: true,
  timeZone: "America/New_York",
  pollMinutes: 5,
  defaultLeadMinutes: 10,
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
    expect(screen.getByText(/every 5 minutes/)).toBeTruthy();

    const list = screen.getByRole("list", { name: "Next alerts" });
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain("Standup");
    expect(items[0].textContent).toContain("Alert Wed, Oct 28, 8:45 AM EDT");
    expect(items[0].textContent).toContain("starts Wed, Oct 28, 9:00 AM EDT");
    expect(items[0].textContent).toContain("Room 1");
    expect(items[0].textContent).toContain("the event's reminder");
    expect(items[1].textContent).toContain("10 minutes before, the default");
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
    expect(screen.getByText(/Test alert sent/)).toBeTruthy();

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

  it("says when the last calendar read failed and which list is shown", async () => {
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

    expect(
      screen.getByText(
        /The last read, Wed, Oct 28, 8:05 AM EDT, failed: HTTP 404. The list is from Wed, Oct 28, 8:00 AM EDT./,
      ),
    ).toBeTruthy();
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
