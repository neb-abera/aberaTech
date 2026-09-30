// @vitest-environment jsdom
/**
 * Edit and Delete from their dialogs: Edit fills in the event, sends only
 * what the owner changed beside the title, start and location, and offers
 * "This event" or "All events" for an event that repeats. Delete asks
 * first, with the same choice. Each says why Google Calendar did not take
 * the change.
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
import type {
  AlertItem,
  AlertsState,
  EventEdit,
  EventScope,
  EventWriteResult,
} from "../../core/api";
import {
  DeleteEventDialog,
  EditEventDialog,
  localInput,
} from "../EventDialogs";

/** 09:00 to 09:30 in this browser's zone, with a reminder 15 minutes before. */
const start = new Date(2026, 9, 28, 9, 0);
const standup: AlertItem = {
  key: "standup@google.com|20261028T130000Z",
  title: "Standup",
  location: "Room 1",
  startsAt: start.toISOString(),
  alertAt: new Date(start.getTime() - 15 * 60_000).toISOString(),
  source: "reminder",
  skipped: false,
  muted: false,
  critical: false,
  type: "alarm",
  typeFrom: "set",
  acknowledged: false,
  acknowledgedAt: null,
  acknowledgedVia: null,
  recurring: false,
  endsAt: new Date(start.getTime() + 30 * 60_000).toISOString(),
};
const daily: AlertItem = {
  ...standup,
  key: "daily@google.com|20261028T140000Z",
  title: "Daily",
  source: "default",
  recurring: true,
  endsAt: null,
};

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

const when = () => "Wed, Oct 28, 9:00 AM EDT";

function renderEdit(alert: AlertItem, answer: EventWriteResult) {
  const edit = vi.fn(async (_body: EventEdit) => answer);
  const onSaved = vi.fn();
  const onClose = vi.fn();
  render(
    <EditEventDialog
      alert={alert}
      when={when}
      onClose={onClose}
      onSaved={onSaved}
      edit={edit}
    />,
  );
  return { edit, onSaved, onClose, dialog: screen.getByRole("dialog") };
}

function renderDelete(alert: AlertItem, answer: EventWriteResult) {
  const remove = vi.fn(async (_key: string, _scope: EventScope) => answer);
  const onDeleted = vi.fn();
  render(
    <DeleteEventDialog
      alert={alert}
      when={when}
      onClose={vi.fn()}
      onDeleted={onDeleted}
      remove={remove}
    />,
  );
  return { remove, onDeleted, dialog: screen.getByRole("dialog") };
}

const click = async (element: HTMLElement) => {
  await act(async () => {
    fireEvent.click(element);
  });
};

beforeAll(() => {
  renderEdit(standup, { ok: true, state });
  cleanup();
});

afterEach(() => cleanup());

describe("Edit", () => {
  it("fills in the event and sends the title, the start and the location, and nothing it did not change", async () => {
    const { edit, onSaved, dialog } = renderEdit(standup, {
      ok: true,
      state,
    });
    const field = (label: RegExp) =>
      within(dialog).getByLabelText(label) as HTMLInputElement;
    expect(field(/^Title/).value).toBe("Standup");
    expect(field(/^Starts/).value).toBe("2026-10-28T09:00");
    expect(field(/^Duration in minutes/).value).toBe("30");
    expect(field(/^Location/).value).toBe("Room 1");
    expect(field(/^Reminder in minutes before/).value).toBe("15");
    // Not a repeating event: no choice of which.
    expect(within(dialog).queryByText("All events")).toBeNull();

    fireEvent.change(field(/^Title/), { target: { value: "  Standup late " } });
    fireEvent.change(field(/^Starts/), {
      target: { value: "2026-10-28T09:30" },
    });
    await click(within(dialog).getByRole("button", { name: "Save" }));

    expect(edit).toHaveBeenCalledWith({
      key: standup.key,
      scope: "occurrence",
      title: "Standup late",
      startsAt: new Date(2026, 9, 28, 9, 30).toISOString(),
      location: "Room 1",
    });
    expect(onSaved).toHaveBeenCalledWith(state, "Standup late is changed.");
  });

  it("sends a new length and a new reminder, and an empty location as none", async () => {
    const { edit, dialog } = renderEdit(standup, { ok: true, state });

    fireEvent.change(within(dialog).getByLabelText(/^Duration in minutes/), {
      target: { value: "45" },
    });
    fireEvent.change(
      within(dialog).getByLabelText(/^Reminder in minutes before/),
      { target: { value: "20" } },
    );
    fireEvent.change(within(dialog).getByLabelText(/^Location/), {
      target: { value: "  " },
    });
    await click(within(dialog).getByRole("button", { name: "Save" }));

    expect(edit.mock.calls[0][0]).toMatchObject({
      durationMinutes: 45,
      leadMinutes: 20,
      location: null,
    });
  });

  it("offers This event and All events for a repeating event, and All events changes every occurrence", async () => {
    const { edit, onSaved, dialog } = renderEdit(daily, { ok: true, state });
    const thisEvent = within(dialog).getByLabelText(
      "This event",
    ) as HTMLInputElement;
    expect(thisEvent.checked).toBe(true);
    // No end and no reminder of its own: both start empty and are left out.
    expect(
      (
        within(dialog).getByLabelText(
          /^Duration in minutes/,
        ) as HTMLInputElement
      ).value,
    ).toBe("");

    await click(within(dialog).getByLabelText("All events"));
    await click(within(dialog).getByRole("button", { name: "Save" }));

    expect(edit.mock.calls[0][0]).toEqual({
      key: daily.key,
      scope: "series",
      title: "Daily",
      startsAt: start.toISOString(),
      location: "Room 1",
    });
    expect(onSaved).toHaveBeenCalledWith(
      state,
      "Daily is changed, every occurrence.",
    );
  });

  it("shows each refused field under its input", async () => {
    const { onSaved, dialog } = renderEdit(standup, {
      ok: false,
      reason: "invalid",
      errors: {
        startsAt: ["An ISO 8601 time with its offset, in the future."],
      },
    });

    await click(within(dialog).getByRole("button", { name: "Save" }));

    expect(
      within(dialog).getByText(
        "An ISO 8601 time with its offset, in the future.",
      ),
    ).toBeTruthy();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("says why Google Calendar did not take the change, and when the event is gone", async () => {
    const { dialog } = renderEdit(standup, {
      ok: false,
      reason: "conflict",
      detail: "Only the organizer can change this event.",
    });
    await click(within(dialog).getByRole("button", { name: "Save" }));
    expect(
      within(dialog).getByText("Only the organizer can change this event."),
    ).toBeTruthy();
    cleanup();

    const gone = renderEdit(standup, { ok: false, reason: "gone" });
    await click(within(gone.dialog).getByRole("button", { name: "Save" }));
    expect(
      within(gone.dialog).getByText(
        "This event is no longer listed. Close this and refresh.",
      ),
    ).toBeTruthy();
  });

  it("closes on Cancel and sends nothing", async () => {
    const { edit, onClose, dialog } = renderEdit(standup, { ok: true, state });

    await click(within(dialog).getByRole("button", { name: "Cancel" }));

    expect(onClose).toHaveBeenCalled();
    expect(edit).not.toHaveBeenCalled();
  });
});

describe("Delete", () => {
  it("asks first, then deletes the one event", async () => {
    const { remove, onDeleted, dialog } = renderDelete(standup, {
      ok: true,
      state,
    });
    expect(within(dialog).getByText("Delete Standup?")).toBeTruthy();
    expect(within(dialog).queryByText("All events")).toBeNull();

    await click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(remove).toHaveBeenCalledWith(standup.key, "occurrence");
    expect(onDeleted).toHaveBeenCalledWith(state, "Standup is deleted.");
  });

  it("deletes this event or all events of a repeating one", async () => {
    const { remove, onDeleted, dialog } = renderDelete(daily, {
      ok: true,
      state,
    });

    await click(within(dialog).getByLabelText("All events"));
    await click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(remove).toHaveBeenCalledWith(daily.key, "series");
    expect(onDeleted).toHaveBeenCalledWith(
      state,
      "Daily is deleted, every occurrence.",
    );
  });

  it("says why Google Calendar kept the event", async () => {
    const { onDeleted, dialog } = renderDelete(standup, {
      ok: false,
      reason: "google",
      detail: "Google refused the change (HTTP 500).",
    });

    await click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(
      within(dialog).getByText("Google refused the change (HTTP 500)."),
    ).toBeTruthy();
    expect(onDeleted).not.toHaveBeenCalled();
  });
});

describe("localInput", () => {
  it("writes an instant as the input shows it, in this browser's zone", () => {
    expect(localInput(new Date(2026, 0, 2, 3, 4).toISOString())).toBe(
      "2026-01-02T03:04",
    );
  });
});
