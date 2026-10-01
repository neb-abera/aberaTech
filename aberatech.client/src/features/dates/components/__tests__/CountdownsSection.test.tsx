// @vitest-environment jsdom
/**
 * Countdowns from the section's controls: each clock and target, Edit,
 * Delete that asks first, and the form with its label, date, time and
 * zone.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import type { AlertsState, StateChangeResult } from "../../../alerts/core/api";
import type { Countdown } from "../../../alerts/core/countdowns";
import CountdownsSection, { type CountdownsApi } from "../CountdownsSection";

const now = Date.parse("2026-10-01T06:47:27Z");

const home: Countdown = {
  id: "3d2c1b0a-9f8e-4d7c-8b6a-5f4e3d2c1b0a",
  label: "Home",
  targetAt: "2026-11-11T12:00:00+00:00",
  timeZone: "Asia/Amman",
  updatedAt: "2026-10-01T06:00:00+00:00",
};

const deployed: Countdown = {
  ...home,
  id: "4e3d2c1b-0a9f-4e8d-9c7b-6a5f4e3d2c1b",
  label: "Deployed",
  targetAt: "2026-06-01T00:00:00+00:00",
  timeZone: "UTC",
};

const state = (countdowns: Countdown[]): AlertsState => ({
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
  countdowns,
});

const answer = (countdowns: Countdown[]): StateChangeResult => ({
  ok: true,
  state: state(countdowns),
});

function fakeApi(over: Partial<CountdownsApi> = {}) {
  return {
    create: vi.fn(async () => answer([home])),
    update: vi.fn(async () => answer([home])),
    remove: vi.fn(async () => answer([])),
    ...over,
  };
}

function mount(countdowns: Countdown[], api = fakeApi(), at: number = now) {
  const onState = vi.fn();
  render(
    <CountdownsSection
      countdowns={countdowns}
      onState={onState}
      api={api}
      now={at}
    />,
  );
  return { api, onState };
}

const press = async (element: HTMLElement) => {
  await act(async () => {
    fireEvent.click(element);
  });
};

/** A field by its label, with or without the required mark. */
const field = (label: string) =>
  screen.getByLabelText(new RegExp(`^${label}\\s*\\*?$`), {
    selector: "input",
  });

const type = (label: string, value: string) =>
  fireEvent.change(field(label), { target: { value } });

beforeAll(() => {
  mount([home]);
  cleanup();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("the list", () => {
  it("shows the time left, the time since, and each target in its own zone", () => {
    mount([deployed, home]);

    const items = within(
      screen.getByRole("list", { name: "Countdowns" }),
    ).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText("122 days 06:47:27 ago")).toBeTruthy();
    expect(within(items[1]).getByText("41 days 05:12:33 left")).toBeTruthy();
    expect(
      within(items[1]).getByText(
        "Wed, Nov 11, 2026, 3:00 PM GMT+3 (Asia/Amman)",
      ),
    ).toBeTruthy();
  });

  it("ticks once a second", async () => {
    vi.useFakeTimers({ now });
    render(
      <CountdownsSection
        countdowns={[home]}
        onState={vi.fn()}
        api={fakeApi()}
      />,
    );
    expect(screen.getByText("41 days 05:12:33 left")).toBeTruthy();

    await act(async () => {
      vi.advanceTimersByTime(3000);
    });

    expect(screen.getByText("41 days 05:12:30 left")).toBeTruthy();
  });

  it("says when there are none", () => {
    mount([]);
    expect(screen.getByText("No countdowns.")).toBeTruthy();
  });
});

describe("the form", () => {
  it("adds a countdown at a date and time in the chosen zone", async () => {
    const { api, onState } = mount([]);

    type("Label", "  Home ");
    type("Date", "2027-03-01");
    type("Time", "09:00");
    type("Time zone", "Asia/Amman");
    await press(screen.getByRole("button", { name: "Add countdown" }));

    expect(api.create).toHaveBeenCalledExactlyOnceWith({
      label: "Home",
      targetAt: "2027-03-01T09:00:00+03:00",
      timeZone: "Asia/Amman",
    });
    expect(onState).toHaveBeenCalledWith(state([home]));
    expect(screen.getByText("Home is added.")).toBeTruthy();
  });

  it("starts on this computer's zone and midnight", () => {
    mount([]);
    expect((field("Time zone") as HTMLInputElement).value).toBe(
      Intl.DateTimeFormat().resolvedOptions().timeZone,
    );
    expect((field("Time") as HTMLInputElement).value).toBe("00:00");
  });

  it("refuses an unknown zone and a missing date before asking the server", async () => {
    const { api } = mount([]);

    type("Date", "2027-03-01");
    type("Time zone", "Mars/Olympus");
    await press(screen.getByRole("button", { name: "Add countdown" }));
    expect(screen.getByText("A time zone such as Asia/Amman.")).toBeTruthy();

    type("Time zone", "UTC");
    type("Date", "");
    await press(screen.getByRole("button", { name: "Add countdown" }));
    expect(screen.getByText("A date and a time.")).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("edits with the saved date, time and zone filled in, and puts the whole body", async () => {
    const { api } = mount([home]);

    await press(screen.getByRole("button", { name: "Edit Home" }));
    const form = screen.getByRole("form", { name: "Edit countdown" });
    expect(within(form).getByDisplayValue("2026-11-11")).toBe(field("Date"));
    expect(within(form).getByDisplayValue("Asia/Amman")).toBe(
      field("Time zone"),
    );
    type("Label", "Leave");
    await press(screen.getByRole("button", { name: "Save countdown" }));

    expect(api.update).toHaveBeenCalledExactlyOnceWith(home.id, {
      label: "Leave",
      targetAt: "2026-11-11T15:00:00+03:00",
      timeZone: "Asia/Amman",
    });
    expect(screen.getByRole("form", { name: "Add countdown" })).toBeTruthy();
  });

  it("shows the server's refusal and marks the field", async () => {
    mount(
      [],
      fakeApi({
        create: vi.fn(async () => ({
          ok: false as const,
          reason: "invalid" as const,
          errors: { label: ["At most 60 characters, no control characters."] },
        })),
      }),
    );
    type("Date", "2027-03-01");
    await press(screen.getByRole("button", { name: "Add countdown" }));

    expect(screen.getByText("Check the fields marked below.")).toBeTruthy();
    expect(
      screen.getByText("At most 60 characters, no control characters."),
    ).toBeTruthy();
  });

  it.each([
    [
      { ok: false as const, reason: "full" as const },
      "At most 50 countdowns. Delete one first.",
    ],
    [
      { ok: false as const, reason: "network" as const },
      "No answer from the server. Check the connection and try again.",
    ],
    [
      { ok: false as const, reason: "throttled" as const },
      "Too many presses. Wait a minute.",
    ],
  ])("explains %j", async (result, message) => {
    mount([], fakeApi({ create: vi.fn(async () => result) }));
    type("Date", "2027-03-01");
    await press(screen.getByRole("button", { name: "Add countdown" }));
    expect(screen.getByText(message)).toBeTruthy();
  });
});

describe("delete", () => {
  it("asks first, then deletes", async () => {
    const { api, onState } = mount([home]);

    await press(screen.getByRole("button", { name: "Delete Home" }));
    expect(api.remove).not.toHaveBeenCalled();
    await press(screen.getByRole("button", { name: "Yes, delete Home" }));

    expect(api.remove).toHaveBeenCalledExactlyOnceWith(home.id);
    expect(onState).toHaveBeenCalledWith(state([]));
  });

  it("Cancel keeps it", async () => {
    const { api } = mount([home]);

    await press(screen.getByRole("button", { name: "Delete Home" }));
    await press(screen.getByRole("button", { name: "Cancel" }));

    expect(api.remove).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Delete Home" })).toBeTruthy();
  });
});
