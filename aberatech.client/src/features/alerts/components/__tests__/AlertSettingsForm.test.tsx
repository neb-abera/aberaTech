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
    expect(input("Read the calendar every").value).toBe("5");
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

  it("sends a cleared number as nothing, for the server to name", async () => {
    const { saver } = mount();

    fireEvent.change(input("Read the calendar every"), {
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

  it("says once that the secrets are not editable here", () => {
    mount();

    expect(
      screen.getAllByText(/Pushover keys stay container secrets/),
    ).toHaveLength(1);
  });
});
