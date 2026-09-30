// @vitest-environment jsdom
/**
 * Routine alarms from the section's controls: each routine's time, days
 * and label, the on and off switch, Edit, Delete that asks first, and the
 * add form with its time, seven day toggles, label and snooze.
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
import type { AlertsState } from "../../core/api";
import type { Routine, RoutineResult } from "../../core/routines";
import RoutinesSection, { type RoutinesApi } from "../RoutinesSection";

const wake: Routine = {
  id: "5f0c1d7e-8a1b-4c2d-9e3f-0a1b2c3d4e5f",
  label: "Wake up",
  hour: 6,
  minute: 30,
  days: [1, 2, 3, 4, 5],
  enabled: true,
  snoozeMinutes: 9,
  updatedAt: "2026-10-28T12:00:00+00:00",
};

const gym: Routine = {
  ...wake,
  id: "6a1d2e8f-9b2c-4d3e-8f40-1b2c3d4e5f60",
  label: "Gym",
  hour: 5,
  minute: 0,
  days: [1, 3, 5],
};

const state = (routines: Routine[]): AlertsState => ({
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
  routines,
});

const answer = (routines: Routine[]): RoutineResult => ({
  ok: true,
  state: state(routines),
});

function fakeApi(over: Partial<RoutinesApi> = {}) {
  return {
    create: vi.fn(async () => answer([wake])),
    update: vi.fn(async () => answer([wake])),
    remove: vi.fn(async () => answer([])),
    ...over,
  };
}

function mount(routines: Routine[], api = fakeApi()) {
  const onState = vi.fn();
  render(<RoutinesSection routines={routines} onState={onState} api={api} />);
  return { api, onState };
}

const press = async (element: HTMLElement) => {
  await act(async () => {
    fireEvent.click(element);
  });
};

beforeAll(() => {
  // The first MUI mount in a worker pays for the theme.
  mount([wake]);
  cleanup();
});

afterEach(() => cleanup());

describe("the list", () => {
  it("shows each routine's time, days and label, and says where they ring", () => {
    mount([gym, wake, { ...wake, id: "x", days: [], label: "Alarm" }]);

    const list = screen.getByRole("list", { name: "Routine alarms" });
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(3);
    expect(within(items[0]).getByText("05:00")).toBeTruthy();
    expect(within(items[0]).getByText("Mon Wed Fri, Gym")).toBeTruthy();
    expect(within(items[1]).getByText("06:30")).toBeTruthy();
    expect(within(items[1]).getByText("Weekdays, Wake up")).toBeTruthy();
    expect(within(items[2]).getByText("Once, Alarm")).toBeTruthy();
    expect(
      screen.getByText(
        "Routine alarms ring on the paired phone like Clock alarms. They do not go through Pushover or ring in this browser.",
      ),
    ).toBeTruthy();
  });

  it.each([
    [[1, 2, 3, 4, 5, 6, 7], "Every day"],
    [[6, 7], "Weekends"],
  ])("%j reads %s", (days, summary) => {
    mount([{ ...wake, days }]);
    expect(screen.getByText(`${summary}, Wake up`)).toBeTruthy();
  });

  it("says when there are none", () => {
    mount([]);
    expect(screen.getByText("No routine alarms.")).toBeTruthy();
  });
});

describe("the switch", () => {
  it("puts the whole routine with enabled false, and hands the state up", async () => {
    const { api, onState } = mount([wake]);
    const toggle = screen.getByRole("switch", { name: "Wake up at 06:30" });
    expect((toggle as HTMLInputElement).checked).toBe(true);

    await press(toggle);

    expect(api.update).toHaveBeenCalledExactlyOnceWith(wake.id, {
      label: "Wake up",
      hour: 6,
      minute: 30,
      days: [1, 2, 3, 4, 5],
      enabled: false,
      snoozeMinutes: 9,
    });
    expect(onState).toHaveBeenCalledWith(state([wake]));
    expect(screen.getByText("Wake up at 06:30 is off.")).toBeTruthy();
  });
});

describe("delete", () => {
  it("asks first, and Cancel sends nothing", async () => {
    const { api } = mount([wake]);

    await press(
      screen.getByRole("button", { name: "Delete Wake up at 06:30" }),
    );
    await press(screen.getByRole("button", { name: "Cancel" }));

    expect(api.remove).not.toHaveBeenCalled();
    expect(
      screen.getByRole("button", { name: "Delete Wake up at 06:30" }),
    ).toBeTruthy();
  });

  it("Yes, delete removes it", async () => {
    const { api, onState } = mount([wake]);

    await press(
      screen.getByRole("button", { name: "Delete Wake up at 06:30" }),
    );
    await press(
      screen.getByRole("button", { name: "Yes, delete Wake up at 06:30" }),
    );

    expect(api.remove).toHaveBeenCalledExactlyOnceWith(wake.id);
    expect(onState).toHaveBeenCalledWith(state([]));
  });

  it("an id the server no longer has says so", async () => {
    mount(
      [wake],
      fakeApi({
        remove: vi.fn(async () => ({
          ok: false as const,
          reason: "missing" as const,
        })),
      }),
    );

    await press(
      screen.getByRole("button", { name: "Delete Wake up at 06:30" }),
    );
    await press(
      screen.getByRole("button", { name: "Yes, delete Wake up at 06:30" }),
    );

    expect(
      screen.getByText(
        "That routine alarm is gone. The list below is current.",
      ),
    ).toBeTruthy();
  });
});

describe("the add form", () => {
  it("sends the time, the days sorted, the trimmed label and the snooze", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    fireEvent.change(within(form).getByLabelText(/^Time/), {
      target: { value: "06:30" },
    });
    await press(within(form).getByRole("button", { name: "Repeat on Friday" }));
    await press(within(form).getByRole("button", { name: "Repeat on Monday" }));
    await press(
      within(form).getByRole("button", { name: "Repeat on Wednesday" }),
    );
    expect(
      within(form)
        .getByRole("button", { name: "Repeat on Monday" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(within(form).getByText("Repeats: Mon Wed Fri.")).toBeTruthy();
    fireEvent.change(within(form).getByLabelText("Label"), {
      target: { value: "  Gym  " },
    });
    fireEvent.change(within(form).getByLabelText("Snooze in minutes"), {
      target: { value: "5" },
    });
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(api.create).toHaveBeenCalledExactlyOnceWith({
      label: "Gym",
      hour: 6,
      minute: 30,
      days: [1, 3, 5],
      enabled: true,
      snoozeMinutes: 5,
    });
    expect(screen.getByText("A routine alarm at 06:30 is added.")).toBeTruthy();
  });

  it("a day pressed twice is off again, and no day rings once", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Repeat on Sunday" }));
    await press(within(form).getByRole("button", { name: "Repeat on Sunday" }));
    expect(
      within(form).getByText(
        "No day chosen: it rings once, at the next time shown.",
      ),
    ).toBeTruthy();
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({ days: [], label: "", snoozeMinutes: 9 }),
    );
  });

  it("snooze is a typed number with the server's bounds", () => {
    mount([]);
    const snooze = screen.getByLabelText(
      "Snooze in minutes",
    ) as HTMLInputElement;
    expect(snooze.type).toBe("number");
    expect(snooze.min).toBe("1");
    expect(snooze.max).toBe("30");
    expect(screen.getByText("1 to 30.")).toBeTruthy();
  });

  it("shows the server's message under the field it refused", async () => {
    mount(
      [],
      fakeApi({
        create: vi.fn(async () => ({
          ok: false as const,
          reason: "invalid" as const,
          errors: { snoozeMinutes: ["1 to 30."], label: ["Too long."] },
        })),
      }),
    );

    await press(screen.getByRole("button", { name: "Add routine alarm" }));

    expect(screen.getByText("Too long.")).toBeTruthy();
    expect(screen.getByText("Check the fields marked below.")).toBeTruthy();
  });

  it("the 51st says the server's limit", async () => {
    mount(
      [],
      fakeApi({
        create: vi.fn(async () => ({
          ok: false as const,
          reason: "full" as const,
          detail: "At most 50 routine alarms. Delete one first.",
        })),
      }),
    );

    await press(screen.getByRole("button", { name: "Add routine alarm" }));

    expect(
      screen.getByText("At most 50 routine alarms. Delete one first."),
    ).toBeTruthy();
  });
});

describe("edit", () => {
  it("fills the form with the routine and saves the whole body under its id", async () => {
    const { api } = mount([{ ...wake, enabled: false }]);

    await press(screen.getByRole("button", { name: "Edit Wake up at 06:30" }));
    const form = screen.getByRole("form", { name: "Edit routine alarm" });
    expect(
      (within(form).getByLabelText(/^Time/) as HTMLInputElement).value,
    ).toBe("06:30");
    expect(
      (within(form).getByLabelText("Label") as HTMLInputElement).value,
    ).toBe("Wake up");
    expect(
      within(form)
        .getByRole("button", { name: "Repeat on Tuesday" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(
      within(form)
        .getByRole("button", { name: "Repeat on Saturday" })
        .getAttribute("aria-pressed"),
    ).toBe("false");

    fireEvent.change(within(form).getByLabelText(/^Time/), {
      target: { value: "07:15" },
    });
    await press(
      within(form).getByRole("button", { name: "Repeat on Saturday" }),
    );
    await press(
      within(form).getByRole("button", { name: "Save routine alarm" }),
    );

    expect(api.update).toHaveBeenCalledExactlyOnceWith(wake.id, {
      label: "Wake up",
      hour: 7,
      minute: 15,
      days: [1, 2, 3, 4, 5, 6],
      enabled: false,
      snoozeMinutes: 9,
    });
    expect(
      screen.getByRole("form", { name: "Add routine alarm" }),
    ).toBeTruthy();
  });

  it("Cancel edit goes back to adding", async () => {
    const { api } = mount([wake]);

    await press(screen.getByRole("button", { name: "Edit Wake up at 06:30" }));
    await press(screen.getByRole("button", { name: "Cancel edit" }));

    expect(
      screen.getByRole("form", { name: "Add routine alarm" }),
    ).toBeTruthy();
    expect(api.update).not.toHaveBeenCalled();
  });
});
