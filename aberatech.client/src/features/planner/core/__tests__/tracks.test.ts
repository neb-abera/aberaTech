/**
 * Every curated track must be a plan you could actually enrol in. These
 * assertions are the reason the tracks can be trusted: each is scheduled by the
 * real engine and checked against the real degree rules.
 */
import { describe, expect, test } from "vitest";
import rawCatalog from "../../data/catalog.json";
import rawTracks from "../../data/tracks.json";
import { Calendar } from "../calendar";
import { CatalogData } from "../catalog";
import { closure } from "../prereq";
import { degreeAudit, LIMITS } from "../rules";
import { placeInOrder, Tracks } from "../tracks";
import type { Catalog, RawCatalog, RawTracks } from "../types";
import { termOf, toyCourse } from "./fixtures";

const data = new CatalogData(rawCatalog as unknown as RawCatalog);
const tracks = new Tracks(rawTracks as unknown as RawTracks);
const cal = new Calendar({});

const SIGNAL_CHAIN: Record<string, string[]> = {
  aperture: ["EN.525.618", "EN.525.738", "EN.525.656"],
  frontEnd: ["EN.525.684", "EN.525.623", "EN.525.774", "EN.525.620"],
  hardware: [
    "EN.525.642",
    "EN.525.677",
    "EN.525.612",
    "EN.525.742",
    "EN.525.743",
  ],
  dsp: ["EN.525.627", "EN.525.718", "EN.525.721", "EN.525.631"],
  exploit: ["EN.525.728", "EN.525.752", "EN.525.783", "EN.525.744"],
};

describe("track definitions", () => {
  test("there is at least one of each kind", () => {
    expect(tracks.byKind("degree").length).toBeGreaterThanOrEqual(2);
    expect(tracks.byKind("mastery").length).toBeGreaterThanOrEqual(2);
  });

  for (const t of tracks.all()) {
    describe(t.name, () => {
      test("every course exists in the catalog", () => {
        expect(t.courses.filter((c) => !data.get(c))).toEqual([]);
      });
      test("no course is listed twice", () => {
        expect(new Set(t.courses).size).toBe(t.courses.length);
      });
      test("every stage course appears in the flattened list and the other way round", () => {
        expect([...t.courses].sort()).toEqual(
          t.stages.flatMap((s) => s.courses).sort(),
        );
      });
      test("it is prerequisite closed: nothing has to be silently added", () => {
        const added = [...closure(data.courses, t.courses)].filter(
          (c) => !t.courses.includes(c),
        );
        expect(
          added,
          `closure would add ${added.map((c) => data.title(c)).join(", ")}`,
        ).toEqual([]);
      });
      test("it schedules with no prerequisite violations", () => {
        const plan = tracks.toPlan(data.courses, t.id, 2);
        expect(plan.violations()).toEqual([]);
        expect(plan.courses()).toHaveLength(t.courses.length);
      });
      test("the stages come out in order", () => {
        const p = tracks.toPlan(data.courses, t.id, 2).placement();
        for (let i = 1; i < t.stages.length; i++) {
          const prevMax = Math.max(
            ...t.stages[i - 1].courses.map((c) => p.get(c) ?? 0),
          );
          const thisMax = Math.max(
            ...t.stages[i].courses.map((c) => p.get(c) ?? 0),
          );
          expect(
            thisMax,
            `stage "${t.stages[i].name}" finishes too early`,
          ).toBeGreaterThanOrEqual(prevMax);
        }
      });
      test("it satisfies every degree rule", () => {
        const a = degreeAudit(tracks.toPlan(data.courses, t.id, 2), cal, {});
        expect(a.rules.filter((r) => !r.met).map((r) => r.id)).toEqual([]);
      });
      test("the applied ten finish inside the five year limit", () => {
        const a = degreeAudit(tracks.toPlan(data.courses, t.id, 2), cal, {});
        expect(
          a.clock.onTime,
          `over by ${-(a.clock.slackMonths ?? 0)} months`,
        ).toBe(true);
      });
      test(
        t.kind === "degree"
          ? "a degree track is exactly the ten the degree needs"
          : "a mastery track is longer than the degree",
        () => {
          if (t.kind === "degree")
            expect(t.courses).toHaveLength(LIMITS.COURSES);
          else expect(t.courses.length).toBeGreaterThan(LIMITS.COURSES);
        },
      );
      test("it carries a goal and an honest tradeoff", () => {
        expect(t.goal.length).toBeGreaterThan(40);
        expect(
          t.tradeoff.length,
          "every track must say what it costs you",
        ).toBeGreaterThan(40);
      });
    });
  }
});

describe("coverage of the signal chain", () => {
  const has = (id: string, list: string[]) =>
    list.some((c) => tracks.get(id)?.courses.includes(c));

  test("the full receiver track covers every stage", () => {
    for (const [name, list] of Object.entries(SIGNAL_CHAIN)) {
      expect(has("collector-full", list), `no course covering ${name}`).toBe(
        true,
      );
    }
  });
  test("the pure signal processing track deliberately has no radio frequency content", () => {
    expect(has("sp-core", SIGNAL_CHAIN.aperture)).toBe(false);
    expect(has("sp-core", SIGNAL_CHAIN.frontEnd)).toBe(false);
  });
  test("the ten course collector cannot fit an antenna course, and says so", () => {
    expect(has("collector-10", SIGNAL_CHAIN.aperture)).toBe(false);
    expect(tracks.get("collector-10")?.tradeoff).toMatch(/aperture|antenna/i);
  });
});

/**
 * placeInOrder promises each course sits "never before a course listed ahead of
 * it". It used to set the floor one term back to allow pairing, which let a
 * course with no prerequisites drop into a gap a prerequisite had left open in
 * the previous term, and run the curated stages backwards.
 */
describe("placeInOrder holds the order it is given", () => {
  const CAT: Catalog = {
    P: toyCourse("P", "Papa"),
    Q: toyCourse("Q", "Quebec", { groups: [["P"]] }),
    R: toyCourse("R", "Romeo", { groups: [["Q"]] }),
    S: toyCourse("S", "Sierra"),
  };

  test("a course never lands earlier than one listed before it", () => {
    const plan = placeInOrder(CAT, ["P", "Q", "R", "S"], 2);
    let last = -1;
    for (const code of ["P", "Q", "R", "S"]) {
      const t = termOf(plan, code);
      expect(t, `${code} ran backwards`).toBeGreaterThanOrEqual(last);
      last = t;
    }
  });

  test("S fills the gap beside R rather than the one the prerequisite left open", () => {
    const plan = placeInOrder(CAT, ["P", "Q", "R", "S"], 2);
    expect(termOf(plan, "S")).toBe(termOf(plan, "R"));
  });

  test("pairing within a term still works, so the plan does not get longer", () => {
    const plan = placeInOrder(CAT, ["P", "S"], 2);
    expect(termOf(plan, "S")).toBe(termOf(plan, "P"));
  });

  test("every curated track still runs its stages forwards", () => {
    for (const t of tracks.all()) {
      const plan = tracks.toPlan(data.courses, t.id, 2);
      let highest = -1;
      for (const term of plan.terms) {
        for (const code of term) {
          const i = t.stages.findIndex((s) => s.courses.includes(code));
          if (i < 0) continue;
          expect(
            i,
            `${t.name}: ${data.title(code)} runs before an earlier stage`,
          ).toBeGreaterThanOrEqual(highest);
          highest = Math.max(highest, i);
        }
      }
    }
  });
});
