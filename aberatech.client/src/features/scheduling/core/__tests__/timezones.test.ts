import { describe, expect, it } from "vitest";
import { zoneOptions } from "../timezones";

describe("zoneOptions", () => {
  it("offers the real IANA list, not a hand-typed subset", () => {
    const zones = zoneOptions();

    expect(zones).toContain("America/New_York");
    expect(zones).toContain("Europe/Berlin");
    expect(zones.length).toBeGreaterThan(100);
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
