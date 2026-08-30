import { describe, expect, it } from "vitest";
import {
  formatPace,
  formatSeconds,
  kgToLb,
  lbToKg,
  metricLabel,
  monthsFromNow,
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
