import { describe, expect, it } from "vitest";
import type { Summary } from "../../core/api";
import { anchorSource } from "../ProjectionPanel";

const settings = {
  startVdot: 35.4,
  vdotMeasuredOn: "2026-04-03",
} as unknown as Summary["settings"];

describe("anchorSource", () => {
  it("names the race the anchor came from", () => {
    expect(
      anchorSource({
        ...settings,
        anchorDistanceMeters: 3218.688,
        anchorSeconds: 1009,
      }),
    ).toBe(" (from a 2 mi run in 16:49 on 2026-04-03)");
  });

  it("says when the anchor was typed as a VDOT", () => {
    expect(anchorSource(settings)).toBe(
      " (entered as a VDOT, measured on 2026-04-03)",
    );
  });

  it("says when there is no measurement at all", () => {
    expect(anchorSource({ ...settings, vdotMeasuredOn: null })).toBe(
      " (a default, not a measurement)",
    );
  });
});
