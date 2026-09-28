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

    expect(input("Repeat every").value).toBe("60");
    expect(input("Stop after").value).toBe("180");
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
    expect(
      screen
        .getByRole("button", { name: "Emergency" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(
      screen
        .getByRole("button", { name: "Normal" })
        .getAttribute("aria-pressed"),
    ).toBe("false");
  });

  it("shows the working for when an emergency message stops", () => {
    mount();
    expect(
      screen.getByText(
        "Stops after 50 min: 50 sounds × 60 s = 50 min, before the 180 min limit.",
      ),
    ).toBeTruthy();

    fireEvent.change(input("Repeat every"), { target: { value: "120" } });
    fireEvent.change(input("Stop after"), { target: { value: "30" } });
    expect(
      screen.getByText(
        "Stops after 30 min: the limit comes before 50 sounds × 120 s = 100 min.",
      ),
    ).toBeTruthy();

    fireEvent.change(input("Repeat every"), { target: { value: "45" } });
    fireEvent.change(input("Stop after"), { target: { value: "180" } });
    expect(screen.getByText(/50 sounds × 45 s = 37\.5 min/)).toBeTruthy();
  });

  it("sets the repeat from a preset chip", () => {
    mount();

    fireEvent.click(screen.getByRole("button", { name: "Repeat every 2 min" }));

    expect(input("Repeat every").value).toBe("120");
    fireEvent.click(screen.getByRole("button", { name: "Repeat every 30 s" }));
    expect(input("Repeat every").value).toBe("30");
  });

  it("disables repeat and stop below emergency, where Pushover sounds once", () => {
    mount({ priority: 1 });

    expect(input("Repeat every").disabled).toBe(true);
    expect(input("Stop after").disabled).toBe(true);
    expect(screen.getByText(/apply to Emergency only/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Emergency" }));
    expect(input("Repeat every").disabled).toBe(false);
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

    fireEvent.click(screen.getByRole("button", { name: "Normal" }));
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
      priority: 0,
      sound: "none",
      lookaheadHours: 72,
      includeAllDay: true,
      timeZone: "America/New_York",
      ownerEmails: ["a@example.test", "b@example.test"],
    });
    expect(onSaved).toHaveBeenCalledTimes(1);
    expect(screen.getByText("Settings saved.")).toBeTruthy();
  });

  it("shows each field the server refused, beside that field", async () => {
    mount({}, async () => ({
      ok: false,
      reason: "invalid",
      errors: {
        repeatSeconds: ["Between 30 and 10800 seconds."],
        timeZone: ["Not a time zone database name, such as America/New_York."],
      },
    }));

    fireEvent.change(input("Repeat every"), { target: { value: "5" } });
    fireEvent.click(saveButton());
    await flush();

    expect(screen.getByText("Between 30 and 10800 seconds.")).toBeTruthy();
    expect(
      screen.getByText(
        "Not a time zone database name, such as America/New_York.",
      ),
    ).toBeTruthy();
    expect(screen.getByText(/Not saved\. Check the fields/)).toBeTruthy();
    expect(input("Repeat every").getAttribute("aria-invalid")).toBe("true");
    expect(input("Repeat every").value).toBe("5");
  });

  it("keeps alarms, notifications and unmarked events in their own sections", () => {
    mount({
      notificationPriority: 1,
      notificationSound: "siren",
      defaultType: "notification",
    });

    for (const heading of [
      "Alarms: events marked #critical or set to Alarm",
      "Notifications: events set to Notification",
      "Calendar",
    ])
      expect(screen.getByRole("heading", { name: heading })).toBeTruthy();
    const pressed = (name: string) =>
      screen.getByRole("button", { name }).getAttribute("aria-pressed");
    expect(pressed("Notification priority High")).toBe("true");
    expect(pressed("Notification priority Normal")).toBe("false");
    expect(pressed("Unmarked events: Notification")).toBe("true");
    expect(pressed("Unmarked events: None")).toBe("false");
    expect(
      (screen.getByLabelText("Notification sound") as HTMLSelectElement).value,
    ).toBe("siren");
    // The alarm's own chips are untouched by the notification's.
    expect(pressed("Emergency")).toBe("true");
  });

  it("sends the notification settings and the default for unmarked events with the rest of the form", async () => {
    const { saver } = mount();

    fireEvent.click(
      screen.getByRole("button", { name: "Notification priority High" }),
    );
    fireEvent.change(screen.getByLabelText("Notification sound"), {
      target: { value: "siren" },
    });
    fireEvent.click(
      screen.getByRole("button", { name: "Unmarked events: Notification" }),
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
      screen.getByRole("button", { name: "Unmarked events: Notification" }),
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

  it("Nonstop sets Emergency, a 30 s repeat and a long sound, and Save sends them", async () => {
    const { saver } = mount({ priority: 0, sound: "" });
    const nonstop = screen.getByRole("button", { name: "Nonstop" });
    expect(nonstop.getAttribute("aria-pressed")).toBe("false");

    fireEvent.click(nonstop);

    expect(
      screen
        .getByRole("button", { name: "Emergency" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(input("Repeat every").value).toBe("30");
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
      priority: 2,
      repeatSeconds: 30,
      sound: "persistent",
    });
  });

  it("says under the repeat why 1 s and 5 s are not offered, with the numbers", () => {
    mount();

    const floor = screen.getByText(
      /Pushover repeats no faster than every 30 s/,
    );
    expect(floor.textContent).toContain("1 s and 5 s cannot be sent.");
    expect(floor.textContent).toContain(
      "iOS plays a notification sound for up to 30 s, the length of the 30 s repeat.",
    );
    expect(floor.textContent).toContain(
      "Nonstop sets Emergency, 30 s and persistent, one of Pushover's 5 long sounds.",
    );
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
});
