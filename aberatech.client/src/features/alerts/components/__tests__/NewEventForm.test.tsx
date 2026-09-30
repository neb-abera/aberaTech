// @vitest-environment jsdom
/**
 * The New event form from its button: it sends the fields the server
 * takes, hands the answer's state to the page, shows each refused field
 * under its input, and says why Google Calendar did not take the event.
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
import type { AlertsState, CreateResult, NewEvent } from "../../core/api";
import NewEventForm from "../NewEventForm";

const state: AlertsState = {
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
  calendarWrite: null,
  alerts: [],
};

const now = () => new Date(2026, 9, 28, 8, 20);

function renderForm(answer: CreateResult) {
  const create = vi.fn(async (_event: NewEvent) => answer);
  const onCreated = vi.fn();
  render(
    <NewEventForm
      defaultLead={10}
      onCreated={onCreated}
      create={create}
      now={now}
    />,
  );
  return { create, onCreated };
}

const press = async () => {
  await act(async () => {
    fireEvent.click(screen.getByRole("button", { name: "Add event" }));
  });
};

beforeAll(() => {
  renderForm({ ok: true, state });
  cleanup();
});

afterEach(() => cleanup());

describe("New event", () => {
  it("sends the title, the start as an instant, the duration, the location, the type and the lead", async () => {
    const { create, onCreated } = renderForm({ ok: true, state });
    expect((screen.getByLabelText(/^Starts/) as HTMLInputElement).value).toBe(
      "2026-10-28T09:00",
    );

    fireEvent.change(screen.getByLabelText(/^Title/), {
      target: { value: "  Dentist  " },
    });
    fireEvent.change(screen.getByLabelText(/^Location/), {
      target: { value: "Main St" },
    });
    fireEvent.change(screen.getByLabelText(/^Duration in minutes/), {
      target: { value: "45" },
    });
    fireEvent.change(screen.getByLabelText(/^Reminder in minutes before/), {
      target: { value: "20" },
    });
    fireEvent.click(
      screen.getByRole("button", { name: "New event type Ring once" }),
    );
    await press();

    expect(create).toHaveBeenCalledWith({
      title: "Dentist",
      startsAt: new Date(2026, 9, 28, 9, 0).toISOString(),
      durationMinutes: 45,
      location: "Main St",
      type: "notification",
      leadMinutes: 20,
    });
    expect(onCreated).toHaveBeenCalledWith(state);
    expect(
      screen.getByText("Dentist is on the calendar and listed below."),
    ).toBeTruthy();
    expect((screen.getByLabelText(/^Title/) as HTMLInputElement).value).toBe(
      "",
    );
  });

  it("leaves the lead out when it is empty, so the server uses the default, and starts as an alarm", async () => {
    const { create } = renderForm({ ok: true, state });
    expect(
      screen
        .getByRole("button", { name: "New event type Ring until stopped" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    expect(
      screen.getByText("0 to 1440. Empty uses the default lead, 10."),
    ).toBeTruthy();
    fireEvent.change(screen.getByLabelText(/^Title/), {
      target: { value: "Call" },
    });
    await press();

    const sent = create.mock.calls[0][0];
    expect(sent.type).toBe("alarm");
    expect(sent.location).toBeNull();
    expect("leadMinutes" in sent).toBe(false);
  });

  it("shows each field the server refused under its input", async () => {
    const { onCreated } = renderForm({
      ok: false,
      reason: "invalid",
      errors: {
        title: ["1 to 200 characters."],
        durationMinutes: ["5 to 1440."],
      },
    });
    await press();

    expect(screen.getByText("1 to 200 characters.")).toBeTruthy();
    expect(screen.getByText("5 to 1440.")).toBeTruthy();
    expect(onCreated).not.toHaveBeenCalled();
  });

  it("an unreadable start is refused before anything is sent", async () => {
    const { create } = renderForm({ ok: true, state });
    fireEvent.change(screen.getByLabelText(/^Starts/), {
      target: { value: "" },
    });
    await press();

    expect(create).not.toHaveBeenCalled();
    expect(screen.getByText("A date and a time.")).toBeTruthy();
  });

  it.each([
    [
      {
        ok: false as const,
        reason: "conflict" as const,
        detail: "The connected calendar is not the one alerts read.",
      },
      "The connected calendar is not the one alerts read.",
    ],
    [
      { ok: false as const, reason: "google" as const },
      "Google Calendar did not take the event.",
    ],
    [
      { ok: false as const, reason: "throttled" as const },
      "Too many presses. Wait a minute.",
    ],
    [
      { ok: false as const, reason: "visitor" as const },
      "The session expired. Reload and sign in again.",
    ],
    [
      { ok: false as const, reason: "network" as const },
      "No answer from the server. Check the connection and try again.",
    ],
  ])("says why it was not created: %o", async (answer, text) => {
    renderForm(answer);
    await press();
    expect(screen.getByText(text)).toBeTruthy();
  });
});
