/**
 * Which alarm rings: an alarm whose time has come and whose event has not
 * started, and nothing that is skipped, muted or acknowledged anywhere.
 */

import fc from "fast-check";
import { describe, expect, it } from "vitest";
import type { AlertItem } from "../api";
import { dueAlarm } from "../ring";

const base: AlertItem = {
  key: "k",
  title: "Standup",
  location: null,
  startsAt: "2026-10-28T13:00:00Z",
  alertAt: "2026-10-28T12:45:00Z",
  source: "reminder",
  skipped: false,
  muted: false,
  critical: true,
  type: "alarm",
  typeFrom: "critical",
  acknowledged: false,
  acknowledgedAt: null,
  acknowledgedVia: null,
  recurring: false,
  endsAt: "2026-10-28T13:30:00Z",
};

const at = (iso: string) => Date.parse(iso);

describe("dueAlarm", () => {
  it("is the alarm from its time up to, not including, its start", () => {
    expect(dueAlarm([base], at("2026-10-28T12:44:59Z"))).toBeNull();
    expect(dueAlarm([base], at("2026-10-28T12:45:00Z"))).toBe(base);
    expect(dueAlarm([base], at("2026-10-28T12:59:59Z"))).toBe(base);
    expect(dueAlarm([base], at("2026-10-28T13:00:00Z"))).toBeNull();
  });

  it("is the earliest when two are due", () => {
    const earlier = { ...base, key: "e", alertAt: "2026-10-28T12:40:00Z" };
    expect(dueAlarm([base, earlier], at("2026-10-28T12:50:00Z"))).toBe(earlier);
  });

  it("never rings for anything skipped, muted, acknowledged or not an alarm", () => {
    fc.assert(
      fc.property(
        fc.record({
          skipped: fc.boolean(),
          muted: fc.boolean(),
          acknowledged: fc.boolean(),
          type: fc.constantFrom("none", "notification", "alarm"),
          offset: fc.integer({ min: -3_600_000, max: 3_600_000 }),
        }),
        ({ skipped, muted, acknowledged, type, offset }) => {
          const alert = {
            ...base,
            skipped,
            muted,
            acknowledged,
            type,
          } as AlertItem;
          const now = at(base.alertAt) + offset;
          const rings = dueAlarm([alert], now) !== null;
          const should =
            !skipped &&
            !muted &&
            !acknowledged &&
            type === "alarm" &&
            now >= at(base.alertAt) &&
            now < at(base.startsAt);
          expect(rings).toBe(should);
        },
      ),
    );
  });
});
