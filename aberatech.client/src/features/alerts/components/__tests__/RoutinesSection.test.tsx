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
import {
  type Routine,
  type RoutineResult,
  startDay,
  todayDay,
} from "../../core/routines";
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

/** Every 97 minutes from Wednesday 25 November 2026 at 06:30. */
const meds: Routine = {
  ...wake,
  id: "7b2e3f90-ac3d-4e4f-9051-2c3d4e5f6071",
  label: "Meds",
  days: [],
  repeat: "interval",
  every: 97,
  unit: "minutes",
  startsOn: "2026-11-25",
};

/** Monday, Wednesday and Friday of every other week. */
const fortnight: Routine = {
  ...gym,
  repeat: "weekdays",
  every: 2,
  unit: "weeks",
  startsOn: "2026-11-25",
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

const storedAs = (routines: Routine[]): RoutineResult => ({
  ok: true,
  state: state(routines),
});

function fakeApi(over: Partial<RoutinesApi> = {}) {
  return {
    create: vi.fn(async () => storedAs([wake])),
    update: vi.fn(async () => storedAs([wake])),
    remove: vi.fn(async () => storedAs([])),
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

/** Whether a choice of the Repeat group is the one picked. */
const pressed = (form: HTMLElement, name: string) =>
  within(form).getByRole("button", { name }).getAttribute("aria-pressed");

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
    expect(within(items[2]).getByText("No repeat, Alarm")).toBeTruthy();
  });

  it("shows an interval and weekdays every 2 weeks in the phone's words, with the start date", () => {
    mount([fortnight, meds]);

    const items = within(
      screen.getByRole("list", { name: "Routine alarms" }),
    ).getAllByRole("listitem");
    expect(
      within(items[0]).getByText("Every 2 weeks on Mon Wed Fri, Gym"),
    ).toBeTruthy();
    expect(within(items[1]).getByText("Every 97 minutes, Meds")).toBeTruthy();
    expect(
      within(items[1]).getByText(
        `From ${startDay("2026-11-25")}. Snooze 9 min. On`,
      ),
    ).toBeTruthy();
    // A routine with no start date says nothing of one.
    cleanup();
    mount([wake]);
    expect(screen.getByText("Snooze 9 min. On")).toBeTruthy();
    expect(
      screen.getByText(
        "Routine alarms ring on the paired phone like Clock alarms. They also ring through Pushover, and here when Ring in this browser is on. Acknowledging one anywhere stops it everywhere.",
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

  it("lists a routine's steps in order with their offsets, read only", () => {
    mount([
      {
        ...wake,
        steps: [
          { id: "s1", label: "Take medication", offsetMinutes: 5 },
          { id: "s2", label: "Beauty care routine", offsetMinutes: 5 },
          {
            id: "s3",
            label: "Tretinoin",
            offsetMinutes: 24,
            everyDays: 2,
            startsOn: "2026-11-27",
          },
        ],
      },
      gym,
    ]);

    const steps = screen.getByRole("list", {
      name: "Steps of Wake up at 06:30",
    });
    expect(
      within(steps)
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual([
      "Take medication, 5 min after",
      "Beauty care routine, 5 min after",
      "Tretinoin, 24 min after, every 2 days from Fri 27 Nov 2026",
    ]);
    expect(
      screen.queryByRole("list", { name: "Steps of Gym at 05:00" }),
    ).toBeNull();
    expect(within(steps).queryByRole("button")).toBeNull();
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

describe("the switch moves at once", () => {
  const toggle = () =>
    screen.getByRole("switch", {
      name: "Wake up at 06:30",
    }) as HTMLInputElement;

  /** An update that answers only when the test says so. */
  function held() {
    let answer: (result: RoutineResult) => void = () => {};
    const update = vi.fn(
      () =>
        new Promise<RoutineResult>((resolve) => {
          answer = resolve;
        }),
    );
    return { update, answer: (result: RoutineResult) => answer(result) };
  }

  it("moves before the server answers, says Sending, then Saved", async () => {
    const { update, answer } = held();
    mount([wake], fakeApi({ update }));

    await press(toggle());

    expect(toggle().checked).toBe(false);
    expect(toggle().disabled).toBe(false);
    expect(screen.getByText("Sending…")).toBeTruthy();
    expect(screen.getByText(/Snooze 9 min\. Off/)).toBeTruthy();

    await act(async () => answer(storedAs([{ ...wake, enabled: false }])));

    expect(toggle().checked).toBe(false);
    expect(screen.getByText("Saved")).toBeTruthy();
    expect(screen.queryByText("Sending…")).toBeNull();
  });

  it("says Sending in a polite live region", async () => {
    const { update } = held();
    mount([wake], fakeApi({ update }));

    await press(toggle());

    const status = screen.getByText("Sending…").closest('[role="status"]');
    expect(status?.getAttribute("aria-live")).toBe("polite");
  });

  it("with no connection keeps the new position, waits, and sends again when back online", async () => {
    const update = vi
      .fn<RoutinesApi["update"]>()
      .mockResolvedValueOnce({ ok: false, reason: "network" })
      .mockResolvedValue(storedAs([{ ...wake, enabled: false }]));
    mount([wake], fakeApi({ update }));

    await press(toggle());

    expect(toggle().checked).toBe(false);
    expect(
      screen.getByText(
        "Waiting for a connection. It will send when you are back online.",
      ),
    ).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();

    await act(async () => {
      window.dispatchEvent(new Event("online"));
    });

    expect(update).toHaveBeenCalledTimes(2);
    expect(update).toHaveBeenLastCalledWith(
      wake.id,
      expect.objectContaining({ enabled: false }),
    );
    expect(toggle().checked).toBe(false);
    expect(screen.getByText("Saved")).toBeTruthy();
  });

  it("a server error waits too, and is sent again every 15 s", async () => {
    vi.useFakeTimers();
    try {
      const update = vi
        .fn<RoutinesApi["update"]>()
        .mockResolvedValueOnce({ ok: false, reason: "unavailable" })
        .mockResolvedValueOnce({ ok: false, reason: "network" })
        .mockResolvedValue(storedAs([{ ...wake, enabled: false }]));
      mount([wake], fakeApi({ update }));

      await press(toggle());
      expect(screen.getByText(/Waiting for a connection/)).toBeTruthy();

      await act(async () => {
        vi.advanceTimersByTime(15_000);
      });
      expect(update).toHaveBeenCalledTimes(2);
      expect(screen.getByText(/Waiting for a connection/)).toBeTruthy();
      expect(toggle().checked).toBe(false);

      await act(async () => {
        vi.advanceTimersByTime(15_000);
      });
      expect(update).toHaveBeenCalledTimes(3);
      expect(screen.getByText("Saved")).toBeTruthy();
    } finally {
      vi.useRealTimers();
    }
  });

  it("a refusal puts the switch back and says why", async () => {
    const update = vi.fn<RoutinesApi["update"]>(async () => ({
      ok: false,
      reason: "refused",
    }));
    mount([wake], fakeApi({ update }));

    await press(toggle());

    expect(toggle().checked).toBe(true);
    expect(
      screen.getByText("The server refused. Refresh and try again."),
    ).toBeTruthy();
    expect(screen.queryByText(/Waiting for a connection/)).toBeNull();
  });

  it("a refresh while the change is out does not flip the switch back", async () => {
    const { update, answer } = held();
    const api = fakeApi({ update });
    const onState = vi.fn();
    const { rerender } = render(
      <RoutinesSection routines={[wake]} onState={onState} api={api} />,
    );

    await press(toggle());
    // The page's own refresh hands down the old state.
    rerender(
      <RoutinesSection routines={[{ ...wake }]} onState={onState} api={api} />,
    );
    expect(toggle().checked).toBe(false);

    await act(async () => answer(storedAs([{ ...wake, enabled: false }])));
    expect(toggle().checked).toBe(false);
  });

  it("presses while one is out send only the latest, once it answers", async () => {
    const { update, answer } = held();
    mount([wake], fakeApi({ update }));

    await press(toggle());
    await press(toggle());
    expect(toggle().checked).toBe(true);
    expect(update).toHaveBeenCalledTimes(1);

    await act(async () => answer(storedAs([{ ...wake, enabled: false }])));
    expect(update).toHaveBeenCalledTimes(2);
    expect(update).toHaveBeenLastCalledWith(
      wake.id,
      expect.objectContaining({ enabled: true }),
    );
    expect(toggle().checked).toBe(true);
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
    expect(
      within(form).getByText(
        "Repeats: Mon Wed Fri. It rings until you stop it.",
      ),
    ).toBeTruthy();
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
      repeat: "weekdays",
      every: 1,
      unit: "weeks",
      startsOn: null,
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
        "No repeat. It rings at the next time shown until you stop it, then switches off.",
      ),
    ).toBeTruthy();
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({ days: [], label: "", snoozeMinutes: 9 }),
    );
  });

  it("starts on No repeat and offers the three ways to repeat", () => {
    mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    expect(pressed(form, "No repeat")).toBe("true");
    expect(pressed(form, "Days of the week")).toBe("false");
    expect(pressed(form, "Every…")).toBe("false");
    expect(within(form).queryByLabelText("Every")).toBeNull();
    expect(within(form).queryByLabelText("Starting")).toBeNull();
  });

  it("a day pressed moves the choice to Days of the week, and No repeat clears the days", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Repeat on Monday" }));
    expect(pressed(form, "Days of the week")).toBe("true");
    expect(
      (within(form).getByLabelText("Every") as HTMLInputElement).value,
    ).toBe("1");
    expect(within(form).getByText("week")).toBeTruthy();
    // Every week counts from nothing, so no start date is asked for.
    expect(within(form).queryByLabelText("Starting")).toBeNull();

    await press(within(form).getByRole("button", { name: "No repeat" }));
    expect(
      within(form)
        .getByRole("button", { name: "Repeat on Monday" })
        .getAttribute("aria-pressed"),
    ).toBe("false");
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({
        days: [],
        repeat: "weekdays",
        every: 1,
        unit: "weeks",
        startsOn: null,
      }),
    );
  });

  it("every 97 minutes sends an interval with no days and its start date", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    fireEvent.change(within(form).getByLabelText(/^Time/), {
      target: { value: "06:30" },
    });
    await press(within(form).getByRole("button", { name: "Every…" }));
    expect(
      within(form).queryByRole("button", { name: "Repeat on Monday" }),
    ).toBeNull();
    // The start date is today on this computer until another is picked.
    expect(
      (within(form).getByLabelText("Starting") as HTMLInputElement).value,
    ).toBe(todayDay());
    const every = within(form).getByLabelText("Every") as HTMLInputElement;
    expect([every.type, every.min, every.max]).toEqual(["number", "1", "999"]);
    fireEvent.change(every, { target: { value: "97" } });
    await act(async () => {
      fireEvent.mouseDown(within(form).getByRole("combobox", { name: "Unit" }));
    });
    expect(
      screen.getAllByRole("option").map((option) => option.textContent),
    ).toEqual(["minutes", "hours", "days", "weeks", "months", "years"]);
    await press(screen.getByRole("option", { name: "minutes" }));
    fireEvent.change(within(form).getByLabelText("Starting"), {
      target: { value: "2026-11-25" },
    });
    expect(
      within(form).getByText(
        `Repeats: Every 97 minutes, from ${startDay("2026-11-25")} at 06:30. It rings until you stop it.`,
      ),
    ).toBeTruthy();
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(api.create).toHaveBeenCalledExactlyOnceWith({
      label: "",
      hour: 6,
      minute: 30,
      days: [],
      repeat: "interval",
      every: 97,
      unit: "minutes",
      startsOn: "2026-11-25",
      enabled: true,
      snoozeMinutes: 9,
    });
  });

  it("the unit reads singular at 1", async () => {
    mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Every…" }));

    expect(
      within(form).getByRole("combobox", { name: "Unit" }).textContent,
    ).toBe("week");
    expect(
      within(form).getByText(
        `Repeats: Every week, from ${startDay(todayDay())} at 07:00. It rings until you stop it.`,
      ),
    ).toBeTruthy();
  });

  it("days every 2 weeks asks for a start date and sends it", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Repeat on Friday" }));
    await press(within(form).getByRole("button", { name: "Repeat on Monday" }));
    fireEvent.change(within(form).getByLabelText("Every"), {
      target: { value: "2" },
    });
    expect(within(form).getByText("weeks")).toBeTruthy();
    fireEvent.change(within(form).getByLabelText("Starting"), {
      target: { value: "2026-11-25" },
    });
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({
        days: [1, 5],
        repeat: "weekdays",
        every: 2,
        unit: "weeks",
        startsOn: "2026-11-25",
      }),
    );
  });

  it.each([["0"], ["1000"], ["2.5"], [""]])(
    "every %s is refused here with the bounds, and nothing is sent",
    async (typed) => {
      const { api } = mount([]);
      const form = screen.getByRole("form", { name: "Add routine alarm" });

      await press(within(form).getByRole("button", { name: "Every…" }));
      fireEvent.change(within(form).getByLabelText("Every"), {
        target: { value: typed },
      });
      await press(
        within(form).getByRole("button", { name: "Add routine alarm" }),
      );

      expect(within(form).getByText("A whole number, 1 to 999.")).toBeTruthy();
      expect(screen.getByText("Check the fields marked below.")).toBeTruthy();
      expect(api.create).not.toHaveBeenCalled();
    },
  );

  it("an interval with no start date is refused here", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Every…" }));
    fireEvent.change(within(form).getByLabelText("Starting"), {
      target: { value: "" },
    });
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(within(form).getByText("A start date.")).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("Days of the week with no day picked says to pick one", async () => {
    const { api } = mount([]);
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Days of the week" }));
    expect(within(form).getByText("Pick the days it rings on.")).toBeTruthy();
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(
      within(form).getByText("Pick at least one day, or choose No repeat."),
    ).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("shows the server's message under the repeat field it refused", async () => {
    mount(
      [],
      fakeApi({
        create: vi.fn(async () => ({
          ok: false as const,
          reason: "invalid" as const,
          errors: {
            every: ["1 to 999."],
            startsOn: ["The date the interval counts from, as yyyy-MM-dd."],
          },
        })),
      }),
    );
    const form = screen.getByRole("form", { name: "Add routine alarm" });

    await press(within(form).getByRole("button", { name: "Every…" }));
    await press(
      within(form).getByRole("button", { name: "Add routine alarm" }),
    );

    expect(within(form).getByText("1 to 999.")).toBeTruthy();
    expect(
      within(form).getByText(
        "The date the interval counts from, as yyyy-MM-dd.",
      ),
    ).toBeTruthy();
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
      repeat: "weekdays",
      every: 1,
      unit: "weeks",
      startsOn: null,
      enabled: false,
      snoozeMinutes: 9,
    });
    expect(
      screen.getByRole("form", { name: "Add routine alarm" }),
    ).toBeTruthy();
  });

  it("an edit of a routine with steps sends no steps, so the server keeps them", async () => {
    const steps = [{ id: "s1", label: "Take medication", offsetMinutes: 5 }];
    const { api } = mount([{ ...wake, steps }]);

    await press(screen.getByRole("button", { name: "Edit Wake up at 06:30" }));
    const form = screen.getByRole("form", { name: "Edit routine alarm" });
    await press(
      within(form).getByRole("button", { name: "Save routine alarm" }),
    );

    expect(api.update).toHaveBeenCalledExactlyOnceWith(wake.id, {
      label: "Wake up",
      hour: 6,
      minute: 30,
      days: [1, 2, 3, 4, 5],
      repeat: "weekdays",
      every: 1,
      unit: "weeks",
      startsOn: null,
      enabled: true,
      snoozeMinutes: 9,
    });
  });

  it("fills the form from an interval and saves a change of its number", async () => {
    const { api } = mount([meds]);

    await press(screen.getByRole("button", { name: "Edit Meds at 06:30" }));
    const form = screen.getByRole("form", { name: "Edit routine alarm" });
    expect(pressed(form, "Every…")).toBe("true");
    expect(
      (within(form).getByLabelText("Every") as HTMLInputElement).value,
    ).toBe("97");
    expect(
      within(form).getByRole("combobox", { name: "Unit" }).textContent,
    ).toBe("minutes");
    expect(
      (within(form).getByLabelText("Starting") as HTMLInputElement).value,
    ).toBe("2026-11-25");
    expect(
      within(form).queryByRole("button", { name: "Repeat on Monday" }),
    ).toBeNull();

    fireEvent.change(within(form).getByLabelText("Every"), {
      target: { value: "45" },
    });
    await press(
      within(form).getByRole("button", { name: "Save routine alarm" }),
    );

    expect(api.update).toHaveBeenCalledExactlyOnceWith(meds.id, {
      label: "Meds",
      hour: 6,
      minute: 30,
      days: [],
      repeat: "interval",
      every: 45,
      unit: "minutes",
      startsOn: "2026-11-25",
      enabled: true,
      snoozeMinutes: 9,
    });
  });

  it("fills the form from weekdays every 2 weeks and changes an interval to it", async () => {
    const { api } = mount([fortnight, meds]);

    await press(screen.getByRole("button", { name: "Edit Gym at 05:00" }));
    let form = screen.getByRole("form", { name: "Edit routine alarm" });
    expect(pressed(form, "Days of the week")).toBe("true");
    expect(
      (within(form).getByLabelText("Every") as HTMLInputElement).value,
    ).toBe("2");
    expect(
      (within(form).getByLabelText("Starting") as HTMLInputElement).value,
    ).toBe("2026-11-25");
    expect(
      within(form)
        .getByRole("button", { name: "Repeat on Wednesday" })
        .getAttribute("aria-pressed"),
    ).toBe("true");

    await press(screen.getByRole("button", { name: "Edit Meds at 06:30" }));
    form = screen.getByRole("form", { name: "Edit routine alarm" });
    await press(within(form).getByRole("button", { name: "Days of the week" }));
    await press(within(form).getByRole("button", { name: "Repeat on Monday" }));
    await press(
      within(form).getByRole("button", { name: "Repeat on Wednesday" }),
    );
    fireEvent.change(within(form).getByLabelText("Every"), {
      target: { value: "2" },
    });
    expect(
      within(form).getByText(
        `Repeats: Every 2 weeks on Mon Wed, from ${startDay("2026-11-25")}. It rings until you stop it.`,
      ),
    ).toBeTruthy();
    await press(
      within(form).getByRole("button", { name: "Save routine alarm" }),
    );

    expect(api.update).toHaveBeenCalledExactlyOnceWith(meds.id, {
      label: "Meds",
      hour: 6,
      minute: 30,
      days: [1, 3],
      repeat: "weekdays",
      every: 2,
      unit: "weeks",
      startsOn: "2026-11-25",
      enabled: true,
      snoozeMinutes: 9,
    });
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
