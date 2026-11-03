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

function mount(
  countdowns: Countdown[],
  api = fakeApi(),
  at: number = now,
  defaultZone?: string,
) {
  const onState = vi.fn();
  render(
    <CountdownsSection
      countdowns={countdowns}
      onState={onState}
      api={api}
      now={at}
      defaultZone={defaultZone}
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
    type("Time zone", "asia/amman");
    await press(screen.getByRole("button", { name: "Add countdown" }));

    expect(api.create).toHaveBeenCalledExactlyOnceWith({
      label: "Home",
      targetAt: "2027-03-01T09:00:00+03:00",
      timeZone: "Asia/Amman",
      alarm: false,
      repeat: "none",
      repeatEvery: 1,
    });
    expect(onState).toHaveBeenCalledWith(state([home]));
    expect(screen.getByText("Home is added.")).toBeTruthy();
  });

  it("sends the alarm when its switch is on", async () => {
    const { api } = mount([]);

    type("Date", "2027-03-01");
    await press(screen.getByRole("switch", { name: "Alarm at this time" }));
    await press(screen.getByRole("button", { name: "Add countdown" }));

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({ alarm: true }),
    );
  });

  it("starts on the phone's zone and midnight, and on New York without one", () => {
    mount([], fakeApi(), now, "Asia/Amman");
    expect((field("Time zone") as HTMLInputElement).value).toBe("Asia/Amman");
    expect((field("Time") as HTMLInputElement).value).toBe("00:00");
    cleanup();

    mount([]);
    expect((field("Time zone") as HTMLInputElement).value).toBe(
      "America/New_York",
    );
  });

  it("suggests the United States zones first", () => {
    mount([]);
    const options = [
      ...document.querySelectorAll("#countdown-zones option"),
    ].map((option) => option.getAttribute("value"));
    expect(options.slice(0, 3)).toEqual([
      "America/New_York",
      "America/Chicago",
      "America/Denver",
    ]);
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
      alarm: false,
      repeat: "none",
      repeatEvery: 1,
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
    [
      {
        ok: false as const,
        reason: "full" as const,
        detail: "Google Calendar is not connected with edit access.",
      },
      "Google Calendar is not connected with edit access.",
    ],
    [
      {
        ok: false as const,
        reason: "unavailable" as const,
        detail: "Google Calendar did not answer.",
      },
      "Google Calendar did not answer.",
    ],
  ])("explains %j", async (result, message) => {
    mount([], fakeApi({ create: vi.fn(async () => result) }));
    type("Date", "2027-03-01");
    await press(screen.getByRole("button", { name: "Add countdown" }));
    expect(screen.getByText(message)).toBeTruthy();
  });
});

describe("the calendar and the alarm", () => {
  it("marks a countdown with an alarm and one not yet on the calendar", () => {
    mount([
      { ...home, alarm: true, onCalendar: true },
      { ...deployed, alarm: false, onCalendar: false },
    ]);

    const homeItem = screen.getByRole("listitem", { name: "Home" });
    expect(within(homeItem).getByTestId("countdown-alarm").textContent).toBe(
      "Alarm at this time",
    );
    expect(within(homeItem).queryByTestId("countdown-off-calendar")).toBeNull();
    const deployedItem = screen.getByRole("listitem", { name: "Deployed" });
    expect(within(deployedItem).queryByTestId("countdown-alarm")).toBeNull();
    expect(
      within(deployedItem).getByTestId("countdown-off-calendar"),
    ).toBeTruthy();
  });

  it("opens Edit with the saved alarm and sends it back", async () => {
    const { api } = mount([{ ...home, alarm: true }]);

    await press(screen.getByRole("button", { name: "Edit Home" }));
    expect(
      (
        screen.getByRole("switch", {
          name: "Alarm at this time",
        }) as HTMLInputElement
      ).checked,
    ).toBe(true);
    await press(screen.getByRole("button", { name: "Save countdown" }));

    expect(api.update).toHaveBeenCalledWith(
      home.id,
      expect.objectContaining({ alarm: true }),
    );
  });
});

describe("repeats", () => {
  const chooseUnit = async (name: string) => {
    // MUI's select opens on the press of the button, not the click.
    await act(async () => {
      fireEvent.mouseDown(screen.getByRole("combobox", { name: "Unit" }));
    });
    await press(screen.getByRole("option", { name }));
  };

  it("sends every so many of a unit and lists it", async () => {
    const { api } = mount([{ ...home, repeat: "weekly", repeatEvery: 2 }]);

    expect(
      within(screen.getByRole("listitem", { name: "Home" })).getByTestId(
        "countdown-repeat",
      ).textContent,
    ).toBe("Every 2 weeks");
    expect((field("Repeats every") as HTMLInputElement).disabled).toBe(true);
    type("Date", "2027-03-01");
    await chooseUnit("hour");
    type("Repeats every", "2");
    await press(screen.getByRole("button", { name: "Add countdown" }));

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({ repeat: "hourly", repeatEvery: 2 }),
    );
  });

  it("names the units in the plural for more than one", async () => {
    mount([]);
    await chooseUnit("year");
    type("Repeats every", "3");
    await act(async () => {
      fireEvent.mouseDown(screen.getByRole("combobox", { name: "Unit" }));
    });
    expect(screen.getByRole("option", { name: "years" })).toBeTruthy();
  });

  it("refuses an interval that is not a whole number from 1 to 999", async () => {
    const { api } = mount([]);
    type("Date", "2027-03-01");
    await chooseUnit("day");
    type("Repeats every", "0");
    await press(screen.getByRole("button", { name: "Add countdown" }));

    expect(screen.getByText("A whole number, 1 to 999.")).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("opens Edit on the saved unit and interval", async () => {
    const { api } = mount([{ ...home, repeat: "monthly", repeatEvery: 6 }]);

    await press(screen.getByRole("button", { name: "Edit Home" }));
    expect((field("Repeats every") as HTMLInputElement).value).toBe("6");
    await press(screen.getByRole("button", { name: "Save countdown" }));

    expect(api.update).toHaveBeenCalledWith(
      home.id,
      expect.objectContaining({ repeat: "monthly", repeatEvery: 6 }),
    );
  });
});

describe("a zone this browser does not know", () => {
  it("lists the countdown and opens Edit in UTC", async () => {
    mount([{ ...home, timeZone: "Mars/Olympus" }]);

    await press(screen.getByRole("button", { name: "Edit Home" }));

    const form = screen.getByRole("form", { name: "Edit countdown" });
    expect(within(form).getByDisplayValue("2026-11-11")).toBe(field("Date"));
    expect(within(form).getByDisplayValue("12:00")).toBe(field("Time"));
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
