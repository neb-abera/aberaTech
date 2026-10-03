/**
 * Which alarm rings: an alarm whose time has come and whose event has not
 * started, and nothing that is skipped, muted or acknowledged anywhere.
 */

import fc from "fast-check";
import { describe, expect, it } from "vitest";
import type { AlertItem, RoutineRing } from "../api";
import { dueAlarm, dueRinging } from "../ring";

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

const ring: RoutineRing = {
  key: "routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-28T08:40",
  routineId: "0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10",
  label: "Meds",
  alertAt: "2026-10-28T12:40:00Z",
  startsAt: "2026-10-28T15:40:00Z",
  acknowledged: false,
  acknowledgedAt: null,
  acknowledgedVia: null,
};

describe("dueRinging", () => {
  const none = { alerts: [], routineRings: [], mutedUntil: null };

  it("rings a routine ring from its time until it stops, titled with its label", () => {
    const state = { ...none, routineRings: [ring] };
    expect(dueRinging(state, at("2026-10-28T12:39:59Z"))).toBeNull();
    expect(dueRinging(state, at("2026-10-28T12:40:00Z"))).toEqual({
      key: ring.key,
      title: "Meds",
      alertAt: ring.alertAt,
      startsAt: ring.startsAt,
      routine: true,
    });
    expect(dueRinging(state, at("2026-10-28T15:39:59Z"))?.key).toBe(ring.key);
    expect(dueRinging(state, at("2026-10-28T15:40:00Z"))).toBeNull();
  });

  it("is the earliest due item from both lists", () => {
    const now = at("2026-10-28T12:50:00Z");
    // The ring at 12:40 is earlier than the standup's alarm at 12:45.
    expect(
      dueRinging({ ...none, alerts: [base], routineRings: [ring] }, now),
    ).toMatchObject({ key: ring.key, routine: true });
    // A ring at 12:48 is later, so the standup rings.
    const later = { ...ring, key: "later", alertAt: "2026-10-28T12:48:00Z" };
    expect(
      dueRinging({ ...none, alerts: [base], routineRings: [later] }, now),
    ).toEqual({
      key: base.key,
      title: "Standup",
      alertAt: base.alertAt,
      startsAt: base.startsAt,
      routine: false,
    });
  });

  it("never rings a ring acknowledged anywhere, or one a mute covers", () => {
    const now = at("2026-10-28T12:50:00Z");
    for (const via of ["phone", "browser", "pushover"] as const) {
      const answered = {
        ...ring,
        acknowledged: true,
        acknowledgedAt: "2026-10-28T12:41:00Z",
        acknowledgedVia: via,
      };
      expect(dueRinging({ ...none, routineRings: [answered] }, now)).toBeNull();
    }
    expect(
      dueRinging(
        { ...none, routineRings: [ring], mutedUntil: "2026-10-28T13:00:00Z" },
        now,
      ),
    ).toBeNull();
    // A mute that ended, or one that began after the ring, does not.
    expect(
      dueRinging(
        { ...none, routineRings: [ring], mutedUntil: "2026-10-28T12:30:00Z" },
        now,
      ),
    ).not.toBeNull();
  });

  it("reads a state from an older server with no routineRings", () => {
    expect(
      dueRinging(
        { alerts: [base], mutedUntil: null },
        at("2026-10-28T12:50:00Z"),
      )?.key,
    ).toBe(base.key);
  });
});
