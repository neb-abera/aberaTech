/**
 * The calculator's arithmetic. The table is the one the phone app's
 * tests hold too, so the page and the phone give the same answers.
 */

import fc from "fast-check";
import { describe, expect, it } from "vitest";
import {
  addMonths,
  addToDate,
  between,
  dayNumber,
  describeDate,
  formatDate,
  fromDayNumber,
  parseDate,
  plural,
  weekday,
} from "../dateMath";

const date = (text: string) => {
  const parsed = parseDate(text);
  if (!parsed) throw new Error(`bad date ${text}`);
  return parsed;
};

describe("between", () => {
  it.each([
    ["2026-10-01", "2026-11-15", false, 45, 0, 1, 14, 6, 3, 32],
    ["2026-10-01", "2026-11-15", true, 46, 0, 1, 15, 6, 4, 32],
    ["2026-01-31", "2026-03-01", false, 29, 0, 1, 1, 4, 1, 20],
    ["2024-02-29", "2025-02-28", false, 365, 1, 0, 0, 52, 1, 261],
    ["2024-02-29", "2028-02-29", false, 1461, 4, 0, 0, 208, 5, 1043],
    ["2026-12-25", "2026-10-01", false, -85, 0, 2, 24, 12, 1, 61],
    ["2026-10-01", "2026-10-01", false, 0, 0, 0, 0, 0, 0, 0],
    ["2026-10-01", "2026-10-01", true, 1, 0, 0, 1, 0, 1, 1],
    ["2026-03-01", "2026-03-31", true, 31, 0, 1, 0, 4, 3, 22],
    ["1991-10-07", "2026-10-01", false, 12778, 34, 11, 24, 1825, 3, 9128],
  ] as const)(
    "%s to %s, end included %s: %i days",
    (start, end, includeEnd, total, years, months, days, weeks, rest, weekdays) => {
      const result = between(date(start), date(end), includeEnd);
      expect(result).toEqual({
        totalDays: total,
        before: total < 0,
        years,
        months,
        days,
        weeks,
        weekRemainder: rest,
        weekdays,
      });
    },
  );
});

describe("addToDate", () => {
  it.each([
    ["2026-01-31", 0, 1, 0, 0, "2026-02-28", "Saturday"],
    ["2024-02-29", 1, 0, 0, 0, "2025-02-28", "Friday"],
    ["2026-10-01", 0, 0, 6, 3, "2026-11-15", "Sunday"],
    ["2026-10-01", 0, -1, 0, -1, "2026-08-31", "Monday"],
    ["2026-10-01", 0, 0, 0, -365, "2025-10-01", "Wednesday"],
    ["2026-03-31", 0, 6, 0, 0, "2026-09-30", "Wednesday"],
  ] as const)(
    "%s + %iy %im %iw %id is %s",
    (start, years, months, weeks, days, result, name) => {
      const moved = addToDate(date(start), { years, months, weeks, days });
      expect(moved && formatDate(moved)).toBe(result);
      expect(moved && describeDate(moved)).toMatch(new RegExp(`^${name} `));
    },
  );

  it("refuses a result outside the years 1 to 9999", () => {
    const zero = { years: 0, months: 0, weeks: 0 };
    expect(addToDate(date("9999-12-31"), { ...zero, days: 1 })).toBeNull();
    expect(addToDate(date("0001-01-01"), { ...zero, days: -1 })).toBeNull();
    expect(addToDate(date("0001-01-01"), { ...zero, days: 0 })).toEqual(
      date("0001-01-01"),
    );
  });
});

describe("parseDate", () => {
  it.each([
    "2026-02-29",
    "2026-13-01",
    "2026-00-10",
    "26-10-01",
    "",
    "x",
    "0000-01-01",
  ])("refuses %j", (text) => {
    expect(parseDate(text)).toBeNull();
  });

  it("takes a leap day and the edges of the calendar", () => {
    expect(parseDate("2024-02-29")).toEqual({ year: 2024, month: 2, day: 29 });
    expect(parseDate("0001-01-01")).toEqual({ year: 1, month: 1, day: 1 });
    expect(parseDate(" 9999-12-31 ")).toEqual({
      year: 9999,
      month: 12,
      day: 31,
    });
  });
});

describe("describeDate and plural", () => {
  it("writes the weekday and the month out", () => {
    expect(describeDate(date("2026-10-01"))).toBe("Thursday 1 October 2026");
    expect(weekday(date("1970-01-01"))).toBe(4);
    expect(weekday(date("0001-01-01"))).toBe(1);
  });

  it("says 1 day and 1,080 hours", () => {
    expect(plural(1, "day")).toBe("1 day");
    expect(plural(0, "year")).toBe("0 years");
    expect(plural(1080, "hour")).toBe("1,080 hours");
  });
});

const dates = fc
  .integer({
    min: dayNumber(date("1900-01-01")),
    max: dayNumber(date("2200-12-31")),
  })
  .map(fromDayNumber);

describe("properties", () => {
  it("a date survives the round trip through its day number and its text", () => {
    fc.assert(
      fc.property(dates, (d) => {
        expect(fromDayNumber(dayNumber(d))).toEqual(d);
        expect(parseDate(formatDate(d))).toEqual(d);
      }),
    );
  });

  it("swapping the dates flips the sign and keeps the parts", () => {
    fc.assert(
      fc.property(dates, dates, fc.boolean(), (a, b, include) => {
        const forward = between(a, b, include);
        const back = between(b, a, include);
        if (dayNumber(a) === dayNumber(b)) return;
        expect(back.totalDays).toBe(-forward.totalDays);
        expect([back.years, back.months, back.days]).toEqual([
          forward.years,
          forward.months,
          forward.days,
        ]);
      }),
    );
  });

  it("the parts add back up to the end, and the weekdays fit the span", () => {
    fc.assert(
      fc.property(dates, dates, (a, b) => {
        const [from, to] = dayNumber(a) <= dayNumber(b) ? [a, b] : [b, a];
        const result = between(from, to, false);
        const rebuilt = addToDate(from, {
          years: result.years,
          months: result.months,
          weeks: 0,
          days: result.days,
        });
        expect(rebuilt).toEqual(to);
        expect(result.weeks * 7 + result.weekRemainder).toBe(result.totalDays);
        expect(result.weekdays).toBeLessThanOrEqual(result.totalDays);
        expect(result.weekdays).toBeGreaterThanOrEqual(result.weeks * 5);
        // The days left over never make another whole month.
        expect(
          dayNumber(addMonths(from, result.years * 12 + result.months + 1)),
        ).toBeGreaterThan(dayNumber(to));
      }),
    );
  });

  it("adding days and counting them back agree", () => {
    fc.assert(
      fc.property(
        dates,
        fc.integer({ min: -40_000, max: 40_000 }),
        (d, days) => {
          const moved = addToDate(d, { years: 0, months: 0, weeks: 0, days });
          if (!moved) return;
          expect(between(d, moved, false).totalDays).toBe(days);
        },
      ),
    );
  });
});
