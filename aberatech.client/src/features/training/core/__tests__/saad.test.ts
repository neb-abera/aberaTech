import { describe, expect, it } from "vitest";
import { currentBand, levelOf, mix, type PoolProblem, pickMix } from "../saad";

describe("a Codeforces rating as a Saad level", () => {
  it("is each Div. 2 letter's midpoint at its median rating", () => {
    expect(levelOf(800)).toBe(1.5);
    expect(levelOf(1200)).toBe(2.25);
    expect(levelOf(1600)).toBe(4.25);
    expect(levelOf(1900)).toBe(5.75);
    expect(levelOf(2300)).toBe(6.75);
  });

  it("runs in a straight line between letters, and stays on his scale", () => {
    expect(levelOf(1000)).toBeCloseTo(1.875);
    expect(levelOf(500)).toBe(1.5);
    expect(levelOf(2700)).toBeCloseTo(7.75);
    expect(levelOf(9000)).toBe(9.75);
  });
});

describe("the current band", () => {
  it("comes from the open band unit first, then the rating, then 800", () => {
    expect(
      currentBand([{ id: "band-1100", title: "Band 1100" }], 1400).rating,
    ).toBe(1100);
    expect(
      currentBand([{ id: "silver-1", title: "Silver" }], 1400).rating,
    ).toBe(1400);
    expect(currentBand([], null)).toEqual({
      rating: 800,
      source: "no band open and no rating yet, so Div. 2 A's 800",
    });
  });
});

/** A problem at every tenth of a level from 1 to 10. */
const pool: PoolProblem[] = Array.from({ length: 91 }, (_, i) => ({
  code: `CF${i + 1}-D2-A`,
  level: Math.round((1 + i * 0.1) * 10) / 10,
  importance: 3 + (i % 3),
  url: null,
  codeforces: `${i + 1}A`,
  editorial: false,
}));

describe("Saad's mix", () => {
  it("picks ten: 1 below, 2 at, 6 slightly above, 1 far above", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set());
    expect(picks).toHaveLength(10);
    const count = (band: string) =>
      picks.filter((pick) => pick.band === band).length;
    expect(mix.map((band) => [band.band, count(band.band)])).toEqual([
      ["below", 1],
      ["at", 2],
      ["above", 6],
      ["far", 1],
    ]);
    for (const pick of picks) {
      const offset = pick.problem.level - 5;
      const band = mix.find((each) => each.band === pick.band);
      if (!band) throw new Error(pick.band);
      expect(offset).toBeGreaterThanOrEqual(band.from);
      expect(offset).toBeLessThanOrEqual(band.to);
      expect(pick.nearest).toBe(false);
    }
  });

  it("puts higher importance first in a band", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set());
    const at = picks.filter((pick) => pick.band === "at");
    // Levels 4.8 to 5.2 are at; their importances are 5, 3, 4, 5, 3.
    expect(at.map((pick) => pick.problem.importance)).toEqual([5, 5]);
  });

  it("leaves out what is solved, by code or Codeforces id", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set(["17A", "CF18-D2-A"]));
    const codes = picks.map((pick) => pick.problem.code);
    expect(codes).not.toContain("CF17-D2-A");
    expect(codes).not.toContain("CF18-D2-A");
  });

  it("fills a short band with the nearest level and says so", () => {
    // At level 1 nothing is below.
    const picks = pickMix(pool, 1, "2026-11-09", new Set());
    const below = picks.find((pick) => pick.band === "below");
    expect(below?.nearest).toBe(true);
    expect(picks).toHaveLength(10);
    expect(new Set(picks.map((pick) => pick.problem.code)).size).toBe(10);
  });

  it("holds its order for a week and changes the next", () => {
    const flat = pool.map((problem) => ({ ...problem, importance: 3 }));
    const week = (start: string) =>
      pickMix(flat, 5, start, new Set()).map((pick) => pick.problem.code);
    expect(week("2026-11-09")).toEqual(week("2026-11-09"));
    expect(week("2026-11-16")).not.toEqual(week("2026-11-09"));
  });
});
