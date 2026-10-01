/**
 * A countdown's clock and its target: the time left or since, and a date
 * and time in a zone turned into the instant the server stores.
 */

import { describe, expect, it } from "vitest";
import {
  describeRemaining,
  formatRemaining,
  formatTarget,
  isoToZoned,
  knownZone,
  remaining,
  zonedToIso,
} from "../countdown";

const at = (iso: string) => Date.parse(iso);

describe("remaining", () => {
  it("splits the time left into days, hours, minutes and seconds", () => {
    const parts = remaining(
      at("2026-11-11T12:00:00Z"),
      at("2026-10-01T06:47:27Z"),
    );
    expect(parts).toEqual({
      passed: false,
      days: 41,
      hours: 5,
      minutes: 12,
      seconds: 33,
    });
    expect(formatRemaining(parts)).toBe("41 days 05:12:33");
  });

  it("counts up once the target passes, and the target second itself is not passed", () => {
    expect(
      describeRemaining(at("2026-10-01T00:00:00Z"), at("2026-10-01T00:00:00Z")),
    ).toBe("0 days 00:00:00 left");
    expect(
      describeRemaining(at("2026-10-01T00:00:00Z"), at("2026-10-02T00:00:01Z")),
    ).toBe("1 day 00:00:01 ago");
  });

  it("drops the part of a second", () => {
    expect(
      remaining(at("2026-10-01T00:00:10Z"), at("2026-10-01T00:00:00.400Z"))
        .seconds,
    ).toBe(9);
    expect(
      formatRemaining(
        remaining(at("2030-01-01T00:00:00Z"), at("2026-10-01T00:00:00Z")),
      ),
    ).toBe("1,188 days 00:00:00");
  });
});

describe("zonedToIso", () => {
  it.each([
    ["2027-03-01", "09:00", "Asia/Amman", "2027-03-01T09:00:00+03:00"],
    ["2027-01-10", "08:00", "America/New_York", "2027-01-10T08:00:00-05:00"],
    ["2027-07-10", "08:00", "America/New_York", "2027-07-10T08:00:00-04:00"],
    ["2027-03-01", "00:00", "UTC", "2027-03-01T00:00:00+00:00"],
    ["2027-03-01", "23:59", "Asia/Kolkata", "2027-03-01T23:59:00+05:30"],
    // 02:30 does not happen in New York that night: the offset before the change.
    ["2026-03-08", "02:30", "America/New_York", "2026-03-08T02:30:00-05:00"],
  ])("%s %s in %s is %s", (date, time, zone, iso) => {
    expect(zonedToIso(date, time, zone)).toBe(iso);
  });

  it("refuses a bad date, a bad time and an unknown zone", () => {
    expect(zonedToIso("2027-02-30", "09:00", "UTC")).toBeNull();
    expect(zonedToIso("", "09:00", "UTC")).toBeNull();
    expect(zonedToIso("2027-03-01", "25:00", "UTC")).toBeNull();
    expect(zonedToIso("2027-03-01", "9", "UTC")).toBeNull();
    expect(zonedToIso("2027-03-01", "09:00", "Mars/Olympus")).toBeNull();
    expect(knownZone("Mars/Olympus")).toBe(false);
    expect(knownZone("Asia/Amman")).toBe(true);
  });

  it("writes an offset with seconds as the same instant in UTC", () => {
    // Amman kept local mean time, +2:23:44, until 1931.
    expect(zonedToIso("1920-01-01", "12:00", "Asia/Amman")).toBe(
      "1920-01-01T09:36:16.000Z",
    );
  });
});

describe("isoToZoned and formatTarget", () => {
  it("gives the form back the date and time it was saved with", () => {
    expect(isoToZoned("2027-03-01T06:00:00+00:00", "Asia/Amman")).toEqual({
      date: "2027-03-01",
      time: "09:00",
    });
    expect(isoToZoned("2027-01-10T13:00:00Z", "America/New_York")).toEqual({
      date: "2027-01-10",
      time: "08:00",
    });
    expect(isoToZoned("2027-01-10T00:30:00Z", "UTC")).toEqual({
      date: "2027-01-10",
      time: "00:30",
    });
  });

  it("writes the target with its year in its own zone", () => {
    expect(formatTarget("2027-03-01T06:00:00Z", "Asia/Amman")).toBe(
      "Mon, Mar 1, 2027, 9:00 AM GMT+3",
    );
    expect(formatTarget("2027-03-01T06:00:00Z", "Mars/Olympus")).toBe(
      "Mon, Mar 1, 2027, 6:00 AM UTC",
    );
  });
});
