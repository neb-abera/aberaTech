/**
 * The words and arithmetic behind the settings: the gap between sounds,
 * when an emergency message stops, and the page's summary per priority.
 */

import { expect, it, describe as suite } from "vitest";
import { settings } from "../../../../test/alertsFixtures";
import { describe, every, stopWork } from "../settings";

suite("every", () => {
  it("reads as a person would say it", () => {
    expect(every(60)).toBe("minute");
    expect(every(120)).toBe("2 minutes");
    expect(every(30)).toBe("30 seconds");
    expect(every(90)).toBe("90 seconds");
  });
});

suite("stopWork", () => {
  it("stops at the fiftieth sound when that comes first", () => {
    expect(stopWork(60, 180, 50)).toEqual({
      minutes: 50,
      text: "Stops after 50 min: 50 sounds × 60 s = 50 min, before the 180 min limit.",
    });
  });

  it("stops at the limit when that comes first, or ties with it", () => {
    expect(stopWork(120, 30, 50).minutes).toBe(30);
    expect(stopWork(60, 50, 50)).toEqual({
      minutes: 50,
      text: "Stops after 50 min: the limit comes before 50 sounds × 60 s = 50 min.",
    });
  });
});

suite("describe", () => {
  it("follows the priority", () => {
    expect(describe(settings, 50, "UTC")).toContain(
      "again every minute until you acknowledge it in the Pushover app, for up to 50 minutes.",
    );
    expect(describe({ ...settings, priority: 1 }, 50, "UTC")).toContain(
      "one sound that plays through Pushover's quiet hours",
    );
    expect(describe({ ...settings, priority: 0 }, 50, "UTC")).toContain(
      "The phone's Pushover settings decide how it plays.",
    );
    expect(describe(settings, 50, "Asia/Amman")).toContain(
      "Times are in Asia/Amman, the calendar's own zone.",
    );
  });
});
