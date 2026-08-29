import { describe, expect, test } from "vitest";
import { Calendar } from "../calendar";

describe("Calendar", () => {
  test("three terms per year cycles Spring, Summer, Fall", () => {
    const cal = new Calendar({
      startTerm: "Spring",
      startYear: 2027,
      termsPerYear: 3,
    });
    expect(cal.label(0)).toBe("Spring 2027");
    expect(cal.label(1)).toBe("Summer 2027");
    expect(cal.label(2)).toBe("Fall 2027");
    expect(cal.label(3)).toBe("Spring 2028");
  });

  test("two terms per year skips Summer", () => {
    const cal = new Calendar({
      startTerm: "Spring",
      startYear: 2027,
      termsPerYear: 2,
    });
    expect(cal.label(0)).toBe("Spring 2027");
    expect(cal.label(1)).toBe("Fall 2027");
    expect(cal.label(2)).toBe("Spring 2028");
  });

  test("a term start date is the published JHU term start month", () => {
    const cal = new Calendar({
      startTerm: "Spring",
      startYear: 2027,
      termsPerYear: 3,
    });
    expect(cal.startDate(0).toISOString().slice(0, 7)).toBe("2027-01");
    expect(cal.startDate(1).toISOString().slice(0, 7)).toBe("2027-05");
    expect(cal.startDate(2).toISOString().slice(0, 7)).toBe("2027-08");
  });

  test("a term end date is roughly fifteen weeks after it starts", () => {
    const cal = new Calendar({
      startTerm: "Spring",
      startYear: 2027,
      termsPerYear: 3,
    });
    const days =
      (cal.endDate(0).getTime() - cal.startDate(0).getTime()) / 86_400_000;
    expect(days).toBeGreaterThan(100);
    expect(days).toBeLessThan(120);
  });

  test("starting in Fall rolls the year over correctly", () => {
    const cal = new Calendar({
      startTerm: "Fall",
      startYear: 2027,
      termsPerYear: 3,
    });
    expect(cal.label(0)).toBe("Fall 2027");
    expect(cal.label(1)).toBe("Spring 2028");
  });

  test("monthsBetween counts whole months", () => {
    expect(
      Calendar.monthsBetween(new Date("2027-01-01"), new Date("2032-01-01")),
    ).toBe(60);
    expect(
      Calendar.monthsBetween(new Date("2027-01-01"), new Date("2027-07-01")),
    ).toBe(6);
    expect(
      Calendar.monthsBetween(new Date("2032-01-01"), new Date("2027-01-01")),
    ).toBe(-60);
  });
});
