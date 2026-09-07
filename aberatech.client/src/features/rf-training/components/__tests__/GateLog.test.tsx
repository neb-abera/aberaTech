// @vitest-environment jsdom
/**
 * A gate's log, from the page: log a pass with a time, log a fail, see the
 * summary change, remove one.
 */

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Attempt } from "../../core/gates";
import GateLog from "../GateLog";

afterEach(cleanup);

const fixed = () => new Date(2027, 0, 19, 9, 0);

function mount(attempts: Attempt[] = [], readOnly = false) {
  const add = vi.fn();
  const remove = vi.fn();
  render(
    <GateLog
      blockId="wire"
      gate="From a bare spool to a contact in under thirty minutes."
      attempts={attempts}
      add={add}
      remove={remove}
      readOnly={readOnly}
      now={fixed}
    />,
  );
  return { add, remove };
}

describe("a gate's log", () => {
  it("shows a visitor the gate and nothing of the log", () => {
    mount([{ id: "a", on: "2027-01-05", passed: true, minutes: 41 }], true);
    expect(
      screen.getByText(
        "From a bare spool to a contact in under thirty minutes.",
      ),
    ).toBeTruthy();
    expect(screen.queryByText(/passed/)).toBeNull();
    expect(screen.queryByText(/2027-01-05/)).toBeNull();
    expect(screen.queryByRole("button", { name: "Log a pass" })).toBeNull();
  });

  it("says so when nothing has been attempted", () => {
    mount();
    expect(screen.getByText("No attempts yet.")).toBeTruthy();
  });

  it("logs a pass with today's date, the minutes and the note", () => {
    const { add } = mount();

    fireEvent.change(screen.getByRole("textbox", { name: "Minutes" }), {
      target: { value: "27" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Note" }), {
      target: { value: "headlamp, drizzle" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Log a pass" }));

    expect(add).toHaveBeenCalledWith("wire", {
      id: expect.any(String),
      on: "2027-01-19",
      passed: true,
      minutes: 27,
      note: "headlamp, drizzle",
    });
  });

  it("logs a fail on a chosen date without a time", () => {
    const { add } = mount();

    fireEvent.change(screen.getByLabelText("Date"), {
      target: { value: "2027-01-05" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Log a fail" }));

    expect(add).toHaveBeenCalledWith("wire", {
      id: expect.any(String),
      on: "2027-01-05",
      passed: false,
    });
  });

  it("summarizes the attempts and lets one be removed", () => {
    const { remove } = mount([
      { id: "first", on: "2027-01-05", passed: false, minutes: 41 },
      {
        id: "second",
        on: "2027-01-19",
        passed: true,
        minutes: 27,
        note: "headlamp",
      },
    ]);

    expect(
      screen.getByText(
        "1 of 2 passed. Best 27 min on 2027-01-19. Latest: pass on 2027-01-19.",
      ),
    ).toBeTruthy();
    expect(
      screen.getByText("2027-01-19: pass, 27 min (headlamp)"),
    ).toBeTruthy();

    fireEvent.click(
      screen.getByRole("button", { name: "Remove attempt on 2027-01-05" }),
    );
    expect(remove).toHaveBeenCalledWith("wire", "first");
  });
});
