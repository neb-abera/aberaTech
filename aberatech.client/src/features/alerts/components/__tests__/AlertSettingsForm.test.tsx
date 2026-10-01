// @vitest-environment jsdom
/**
 * The settings form on /alerts from every control on it: what it shows,
 * what each control changes, what Save sends, and what it shows back.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import type { ActionResult, AlertSettings } from "../../core/api";
import AlertSettingsForm from "../AlertSettingsForm";

afterEach(() => {
  cleanup();
});

// The first MUI mount in a worker pays the theme's setup. Paid once here.
beforeAll(() => {
  render(
    <AlertSettingsForm settings={settings} bounds={bounds} onSaved={vi.fn()} />,
  );
  cleanup();
});

function mount(
  over: Partial<AlertSettings> = {},
  save: (value: AlertSettings) => Promise<ActionResult> = async () => ({
    ok: true,
  }),
) {
  const onSaved = vi.fn();
  const saver = vi.fn(save);
  render(
    <AlertSettingsForm
      settings={{ ...settings, ...over }}
      bounds={bounds}
      onSaved={onSaved}
      save={saver}
    />,
  );
  return { onSaved, saver };
}

const input = (label: string) =>
  screen.getByLabelText(label) as HTMLInputElement;
const saveButton = () => screen.getByRole("button", { name: "Save settings" });

const flush = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
};

describe("the settings form", () => {
  it("shows the settings in force", () => {
    mount({
      sound: "siren",
      ownerEmails: ["a@example.test", "b@example.test"],
      timeZone: "Asia/Amman",
      includeAllDay: true,
    });

    expect(input("Pushover repeats every").value).toBe("60");
    expect(input("Pushover stops after").value).toBe("180");
    expect(input("Default lead").value).toBe("10");
    expect(input("Check calendar every").value).toBe("5");
    expect(input("Look ahead").value).toBe("48");
    expect((screen.getByLabelText("Sound") as HTMLSelectElement).value).toBe(
      "siren",
    );
    expect(input("Time zone when the calendar names none").value).toBe(
      "Asia/Amman",
    );
    expect(input("Your addresses, for declined invitations").value).toBe(
      "a@example.test, b@example.test",
    );
    expect(input("Alert for all-day events").checked).toBe(true);
  });

  it("has no priority for an alarm: every alarm repeats until acknowledged", () => {
    mount();

    const alarms = screen.getByRole("heading", {
      name: "Ring until stopped settings",
    });
    expect(alarms).toBeTruthy();
    expect(screen.queryByText("Priority")).toBeNull();
    expect(screen.queryByRole("button", { name: "Emergency" })).toBeNull();
    expect(screen.queryByRole("button", { name: "High" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Normal" })).toBeNull();
    expect(document.body.textContent).not.toMatch(/emergency/i);
  });

  it("says the 30 s floor is Pushover's, and that the phone and this browser ring without it", () => {
    mount();

    expect(screen.getByLabelText("Pushover repeats every")).toBeTruthy();
    expect(screen.getByLabelText("Pushover stops after")).toBeTruthy();
    expect(
      screen.getByText(
        "Pushover does not repeat faster than every 30 s. A paired phone rings as an iPhone alarm until you press Stop. Ring in this browser beeps every second until you acknowledge it.",
      ),
    ).toBeTruthy();
    expect(screen.queryByText("30 to 10800 seconds")).toBeNull();
  });

  it("shows the working for when an alarm stops", () => {
    mount();
    expect(
      screen.getByText(
        "Stops after 50 min: 50 sounds × 60 s = 50 min, before the 180 min limit.",
      ),
    ).toBeTruthy();

    fireEvent.change(input("Pushover repeats every"), {
      target: { value: "120" },
    });
    fireEvent.change(input("Pushover stops after"), {
      target: { value: "30" },
    });
    expect(
      screen.getByText(
        "Stops after 30 min: the limit comes before 50 sounds × 120 s = 100 min.",
      ),
    ).toBeTruthy();

    fireEvent.change(input("Pushover repeats every"), {
      target: { value: "45" },
    });
    fireEvent.change(input("Pushover stops after"), {
      target: { value: "180" },
    });
    expect(screen.getByText(/50 sounds × 45 s = 37\.5 min/)).toBeTruthy();
  });

  it("sets the repeat from a preset chip", () => {
    mount();

    fireEvent.click(screen.getByRole("button", { name: "Repeat every 2 min" }));

    expect(input("Pushover repeats every").value).toBe("120");
    fireEvent.click(screen.getByRole("button", { name: "Repeat every 30 s" }));
    expect(input("Pushover repeats every").value).toBe("30");
  });

  it("always enables repeat, stop, their presets and the working", () => {
    mount();

    expect(input("Pushover repeats every").disabled).toBe(false);
    expect(input("Pushover stops after").disabled).toBe(false);
    for (const name of [
      "Repeat every 30 s",
      "Repeat every 1 min",
      "Repeat every 2 min",
      "Repeat every 5 min",
      "Nonstop",
    ])
      expect(
        screen.getByRole("button", { name }).getAttribute("aria-disabled"),
      ).not.toBe("true");
    expect(screen.getByText(/^Stops after 50 min/)).toBeTruthy();
  });

  it("keeps Save off until something changes, and off again when it is changed back", () => {
    mount();
    expect(saveButton().hasAttribute("disabled")).toBe(true);

    fireEvent.change(input("Default lead"), { target: { value: "15" } });
    expect(saveButton().hasAttribute("disabled")).toBe(false);

    fireEvent.change(input("Default lead"), { target: { value: "10" } });
    expect(saveButton().hasAttribute("disabled")).toBe(true);
  });

  it("sends the whole form, numbers as numbers and addresses as a list", async () => {
    const { saver, onSaved } = mount({}, async (value) => ({
      ok: true,
      state: { settings: value } as never,
    }));

    fireEvent.change(screen.getByLabelText("Sound"), {
      target: { value: "none" },
    });
    fireEvent.change(input("Look ahead"), { target: { value: "72" } });
    fireEvent.click(input("Alert for all-day events"));
    fireEvent.change(input("Time zone when the calendar names none"), {
      target: { value: " America/New_York " },
    });
    fireEvent.change(input("Your addresses, for declined invitations"), {
      target: { value: "a@example.test, b@example.test" },
    });
    fireEvent.click(saveButton());
    await flush();

    expect(saver).toHaveBeenCalledWith({
      ...settings,
      sound: "none",
      lookaheadHours: 72,
      includeAllDay: true,
      timeZone: "America/New_York",
      ownerEmails: ["a@example.test", "b@example.test"],
    });
    expect(saver.mock.calls[0][0]).not.toHaveProperty("priority");
    expect(onSaved).toHaveBeenCalledTimes(1);
    expect(screen.getByText("Settings saved.")).toBeTruthy();
  });

  it("shows each field the server refused, beside that field", async () => {
    mount({}, async () => ({
      ok: false,
      reason: "invalid",
      errors: {
        repeatSeconds: [
          "Pushover does not repeat faster than every 30 s or slower than every 10800 s.",
        ],
        timeZone: ["Not a time zone database name, such as America/New_York."],
      },
    }));

    fireEvent.change(input("Pushover repeats every"), {
      target: { value: "5" },
    });
    fireEvent.click(saveButton());
    await flush();

    expect(
      screen.getByText(
        "Pushover does not repeat faster than every 30 s or slower than every 10800 s.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "Not a time zone database name, such as America/New_York.",
      ),
    ).toBeTruthy();
    expect(screen.getByText(/Not saved\. Check the fields/)).toBeTruthy();
    expect(input("Pushover repeats every").getAttribute("aria-invalid")).toBe(
      "true",
    );
    expect(input("Pushover repeats every").value).toBe("5");
  });

  it("keeps alarms, notifications and unmarked events in their own sections", () => {
    mount({
      notificationPriority: 1,
      notificationSound: "siren",
      defaultType: "notification",
    });

    for (const heading of [
      "Ring until stopped settings",
      "Ring once settings",
      "Calendar",
    ])
      expect(screen.getByRole("heading", { name: heading })).toBeTruthy();
    const pressed = (name: string) =>
      screen.getByRole("button", { name }).getAttribute("aria-pressed");
    expect(pressed("Ring once priority High")).toBe("true");
    expect(pressed("Ring once priority Normal")).toBe("false");
    expect(pressed("Unmarked events: Ring once")).toBe("true");
    expect(pressed("Unmarked events: Off")).toBe("false");
    expect(
      (screen.getByLabelText("Ring once sound") as HTMLSelectElement).value,
    ).toBe("siren");
  });

  it("says what each notification choice does in plain words", () => {
    mount();

    expect(
      screen.getByText("One sound, follows the phone's settings."),
    ).toBeTruthy();
    expect(
      screen.getByText("One sound, even during Pushover's quiet hours."),
    ).toBeTruthy();
  });

  it("sends the notification settings and the default for unmarked events with the rest of the form", async () => {
    const { saver } = mount();

    fireEvent.click(
      screen.getByRole("button", { name: "Ring once priority High" }),
    );
    fireEvent.change(screen.getByLabelText("Ring once sound"), {
      target: { value: "siren" },
    });
    fireEvent.click(
      screen.getByRole("button", { name: "Unmarked events: Ring once" }),
    );
    fireEvent.click(saveButton());
    await flush();

    expect(saver).toHaveBeenCalledWith({
      ...settings,
      notificationPriority: 1,
      notificationSound: "siren",
      defaultType: "notification",
    });
  });

  it("shows a refused notification field beside it", async () => {
    mount({}, async () => ({
      ok: false,
      reason: "invalid",
      errors: {
        notificationPriority: ["0 or 1."],
        notificationSound: ["Not a Pushover sound."],
        defaultType: ['"none" or "notification".'],
      },
    }));

    fireEvent.click(
      screen.getByRole("button", { name: "Unmarked events: Ring once" }),
    );
    fireEvent.click(saveButton());
    await flush();

    expect(screen.getByText("0 or 1.")).toBeTruthy();
    expect(screen.getByText("Not a Pushover sound.")).toBeTruthy();
    expect(screen.getByText('"none" or "notification".')).toBeTruthy();
  });

  it("sends a cleared number as nothing, for the server to name", async () => {
    const { saver } = mount();

    fireEvent.change(input("Check calendar every"), {
      target: { value: "" },
    });
    fireEvent.click(saveButton());
    await flush();

    expect(Number.isNaN(saver.mock.calls[0][0].pollMinutes)).toBe(true);
  });

  it("says why a save did not go through", async () => {
    for (const [reason, text] of [
      ["throttled", /Too many presses/],
      ["visitor", /session expired/],
      ["network", /did not take it/],
    ] as const) {
      mount({}, async () => ({ ok: false, reason }));
      fireEvent.change(input("Default lead"), { target: { value: "20" } });
      fireEvent.click(saveButton());
      await flush();
      expect(screen.getByText(text)).toBeTruthy();
      cleanup();
    }
  });

  it("Nonstop sets a 30 s repeat and a long sound, nothing else, and Save sends them", async () => {
    const { saver } = mount({ sound: "" });
    const nonstop = screen.getByRole("button", { name: "Nonstop" });
    expect(nonstop.getAttribute("aria-pressed")).toBe("false");

    fireEvent.click(nonstop);

    expect(input("Pushover repeats every").value).toBe("30");
    expect((screen.getByLabelText("Sound") as HTMLSelectElement).value).toBe(
      "persistent",
    );
    expect(
      screen
        .getByRole("button", { name: "Nonstop" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    fireEvent.click(saveButton());
    await flush();
    expect(saver).toHaveBeenCalledWith({
      ...settings,
      repeatSeconds: 30,
      sound: "persistent",
    });
    expect(saver.mock.calls[0][0]).not.toHaveProperty("priority");
  });

  it("says under the repeat how a long sound fills the gap, with the numbers", () => {
    mount();

    const gap = screen.getByText(/A long sound plays into the gap/);
    expect(gap.textContent).toContain(
      "iOS plays a notification sound for up to 30 s, the length of the 30 s repeat.",
    );
    expect(gap.textContent).toContain(
      "Nonstop sets 30 s and persistent, one of Pushover's 5 long sounds.",
    );
    expect(gap.textContent).toContain(
      "Pushover stops after 50 repeats: 50 × 30 s = 25 min.",
    );
  });

  it("lists the sounds uploaded to the Pushover account first, under Your sounds", async () => {
    const onSaved = vi.fn();
    const saver = vi.fn(async () => ({ ok: true }) as ActionResult);
    render(
      <AlertSettingsForm
        settings={settings}
        bounds={{
          ...bounds,
          sounds: [
            {
              name: "aberaalarm",
              description: "Abera alarm (29.5 s)",
              custom: true,
            },
            ...bounds.sounds,
          ],
        }}
        onSaved={onSaved}
        save={saver}
      />,
    );

    for (const label of ["Sound", "Ring once sound"]) {
      const select = screen.getByLabelText(label) as HTMLSelectElement;
      const groups = Array.from(select.querySelectorAll("optgroup")).map(
        (group) => group.label,
      );
      expect(groups).toEqual(["Your sounds", "Pushover's sounds"]);
      const first = select.querySelector(
        "optgroup option",
      ) as HTMLOptionElement;
      expect(first.value).toBe("aberaalarm");
      expect(first.textContent).toBe("Abera alarm (29.5 s) (aberaalarm)");
    }

    fireEvent.change(screen.getByLabelText("Sound"), {
      target: { value: "aberaalarm" },
    });
    fireEvent.click(saveButton());
    await flush();
    expect(saver).toHaveBeenCalledWith({ ...settings, sound: "aberaalarm" });
  });

  it("keeps a saved upload choosable when the list names only Pushover's sounds", () => {
    mount({ sound: "aberaalarm" });

    const select = screen.getByLabelText("Sound") as HTMLSelectElement;
    expect(select.value).toBe("aberaalarm");
    expect(select.querySelector("optgroup")?.label).toBe("Your sounds");
    const notification = screen.getByLabelText(
      "Ring once sound",
    ) as HTMLSelectElement;
    expect(
      Array.from(notification.querySelectorAll("optgroup")).map(
        (group) => group.label,
      ),
    ).toEqual(["Pushover's sounds"]);
  });

  it("marks Pushover's long sounds in the list", () => {
    mount();

    const options = Array.from(
      (screen.getByLabelText("Sound") as HTMLSelectElement).options,
    ).map((option) => option.textContent);
    expect(options).toContain("persistent (long)");
    expect(options).toContain("siren");
  });

  it("explains the three calendar fields with the values typed", () => {
    mount();

    expect(
      screen.getByText(
        "An event with no notification of its own alerts 10 minutes before it starts.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "The server reads the calendar every 5 minutes. A new or moved event shows up within 5 minutes.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "Events that start in the next 48 hours are planned and listed above.",
      ),
    ).toBeTruthy();

    fireEvent.change(input("Default lead"), { target: { value: "1" } });
    fireEvent.change(input("Check calendar every"), { target: { value: "1" } });
    fireEvent.change(input("Look ahead"), { target: { value: "1" } });
    expect(
      screen.getByText(
        "An event with no notification of its own alerts 1 minute before it starts.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "The server reads the calendar every 1 minute. A new or moved event shows up within 1 minute.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "Events that start in the next 1 hour are planned and listed above.",
      ),
    ).toBeTruthy();

    fireEvent.change(input("Default lead"), { target: { value: "" } });
    expect(screen.getByText("0 to 1440 minutes.")).toBeTruthy();
  });

  it("says once that the secrets are not editable here", () => {
    mount();

    expect(
      screen.getAllByText(/Pushover keys stay container secrets/),
    ).toHaveLength(1);
  });

  it("shows the phone's sound and snooze under On the phone, with one line on what they do", () => {
    mount({ phoneSound: "chime", phoneSnoozeMinutes: 12 });

    expect(screen.getByRole("heading", { name: "On the phone" })).toBeTruthy();
    expect(
      screen.getByText(
        "The phone plays this sound for every alarm it rings. Snooze delays a calendar alarm by this many minutes without acknowledging it.",
      ),
    ).toBeTruthy();
    const sound = screen.getByLabelText("Alarm sound") as HTMLSelectElement;
    expect(sound.value).toBe("chime");
    expect([...sound.options].map((option) => option.textContent)).toEqual([
      "iPhone default",
      "Pulse",
      "Chime",
      "Rise",
      "Siren",
      "Beacon",
    ]);
    const snooze = input("Snooze");
    expect(snooze.value).toBe("12");
    expect(snooze.min).toBe("1");
    expect(snooze.max).toBe("30");
    expect(screen.getByText("1 to 30 minutes")).toBeTruthy();
  });

  it("sends the phone's sound and snooze with the rest of the form", async () => {
    const { saver } = mount();

    fireEvent.change(screen.getByLabelText("Alarm sound"), {
      target: { value: "beacon" },
    });
    fireEvent.change(input("Snooze"), { target: { value: "15" } });
    fireEvent.click(saveButton());
    await flush();

    expect(saver).toHaveBeenCalledWith({
      ...settings,
      phoneSound: "beacon",
      phoneSnoozeMinutes: 15,
    });
  });

  it("keeps Save off when the phone's fields are changed back", () => {
    mount();

    fireEvent.change(input("Snooze"), { target: { value: "10" } });
    expect(saveButton().hasAttribute("disabled")).toBe(false);
    fireEvent.change(input("Snooze"), { target: { value: "9" } });
    expect(saveButton().hasAttribute("disabled")).toBe(true);
    fireEvent.change(screen.getByLabelText("Alarm sound"), {
      target: { value: "rise" },
    });
    expect(saveButton().hasAttribute("disabled")).toBe(false);
  });

  it("shows a refused phone field beside it", async () => {
    mount({}, async () => ({
      ok: false,
      reason: "invalid",
      errors: {
        phoneSound: ['One of "default", "pulse".'],
        phoneSnoozeMinutes: ["Between 1 and 30 minutes."],
      },
    }));

    fireEvent.change(input("Snooze"), { target: { value: "31" } });
    fireEvent.click(saveButton());
    await flush();

    expect(screen.getByText('One of "default", "pulse".')).toBeTruthy();
    expect(screen.getByText("Between 1 and 30 minutes.")).toBeTruthy();
    expect(input("Snooze").getAttribute("aria-invalid")).toBe("true");
  });

  it("sends the backup delay with the form and explains it at the value typed", async () => {
    const { saver } = mount();
    const delay = input("Pushover backup after");
    expect(delay.value).toBe("0");
    expect(delay.min).toBe("0");
    expect(delay.max).toBe("900");
    expect(
      screen.getByText(
        "Pushover sends at the alert's time, with the paired phones.",
      ),
    ).toBeTruthy();

    fireEvent.change(delay, { target: { value: "120" } });
    expect(
      screen.getByText(
        "A paired phone rings first. Pushover follows 120 seconds later if nobody acknowledged it, and no later than 1 minute before the start.",
      ),
    ).toBeTruthy();
    fireEvent.change(delay, { target: { value: "901" } });
    expect(screen.getByText("0 to 900 seconds.")).toBeTruthy();
    fireEvent.change(delay, { target: { value: "120" } });
    fireEvent.click(saveButton());
    await flush();

    expect(saver).toHaveBeenCalledWith({
      ...settings,
      backupDelaySeconds: 120,
    });
  });
});
