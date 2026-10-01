// @vitest-environment jsdom
/**
 * The calculator from its inputs: two dates and the end-date box, and a
 * date with Add or Subtract and four numbers. The arithmetic itself is
 * dateMath.test.ts.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import DateCalculator from "../DateCalculator";

beforeEach(() => {
  vi.useFakeTimers({ now: new Date(2026, 9, 1, 12, 0), toFake: ["Date"] });
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

const between = () =>
  screen.getByRole("region", { name: "Days between two dates" });
const adding = () =>
  screen.getByRole("region", { name: "Add to or subtract from a date" });

const set = (region: HTMLElement, label: string, value: string) =>
  fireEvent.change(within(region).getByLabelText(label, { exact: true }), {
    target: { value },
  });

describe("days between two dates", () => {
  it("starts on today and waits for an end date", () => {
    render(<DateCalculator />);
    expect(
      (within(between()).getByLabelText("Start date") as HTMLInputElement)
        .value,
    ).toBe("2026-10-01");
    expect(within(between()).getByText("Pick both dates.")).toBeTruthy();
  });

  it("gives every breakdown, then adds the end date when asked", async () => {
    render(<DateCalculator />);
    set(between(), "End date", "2026-11-15");

    const result = within(between()).getByRole("status", {
      name: "Days between",
    });
    expect(result.textContent).toContain("45 days");
    expect(result.textContent).toContain("0 years, 1 month, 14 days");
    expect(result.textContent).toContain("6 weeks, 3 days");
    expect(result.textContent).toContain("32 weekdays, Monday to Friday");
    expect(result.textContent).toContain(
      "1,080 hours, 64,800 minutes, 3,888,000 seconds",
    );
    expect(result.textContent).toContain(
      "From Thursday 1 October 2026 to Sunday 15 November 2026, end date not included.",
    );

    await act(async () => {
      fireEvent.click(
        within(between()).getByLabelText("Include the end date (adds 1 day)"),
      );
    });
    expect(result.textContent).toContain("46 days");
    expect(result.textContent).toContain("end date included");
  });

  it("says when the end is before the start", () => {
    render(<DateCalculator />);
    set(between(), "Start date", "2026-12-25");
    set(between(), "End date", "2026-10-01");

    expect(within(between()).getByRole("status").textContent).toContain(
      "85 days before the start",
    );
  });
});

describe("add to or subtract from a date", () => {
  it("adds months held to the month's end, then weeks and days", () => {
    render(<DateCalculator />);
    set(adding(), "Date", "2026-01-31");
    set(adding(), "Months", "1");
    const result = () =>
      within(adding()).getByRole("status", { name: "Resulting date" });
    expect(result().textContent).toContain("Saturday 28 February 2026");
    expect(result().textContent).toContain(
      "Saturday 31 January 2026 + 1 month.",
    );

    set(adding(), "Months", "");
    set(adding(), "Weeks", "6");
    set(adding(), "Days", "3");
    set(adding(), "Date", "2026-10-01");
    expect(result().textContent).toContain("Sunday 15 November 2026");
  });

  it("subtracts", async () => {
    render(<DateCalculator />);
    set(adding(), "Days", "365");
    await act(async () => {
      fireEvent.click(
        within(adding()).getByRole("button", { name: "Subtract" }),
      );
    });

    expect(within(adding()).getByRole("status").textContent).toContain(
      "Wednesday 1 October 2025",
    );
    expect(within(adding()).getByRole("status").textContent).toContain(
      "− 365 days",
    );
  });

  it("asks for whole numbers and stays inside the calendar", () => {
    render(<DateCalculator />);
    set(adding(), "Weeks", "1.5");
    expect(within(adding()).getByText("A whole number.")).toBeTruthy();
    expect(
      within(adding()).getByText("Pick a date and enter whole numbers."),
    ).toBeTruthy();

    set(adding(), "Weeks", "");
    set(adding(), "Date", "9999-12-31");
    set(adding(), "Days", "1");
    expect(
      within(adding()).getByText("Outside the years 1 to 9999."),
    ).toBeTruthy();
  });
});
