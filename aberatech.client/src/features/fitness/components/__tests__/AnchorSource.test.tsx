import { describe, expect, it } from "vitest";
import type { AnchorInfo, Summary } from "../../core/api";
import { anchorSource } from "../ProjectionPanel";

const settings = {
  startVdot: 35.4,
  vdotMeasuredOn: "2026-04-03",
} as unknown as Summary["settings"];

const anchor = (over: Partial<AnchorInfo>): AnchorInfo => ({
  vdot: 36,
  on: "2026-11-20",
  kind: "Recording",
  name: "5 mile time trial",
  distanceMeters: 5 * 1609.344,
  seconds: 2050,
  altitudeMeters: 15,
  floor: false,
  why: "",
  ...over,
});

describe("anchorSource", () => {
  it("names the test, its distance, time, date, and altitude", () => {
    expect(anchorSource({ settings, anchor: anchor({}) })).toBe(
      " (from your 5 mile time trial: 5 mile in 34:10 on 2026-11-20 at 15 m (49 ft))",
    );
  });

  it("reads an AFT 2-mile as a floor", () => {
    const text = anchorSource({
      settings,
      anchor: anchor({
        kind: "Aft2Mile",
        name: "AFT 2-mile",
        distanceMeters: 2 * 1609.344,
        seconds: 1009,
        on: "2026-04-03",
        altitudeMeters: 1186,
        floor: true,
      }),
    });
    expect(text).toContain(
      "at least, from your AFT 2-mile: 2 mile in 16:49 on 2026-04-03 at 1,186 m",
    );
    expect(text).toContain("four other events");
  });

  it("says when the anchor was typed or is the default", () => {
    expect(
      anchorSource({
        settings,
        anchor: anchor({
          kind: "TypedVdot",
          distanceMeters: null,
          seconds: null,
          on: "2026-04-03",
        }),
      }),
    ).toBe(" (entered as a VDOT, measured on 2026-04-03)");
    expect(
      anchorSource({
        settings: { ...settings, vdotMeasuredOn: null },
        anchor: null,
      }),
    ).toContain("a default, not a measurement");
  });
});
