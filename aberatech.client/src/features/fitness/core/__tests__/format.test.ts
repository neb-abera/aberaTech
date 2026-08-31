import { describe, expect, it } from "vitest";
import {
  dateInMonths,
  formatChance,
  formatDistance,
  formatPace,
  formatSeconds,
  fromMeters,
  goalKey,
  kgToLb,
  lbToKg,
  metricLabel,
  monthsFromNow,
  monthsUntil,
  parseClock,
  toMeters,
} from "../format";

describe("formatSeconds", () => {
  it("renders race times the way runners read them", () => {
    expect(formatSeconds(735)).toBe("12:15");
    expect(formatSeconds(1009)).toBe("16:49");
    expect(formatSeconds(2040)).toBe("34:00");
  });

  it("rolls into hours for long effort", () => {
    expect(formatSeconds(2 * 3600 + 30 * 60)).toBe("2:30:00");
  });

  it("rounds fractional seconds instead of truncating", () => {
    expect(formatSeconds(734.6)).toBe("12:15");
  });
});

describe("formatPace", () => {
  it("labels seconds-per-km as a pace", () => {
    expect(formatPace(390)).toBe("6:30/km");
  });
});

describe("weight conversion", () => {
  it("roundtrips pounds through kilograms", () => {
    expect(kgToLb(lbToKg(174))).toBeCloseTo(174, 8);
  });
});

describe("monthsFromNow", () => {
  it("anchors at a fixed date for testability", () => {
    const from = new Date(2026, 8, 1); // Sep 2026
    expect(monthsFromNow(6, from)).toBe("Mar 2027");
    expect(monthsFromNow(18, from)).toBe("Mar 2028");
  });
});

describe("metricLabel", () => {
  it("names the known metrics and passes unknown ones through", () => {
    expect(metricLabel("run-2mi")).toBe("2-mile run");
    expect(metricLabel("row-500m")).toBe("row-500m");
  });
});

describe("inputs an athlete actually types", () => {
  it("reads a clock in any of the shapes a time gets written", () => {
    expect(parseClock("12:15")).toBe(735);
    expect(parseClock("1:02:30")).toBe(3750);
    expect(parseClock(" 735 ")).toBe(735);
    expect(parseClock("7:30.5")).toBeCloseTo(450.5);
  });

  it("refuses what is not a time rather than guessing at it", () => {
    expect(parseClock("")).toBeNull();
    expect(parseClock("soon")).toBeNull();
    expect(parseClock("1:2:3:4")).toBeNull();
    expect(parseClock("-5:00")).toBeNull();
    expect(parseClock("0")).toBeNull();
  });

  it("converts distances both ways without drift", () => {
    expect(toMeters(5, "mi")).toBeCloseTo(8046.72);
    expect(toMeters(10, "km")).toBe(10000);
    expect(fromMeters(toMeters(3.7, "mi"), "mi")).toBeCloseTo(3.7);
  });

  it("names a distance in the unit it was probably meant in", () => {
    expect(formatDistance(1.5 * 1609.344)).toBe("1.5 mi");
    expect(formatDistance(10000)).toBe("10 km");
    expect(formatDistance(3000)).toBe("3 km");
    expect(formatDistance(457)).toBe("457 m");
  });

  it("is honest about probabilities at both ends", () => {
    expect(formatChance(0.0032)).toBe("<1%");
    expect(formatChance(0.5)).toBe("50%");
    expect(formatChance(0.999)).toBe(">99%");
  });

  it("round-trips a deadline between months and a date", () => {
    const from = new Date("2026-08-31T12:00:00");
    expect(monthsUntil(dateInMonths(18, from), from)).toBeCloseTo(18, 0);
  });

  it("keys a goal by its distance, so any distance can be one", () => {
    expect(goalKey(8046.72)).toBe("run-8047m");
  });
});
