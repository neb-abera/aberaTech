import { describe, expect, it } from "vitest";
import {
  addMonths,
  longDayLabel,
  monthGrid,
  monthLabel,
  monthOf,
} from "../month";

describe("monthGrid", () => {
  it("pads so the first lands in the right column", () => {
    // 1 June 2027 is a Tuesday, so two blanks precede it in a Sunday-first grid.
    const cells = monthGrid(2027, 6);

    expect(cells.slice(0, 2).every((cell) => cell.date === null)).toBe(true);
    expect(cells[2]).toEqual({ date: "2027-06-01", dayOfMonth: 1 });
  });

  it("starts with no blanks when the first is a Sunday", () => {
    // 1 August 2027 is a Sunday.
    expect(monthGrid(2027, 8)[0]).toEqual({
      date: "2027-08-01",
      dayOfMonth: 1,
    });
  });

  it("has the right number of days for each month", () => {
    const days = (year: number, month: number) =>
      monthGrid(year, month).filter((cell) => cell.date).length;

    expect(days(2027, 6)).toBe(30);
    expect(days(2027, 7)).toBe(31);
    expect(days(2027, 2)).toBe(28);
    // 2028 is a leap year — the case a hand-rolled grid gets wrong.
    expect(days(2028, 2)).toBe(29);
  });

  it("zero pads so the keys match the ISO dates the server sends", () => {
    const cells = monthGrid(2027, 6).filter((cell) => cell.date);

    expect(cells[0].date).toBe("2027-06-01");
    expect(cells[8].date).toBe("2027-06-09");
  });
});

describe("addMonths", () => {
  it("moves within a year", () => {
    expect(addMonths(2027, 6, 1)).toEqual({ year: 2027, month: 7 });
    expect(addMonths(2027, 6, -1)).toEqual({ year: 2027, month: 5 });
  });

  it("rolls over the year boundary in both directions", () => {
    expect(addMonths(2027, 12, 1)).toEqual({ year: 2028, month: 1 });
    expect(addMonths(2027, 1, -1)).toEqual({ year: 2026, month: 12 });
  });

  it("handles more than a year at once", () => {
    expect(addMonths(2027, 6, 14)).toEqual({ year: 2028, month: 8 });
    expect(addMonths(2027, 6, -14)).toEqual({ year: 2026, month: 4 });
  });
});

describe("labels", () => {
  it("names the month and the day without shifting by a timezone", () => {
    expect(monthLabel(2027, 6)).toBe("June 2027");
    expect(monthOf("2027-06-01")).toEqual({ year: 2027, month: 6 });
    expect(longDayLabel("2027-06-01")).toBe("Tuesday, June 1");
  });
});
