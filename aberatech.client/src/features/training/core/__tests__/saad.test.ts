import { describe, expect, it } from "vitest";
import {
  type BandProblem,
  currentBand,
  levelOf,
  mix,
  type Pick,
  type PoolProblem,
  pickMix,
  poolStart,
  ratingOf,
} from "../saad";

const fromSaad = (picks: Pick[]) =>
  picks.flatMap((pick) => (pick.source === "saad" ? [pick] : []));
const fromBand = (picks: Pick[]) =>
  picks.flatMap((pick) => (pick.source === "codeforces" ? [pick] : []));
const idOf = (pick: Pick) =>
  pick.source === "saad" ? pick.problem.code : pick.problem.id;

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
  url: `https://codeforces.com/contest/${i + 1}/problem/A`,
  codeforces: `${i + 1}A`,
  editorial: false,
}));

describe("Saad's mix", () => {
  it("picks ten: 1 below, 2 at, 6 slightly above, 1 far above", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set());
    expect(fromSaad(picks)).toHaveLength(10);
    const count = (band: string) =>
      picks.filter((pick) => pick.band === band).length;
    expect(mix.map((band) => [band.band, count(band.band)])).toEqual([
      ["below", 1],
      ["at", 2],
      ["above", 6],
      ["far", 1],
    ]);
    for (const pick of fromSaad(picks)) {
      const offset = pick.problem.level - 5;
      const band = mix.find((each) => each.band === pick.band);
      if (!band) throw new Error(pick.band);
      expect(offset).toBeGreaterThanOrEqual(band.from);
      expect(offset).toBeLessThanOrEqual(band.to);
    }
  });

  it("puts higher importance first in a band", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set());
    const at = fromSaad(picks).filter((pick) => pick.band === "at");
    // Levels 4.8 to 5.2 are at; their importances are 5, 3, 4, 5, 3.
    expect(at.map((pick) => pick.problem.importance)).toEqual([5, 5]);
  });

  it("leaves out what is solved, by code or Codeforces id", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set(["17A", "CF18-D2-A"]));
    const codes = picks.map(idOf);
    expect(codes).not.toContain("CF17-D2-A");
    expect(codes).not.toContain("CF18-D2-A");
  });

  it("takes nothing from his pool without a working link", () => {
    const unlinked = pool.map((problem) => ({ ...problem, url: null }));
    expect(pickMix(unlinked, 5, "2026-11-09", new Set())).toEqual([]);
  });

  it("holds its order for a week and changes the next", () => {
    const flat = pool.map((problem) => ({ ...problem, importance: 3 }));
    const week = (start: string) =>
      pickMix(flat, 5, start, new Set()).map(idOf);
    expect(week("2026-11-09")).toEqual(week("2026-11-09"));
    expect(week("2026-11-16")).not.toEqual(week("2026-11-09"));
  });
});

/** Codeforces problems rated 800 to 1000, as the band endpoint sends them. */
const band: BandProblem[] = Array.from({ length: 12 }, (_, i) => ({
  id: `${2250 + i}A`,
  name: `Problem ${i}`,
  rating: 800 + (i % 3) * 100,
  url: `https://codeforces.com/problemset/problem/${2250 + i}/A`,
}));

/**
 * His pool as the live sheet has it on 9 Nov 2026: one row below level 3,
 * from LiveArchive with no link, then problems from level 3 up.
 */
const live: PoolProblem[] = [
  {
    code: "LIVEARCHIVE 2191",
    level: 2,
    importance: 4,
    url: null,
    codeforces: null,
    editorial: false,
  },
  ...Array.from({ length: 30 }, (_, i) => ({
    code: `CF${600 + i}-D2-C`,
    level: 3 + Math.floor(i / 5) * 0.5,
    importance: 3 + (i % 3),
    url: `https://codeforces.com/contest/${600 + i}/problem/C`,
    codeforces: `${600 + i}C`,
    editorial: false,
  })),
];

describe("Saad's mix below his pool's range", () => {
  it("at level 1.5, fills the slots his pool cannot from the Codeforces band, never from far above", () => {
    const picks = pickMix(live, levelOf(800), "2026-11-09", new Set(), band);
    expect(picks).toHaveLength(10);
    const saad = fromSaad(picks);
    // Only far above fits: levels 3 to 4.5 around 1.5.
    expect(saad.map((pick) => pick.band)).toEqual(["far"]);
    expect(saad[0].problem.level).toBeGreaterThanOrEqual(3);
    expect(saad[0].problem.level).toBeLessThan(4.5);
    const codeforces = fromBand(picks);
    expect(codeforces).toHaveLength(9);
    expect(codeforces.map((pick) => pick.band).sort()).toEqual([
      "above",
      "above",
      "above",
      "above",
      "above",
      "above",
      "at",
      "at",
      "below",
    ]);
    for (const pick of codeforces) {
      expect(pick.problem.rating).toBeGreaterThanOrEqual(800);
      expect(pick.problem.rating).toBeLessThanOrEqual(1000);
    }
    expect(picks.map(idOf)).not.toContain("LIVEARCHIVE 2191");
  });

  it("leaves a slot empty rather than fill it from far above when the band is not read", () => {
    const picks = pickMix(live, 1.5, "2026-11-09", new Set());
    expect(picks.map((pick) => pick.band)).toEqual(["far"]);
  });

  it("leaves out band problems already solved", () => {
    const picks = pickMix(
      live,
      1.5,
      "2026-11-09",
      new Set(["2250A", "2251A"]),
      band,
    );
    const ids = picks.map(idOf);
    expect(ids).not.toContain("2250A");
    expect(ids).not.toContain("2251A");
    expect(picks).toHaveLength(10);
  });

  it("says where his pool starts: level 3, Div. 2 C, a rating of about 1350", () => {
    expect(poolStart(live, new Set())).toEqual({
      level: 3,
      letter: "C",
      rating: 1350,
    });
    expect(poolStart([], new Set())).toBeNull();
  });

  it("maps a level back to the rating it came from", () => {
    for (const rating of [800, 1000, 1350, 1600, 1900, 2300, 2700])
      expect(ratingOf(levelOf(rating))).toBeCloseTo(rating);
  });
});

describe("Saad's mix at the edge of his pool", () => {
  // Every open problem is at level 3.
  const edge = live.slice(1, 6);

  it("at level 2, slightly above stops short of 3, so nothing of his fits", () => {
    const picks = pickMix(edge, 2, "2026-11-09", new Set(), band);
    expect(fromSaad(picks)).toEqual([]);
    expect(picks).toHaveLength(10);
  });

  it("just past level 2, his level-3 problems fill slightly above first", () => {
    const picks = pickMix(edge, 2.01, "2026-11-09", new Set(), band);
    const saad = fromSaad(picks);
    expect(saad.map((pick) => pick.band)).toEqual(Array(5).fill("above"));
    expect(fromBand(picks)).toHaveLength(5);
  });

  it("inside his pool, takes nothing from the band", () => {
    const picks = pickMix(pool, 5, "2026-11-09", new Set(), band);
    expect(picks.every((pick) => pick.source === "saad")).toBe(true);
  });
});
