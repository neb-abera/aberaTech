import { describe, expect, it } from "vitest";
import { ATHLETIC, EVOKE } from "../doses";
import { planFromPreset } from "../PlanEditor";

describe("planFromPreset", () => {
  it("opens on 8 h of running and 3 h of strength, split near 80% easy", () => {
    const plan = planFromPreset(ATHLETIC);
    expect(plan.totalHours).toBe(8);
    expect(plan.strengthHours).toBe(3);
    expect(plan.easyHours + plan.thresholdHours + plan.intervalHours).toBe(8);
    expect(plan.easyHours / plan.totalHours).toBeGreaterThanOrEqual(0.8);
  });

  it("gives the Evoke plan's base hours to easy running", () => {
    const plan = planFromPreset(EVOKE, "zones");
    expect(plan.easyHours).toBe(plan.totalHours);
    expect(plan.thresholdHours + plan.intervalHours).toBe(0);
    expect(plan.strengthHours).toBe(2.5);
  });
});

describe("written plans", () => {
  it("marks only the Evoke plan as written, so only it takes compliance", () => {
    expect(planFromPreset(EVOKE).written).toBe(true);
    expect(planFromPreset(ATHLETIC).written).toBe(false);
  });
});
