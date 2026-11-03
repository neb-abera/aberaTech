import { describe, expect, it } from "vitest";
import { usFirst, usZones, zoneOptions } from "../timezones";

describe("zoneOptions", () => {
  it("offers the real IANA list, not a hand-typed subset", () => {
    const zones = zoneOptions();

    expect(zones).toContain("America/New_York");
    expect(zones).toContain("Europe/Berlin");
    expect(zones.length).toBeGreaterThan(100);
  });

  it("starts with the United States zones, east to west", () => {
    const zones = zoneOptions();

    expect(zones.slice(0, 7)).toEqual([
      "America/New_York",
      "America/Chicago",
      "America/Denver",
      "America/Phoenix",
      "America/Los_Angeles",
      "America/Anchorage",
      "Pacific/Honolulu",
    ]);
    expect(zones.slice(0, usZones.length)).toEqual(usZones);
    // The rest follow once each, in the browser's order.
    expect(zones.filter((zone) => zone === "America/Chicago")).toHaveLength(1);
    expect(zones.indexOf("Africa/Abidjan")).toBe(usZones.length);
  });

  it("keeps a saved zone selectable even when the list lacks it", () => {
    // A value written by a browser with a newer tzdb must not vanish from the
    // dropdown here, because a vanished value is a value silently replaced.
    const zones = zoneOptions("Custom/Somewhere");

    expect(zones[0]).toBe("Custom/Somewhere");
    expect(zones).toContain("America/New_York");
  });

  it("does not duplicate a saved zone the list already has", () => {
    const zones = zoneOptions("America/New_York");

    expect(zones.filter((zone) => zone === "America/New_York")).toHaveLength(1);
  });
});

describe("usFirst", () => {
  it("puts the United States zones first and keeps the rest in order", () => {
    expect(usFirst(["Asia/Amman", "America/Chicago", "Europe/Berlin"])).toEqual(
      [...usZones, "Asia/Amman", "Europe/Berlin"],
    );
  });
});
