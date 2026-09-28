// @vitest-environment jsdom
/**
 * Ring in this browser, from the switch and the Acknowledge button: off by
 * default, remembered per browser, a due alarm rings with a tone, the
 * title and a notification, and stops once it is acknowledged anywhere,
 * skipped, muted or started.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import {
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import type { ActionResult, AlertItem, AlertsState } from "../../core/api";
import { ringPreferenceKey, type Tone } from "../../core/ring";
import RingInBrowser from "../RingInBrowser";

/** 08:50 New York on the standup's morning: its alarm went at 08:45, it starts at 09:00. */
const now = Date.parse("2026-10-28T12:50:00Z");

const standup: AlertItem = {
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

const state = (alerts: AlertItem[]): AlertsState => ({
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
  alerts,
});

function fakeTone() {
  const tone = {
    unlocked: 0,
    playing: false,
    starts: 0,
    unlock: vi.fn(() => {
      tone.unlocked++;
    }),
    start: vi.fn(() => {
      tone.playing = true;
      tone.starts++;
    }),
    stop: vi.fn(() => {
      tone.playing = false;
    }),
  };
  return tone;
}

let clock = now;
let tone: ReturnType<typeof fakeTone>;
let notifications: { title: string; tag?: string }[];
let requested: number;

class FakeNotification {
  static permission: NotificationPermission = "default";
  static requestPermission = vi.fn(async () => {
    requested++;
    FakeNotification.permission = "granted";
    return "granted" as NotificationPermission;
  });
  constructor(title: string, options?: NotificationOptions) {
    notifications.push({ title, tag: options?.tag });
  }
}

function mount(
  alerts: AlertItem[],
  acknowledge: (
    key: string,
    via: "browser",
  ) => Promise<ActionResult> = async () => ({
    ok: true,
  }),
) {
  const onState = vi.fn();
  const onEnabledChange = vi.fn();
  const view = render(
    <RingInBrowser
      state={state(alerts)}
      onState={onState}
      onEnabledChange={onEnabledChange}
      now={() => clock}
      makeTone={() => tone as unknown as Tone}
      acknowledge={acknowledge}
    />,
  );
  const rerender = (next: AlertItem[]) =>
    view.rerender(
      <RingInBrowser
        state={state(next)}
        onState={onState}
        onEnabledChange={onEnabledChange}
        now={() => clock}
        makeTone={() => tone as unknown as Tone}
        acknowledge={acknowledge}
      />,
    );
  return { onState, onEnabledChange, rerender };
}

const switchOn = () =>
  fireEvent.click(screen.getByRole("switch", { name: "Ring in this browser" }));

beforeAll(() => {
  // The first MUI mount in a worker pays the theme's setup. Paid once here.
  render(
    <RingInBrowser
      state={state([])}
      onState={() => undefined}
      onEnabledChange={() => undefined}
    />,
  );
  cleanup();
});

beforeEach(() => {
  vi.useFakeTimers();
  clock = now;
  tone = fakeTone();
  notifications = [];
  requested = 0;
  FakeNotification.permission = "default";
  vi.stubGlobal("Notification", FakeNotification);
  window.localStorage.clear();
  document.title = "Alerts";
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("the switch", () => {
  it("is off by default, and a due alarm does not ring", () => {
    const { onEnabledChange } = mount([standup]);

    expect(
      (
        screen.getByRole("switch", {
          name: "Ring in this browser",
        }) as HTMLInputElement
      ).checked,
    ).toBe(false);
    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(tone.start).not.toHaveBeenCalled();
    expect(onEnabledChange).toHaveBeenLastCalledWith(false);
    expect(screen.getByText(/Rings only while this tab is open/)).toBeTruthy();
  });

  it("unlocks audio and asks to notify on the click, and is remembered in this browser", () => {
    const { onEnabledChange } = mount([]);

    switchOn();

    expect(tone.unlock).toHaveBeenCalledTimes(1);
    expect(requested).toBe(1);
    expect(window.localStorage.getItem(ringPreferenceKey)).toBe("on");
    expect(onEnabledChange).toHaveBeenLastCalledWith(true);

    cleanup();
    mount([]);
    expect(
      (
        screen.getByRole("switch", {
          name: "Ring in this browser",
        }) as HTMLInputElement
      ).checked,
    ).toBe(true);

    switchOn();
    expect(window.localStorage.getItem(ringPreferenceKey)).toBeNull();
  });

  it("works when storage throws", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });

    mount([standup]);
    switchOn();

    expect(
      screen.getByRole("alertdialog", { name: "Ringing: Standup" }),
    ).toBeTruthy();
    vi.restoreAllMocks();
  });
});

describe("a due alarm", () => {
  it("rings with the tone, the title and one notification", () => {
    FakeNotification.permission = "granted";
    mount([standup]);
    switchOn();

    expect(
      screen.getByRole("alertdialog", { name: "Ringing: Standup" }),
    ).toBeTruthy();
    expect(tone.playing).toBe(true);
    expect(document.title).toBe("Alarm: Standup");
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(document.title).toBe("Alerts");
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(document.title).toBe("Alarm: Standup");
    expect(notifications).toEqual([
      { title: "Alarm: Standup", tag: standup.key },
    ]);
    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(notifications).toHaveLength(1);
    expect(tone.starts).toBe(1);
  });

  it("starts ringing the second its time comes, between answers from the server", () => {
    clock = Date.parse("2026-10-28T12:44:58Z");
    mount([standup]);
    switchOn();
    expect(screen.queryByRole("alertdialog")).toBeNull();

    clock = Date.parse("2026-10-28T12:45:00Z");
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(
      screen.getByRole("alertdialog", { name: "Ringing: Standup" }),
    ).toBeTruthy();
  });

  it("stops when its event starts, and the title comes back", () => {
    mount([standup]);
    switchOn();

    clock = Date.parse("2026-10-28T13:00:00Z");
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(tone.playing).toBe(false);
    expect(document.title).toBe("Alerts");
  });

  it.each([
    [
      "acknowledged on the phone",
      { acknowledged: true, acknowledgedVia: "phone" as const },
    ],
    ["skipped", { skipped: true }],
    ["muted", { muted: true }],
  ])("stops once the server says it was %s", (_, change) => {
    const { rerender } = mount([standup]);
    switchOn();
    expect(tone.playing).toBe(true);

    rerender([{ ...standup, ...change }]);

    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(tone.playing).toBe(false);
  });

  it.each([
    ["a notification", { type: "notification" as const }],
    ["sends nothing", { type: "none" as const }],
    ["not due yet", { alertAt: "2026-10-28T12:55:00+00:00" }],
  ])("does not ring for an event that is %s", (_, change) => {
    mount([{ ...standup, ...change }]);
    switchOn();

    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(tone.start).not.toHaveBeenCalled();
  });

  it("Acknowledge posts once as the browser and shows the state the server answered", async () => {
    const answered = state([
      { ...standup, acknowledged: true, acknowledgedVia: "browser" },
    ]);
    const acknowledge = vi.fn(async () => ({
      ok: true as const,
      state: answered,
    }));
    const { onState } = mount([standup], acknowledge);
    switchOn();

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Acknowledge" }));
    });

    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(standup.key, "browser");
    expect(onState).toHaveBeenCalledWith(answered);
  });

  it("an acknowledgement the server refuses says so and keeps ringing", async () => {
    mount([standup], async () => ({ ok: false, reason: "throttled" }));
    switchOn();

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Acknowledge" }));
    });

    expect(screen.getByText("Too many presses. Wait a minute.")).toBeTruthy();
    expect(tone.playing).toBe(true);
  });

  it("sends no notification when the owner did not allow one", () => {
    FakeNotification.requestPermission.mockImplementationOnce(async () => {
      requested++;
      FakeNotification.permission = "denied";
      return "denied" as NotificationPermission;
    });
    FakeNotification.permission = "default";
    mount([standup]);

    switchOn();

    expect(notifications).toEqual([]);
    expect(tone.playing).toBe(true);
  });
});
