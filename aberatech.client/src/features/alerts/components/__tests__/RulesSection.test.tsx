// @vitest-environment jsdom
/**
 * Rules from the section's controls: add, choose the field, type the
 * text, choose what it sends, move, remove, and Save rules, which sends
 * the whole list and shows what the server stored.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import type { AlertsState, StateChangeResult, TypeRule } from "../../core/api";
import RulesSection, { type RulesApi } from "../RulesSection";

const state = (rules: TypeRule[]): AlertsState => ({
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
  rules,
});

const work: TypeRule = { field: "organizer", text: "army.mil", type: "alarm" };
const place: TypeRule = { field: "location", text: "", type: "notification" };

function setUp(
  rules: TypeRule[],
  answer?: (sent: TypeRule[]) => StateChangeResult,
) {
  const save = vi.fn(
    async (sent: TypeRule[]) =>
      answer?.(sent) ?? { ok: true as const, state: state(sent) },
  );
  const onState = vi.fn();
  render(
    <RulesSection
      rules={rules}
      api={{ save } satisfies RulesApi}
      onState={onState}
    />,
  );
  return { save, onState };
}

const rule = (n: number) => screen.getByRole("group", { name: `Rule ${n}` });

afterEach(cleanup);

describe("RulesSection", () => {
  it("adds a rule, fills it in and saves the whole list", async () => {
    const { save, onState } = setUp([work]);

    fireEvent.click(screen.getByRole("button", { name: "Add rule" }));
    const added = rule(2);
    fireEvent.change(within(added).getByLabelText("When"), {
      target: { value: "title" },
    });
    fireEvent.change(within(added).getByLabelText("Text"), {
      target: { value: " standup " },
    });
    fireEvent.change(within(added).getByLabelText("Sends"), {
      target: { value: "none" },
    });
    expect(screen.getByRole("status").textContent).toBe("Not saved yet");
    fireEvent.click(screen.getByRole("button", { name: "Save rules" }));

    await waitFor(() =>
      expect(screen.getByRole("status").textContent).toBe("Saved"),
    );
    expect(save).toHaveBeenCalledWith([
      work,
      { field: "title", text: " standup ", type: "none" },
    ]);
    expect(onState).toHaveBeenCalledOnce();
  });

  it("moves and removes rules, and saves them in the new order", async () => {
    const { save } = setUp([work, place]);

    fireEvent.click(screen.getByRole("button", { name: "Move rule 2 up" }));
    expect(
      (within(rule(1)).getByLabelText("When") as HTMLSelectElement).value,
    ).toBe("location");
    fireEvent.click(screen.getByRole("button", { name: "Remove rule 2" }));
    fireEvent.click(screen.getByRole("button", { name: "Save rules" }));

    await waitFor(() => expect(save).toHaveBeenCalledWith([place]));
  });

  it("keeps Save off with nothing changed or a title rule with no text, and says why", () => {
    setUp([work]);
    expect(
      (screen.getByRole("button", { name: "Save rules" }) as HTMLButtonElement)
        .disabled,
    ).toBe(true);

    fireEvent.change(within(rule(1)).getByLabelText("When"), {
      target: { value: "title" },
    });
    fireEvent.change(within(rule(1)).getByLabelText("Text"), {
      target: { value: "" },
    });

    expect(
      (screen.getByRole("button", { name: "Save rules" }) as HTMLButtonElement)
        .disabled,
    ).toBe(true);
    expect(
      screen.getByText("Rule 1: a title rule needs a word to look for."),
    ).toBeTruthy();
  });

  it("keeps the edit when there is no connection and says to save again", async () => {
    setUp([], () => ({ ok: false, reason: "network" }));

    fireEvent.click(screen.getByRole("button", { name: "Add rule" }));
    fireEvent.click(screen.getByRole("button", { name: "Save rules" }));

    expect(await screen.findByText(/No connection/)).toBeTruthy();
    expect(rule(1)).toBeTruthy();
    expect(
      (screen.getByRole("button", { name: "Save rules" }) as HTMLButtonElement)
        .disabled,
    ).toBe(false);
  });
});
