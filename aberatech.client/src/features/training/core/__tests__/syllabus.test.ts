import { describe, expect, it } from "vitest";
import linkCheck from "../../data/link-check.json";
import { readError } from "../grade";
import { everyItem } from "../progress";
import {
  allLessons,
  checkpointLabel,
  countOf,
  formatMinutes,
  locate,
  phaseOn,
  syllabus,
  unitMinutes,
  unitsOf,
} from "../syllabus";

/**
 * The syllabus is data, so these tests hold it to its own rules: every id
 * unique, every address opened and recorded, every number with a source.
 */

const items = everyItem();
const links = linkCheck.links as Record<string, { status?: number }>;

describe("the syllabus", () => {
  it("has a unique id for every unit, lesson, and item", () => {
    const ids = [
      ...syllabus.units.map((unit) => unit.id),
      ...allLessons().map((lesson) => lesson.id),
      ...items.map((item) => item.id),
    ];
    expect(ids.length).toBeGreaterThan(400);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("names a known system and a positive time on every item", () => {
    const systems = new Set(syllabus.systems.map((system) => system.id));
    for (const item of items) {
      expect(systems.has(item.system), item.id).toBe(true);
      expect(item.minutes, item.id).toBeGreaterThan(0);
      expect(item.what.length, item.id).toBeGreaterThan(10);
    }
  });

  it("links only addresses the link check opened and found", () => {
    const addresses = [
      ...items.flatMap((item) => [
        ...(item.url ? [item.url] : []),
        ...(item.videos ?? []).map((video) => video.url),
        ...(item.links ?? []).map((link) => link.url),
      ]),
      ...syllabus.systems
        .map((system) => system.url)
        .filter((url) => url.startsWith("http")),
    ];
    const missing = addresses.filter((url) => !(url in links));
    expect(missing).toEqual([]);
    const failed = addresses.filter(
      (url) => links[url].status !== undefined && links[url].status !== 200,
    );
    expect(failed).toEqual([]);
  });

  it("gives every judge problem its id and every Codeforces problem its rating", () => {
    for (const item of items.filter((item) => item.problem)) {
      const problem = item.problem;
      if (!problem) continue;
      if (problem.judge === "codeforces") {
        expect(problem.id, item.id).toMatch(/^\d+[A-Z]\d?$/);
        expect(problem.rating, item.id).toBeGreaterThanOrEqual(800);
        expect(item.record).toBe("codeforces");
      }
      if (problem.judge === "atcoder") {
        expect(problem.id, item.id).toMatch(
          /^(abc\d+_[a-g]|dp_[a-z]|(arc|agc)\d+_[a-f]\d?)$/,
        );
        expect(item.record).toBe("atcoder");
      }
      if (problem.judge === "cses") {
        expect(item.url).toBe(`https://cses.fi/problemset/task/${problem.id}`);
        expect(item.record).toBe("manual");
      }
    }
  });

  it("starts each track with the refresher, then the basics, and keeps them", () => {
    expect(unitsOf("programming")[0].id).toBe("refresh-cpp");
    expect(unitsOf("math")[0].id).toBe("refresh-algebra");
    expect(unitsOf("programming").map((unit) => unit.id)).toContain(
      "cpp-basics",
    );
    expect(unitsOf("math").map((unit) => unit.id)).toEqual(
      expect.arrayContaining([
        "prealgebra-1",
        "prealgebra-2",
        "prealgebra-3",
        "algebra-1",
      ]),
    );
  });

  it("writes lessons for every phase, Phase 3 included, and locks none", () => {
    for (const unit of syllabus.units) {
      expect(unit.lessons.length, unit.id).toBeGreaterThan(0);
      expect(unit.locked, unit.id).toBeUndefined();
    }
    expect(syllabus.units.filter((unit) => unit.phase === 3).length).toBe(17);
  });

  it("gives a checkpoint to every unit of lessons a placement can skip", () => {
    const without = syllabus.units
      .filter((unit) => unit.lessons.length > 0 && !unit.checkpoint)
      .map((unit) => unit.id);
    expect(without).toEqual([
      "refresh-cpp",
      "refresh-stl",
      "refresh-complexity",
      "cpp-toolkit",
      "refresh-dsa",
      "refresh-algebra",
      "first-contest",
      "amc-baseline",
      "amc12-gate",
      "otis-apply",
      "zeitz",
      "edpc",
      "ac-bands",
      "p3-zeitz-finish",
      "p3-egmo",
      "p3-stevens",
      "p3-khurmi",
      "p3-rocks",
      "p3-mathur",
      "p3-sriram",
      "p3-otis-excerpts",
      "p3-isl",
      "p3-usamo",
      "p3-imo",
      "p3-optional-engel",
      "p3-cpalgo",
      "p3-arc-old",
      "p3-div1-virtuals",
      "p3-div1-hard",
      "p3-agc",
    ]);
  });

  it("opens Phase 0 with a refresher of 3 to 5 weeks at the plan's pace, before any placement", () => {
    const refresher = syllabus.units.filter(
      (unit) => unit.id.startsWith("refresh-") || unit.id === "cpp-toolkit",
    );
    const minutes = refresher.reduce((sum, unit) => sum + unitMinutes(unit), 0);
    const week = syllabus.phases[0].hoursPerWeek * 60;
    expect(minutes).toBeGreaterThanOrEqual(3 * week);
    expect(minutes).toBeLessThanOrEqual(5 * week);
    for (const track of ["math", "programming"] as const) {
      const units = unitsOf(track).filter((unit) => !unit.slot);
      const firstPlacement = units.findIndex((unit) => unit.checkpoint);
      const lastRefresher = Math.max(
        ...units.map((unit, index) => (refresher.includes(unit) ? index : -1)),
      );
      expect(lastRefresher, track).toBeLessThan(firstPlacement);
    }
  });

  it("ends every C++ and STL refresher lesson in drills with tests", () => {
    for (const id of ["refresh-cpp", "refresh-stl"]) {
      const unit = syllabus.units.find((each) => each.id === id);
      for (const lesson of unit?.lessons ?? []) {
        const drills = lesson.items.filter((item) => item.drill);
        expect(drills.length, lesson.id).toBeGreaterThan(0);
        for (const item of drills) {
          expect(item.record, item.id).toBe("drill");
          expect(item.drill?.tests.length, item.id).toBeGreaterThanOrEqual(3);
        }
      }
    }
  });

  it("splits each written phase's week to its hours from the plan", () => {
    const two = syllabus.phases[2];
    expect(two.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(600);
    const one = syllabus.phases[1];
    expect(one.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(
      one.hoursPerWeek * 60,
    );
    const three = syllabus.phases[3];
    expect(three.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(
      three.hoursPerWeek * 60,
    );
    const zero = syllabus.phases[0];
    expect(zero.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(
      zero.hoursPerWeek * 60,
    );
  });

  it("places math by AoPS's own test for each course it has one for", () => {
    const aops = [
      "prealgebra",
      "algebra",
      "cp",
      "nt",
      "geometry",
      "int-algebra",
      "int-counting",
      "precalculus",
    ];
    const units = unitsOf("math").filter((unit) =>
      aops.includes(unit.id.replace(/-[^-]+$/, "")),
    );
    expect(units.length).toBe(28);
    for (const unit of units) {
      const items = unit.checkpoint?.items ?? [];
      expect(items.length, unit.id).toBeGreaterThan(0);
      for (const item of items) {
        expect(item.system, item.id).toBe("aops-diagnostic");
        expect(item.url, item.id).toMatch(
          /^https:\/\/data\.artofproblemsolving\.com\/course-docs\/diagnostics\/[a-z0-9-]+-posttest\.pdf$/,
        );
      }
    }
  });

  it("names its Alcumus topics, and sends to AoPS videos only for the books that have them", () => {
    for (const item of items) {
      if (item.system === "alcumus" && item.id !== "refresh-alcumus-setup") {
        expect(item.title, item.id).toMatch(/^Alcumus, [A-Za-z &]+: /);
        expect(item.what, item.id).toMatch(/mastered/);
      }
      if (item.system === "aops-video") {
        expect(item.id, item.id).toMatch(/^(pre|alg|cp)-\d+-watch$/);
        const ids = (item.videos ?? []).map((video) => video.youtube);
        expect(new Set(ids).size, item.id).toBe(ids.length);
      }
    }
    expect(JSON.stringify(syllabus)).not.toContain("pick the topic");
  });

  it("has no interview-prep site in it", () => {
    const text = JSON.stringify(syllabus).toLowerCase();
    for (const site of ["leetcode", "hackerrank", "structy", "algomonster"])
      expect(text).not.toContain(site);
  });
});

describe("the questions", () => {
  const units = syllabus.units;
  const questions = allLessons().flatMap((lesson) => lesson.questions ?? []);
  const alcumus = units.filter((unit) => unit.externalMastery);
  // Practice bands, contests, and timed papers are tested by the judges and
  // the paper's score.
  const untested = [
    "first-contest",
    "amc-baseline",
    "amc12-gate",
    "otis-apply",
    "ac-bands",
  ];

  it("has a unique id for every question", () => {
    const ids = questions.map((question) => question.id);
    expect(ids.length).toBeGreaterThan(900);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.filter((id) => id.startsWith("fixture-"))).toEqual([]);
  });

  it("sends every question to a place the link check opened", () => {
    const urls = [
      ...questions.map((question) => question.ref.url),
      ...alcumus.map((unit) => unit.externalMastery?.url ?? ""),
    ];
    expect(urls.filter((url) => !(url in links))).toEqual([]);
    expect(urls.filter((url) => (links[url].status ?? 200) !== 200)).toEqual(
      [],
    );
    for (const question of questions)
      expect(question.ref.label.length, question.id).toBeGreaterThan(5);
  });

  it("gives each type what it needs to be graded", () => {
    for (const question of questions) {
      if (question.type === "choice") {
        expect(question.choices.length, question.id).toBeGreaterThanOrEqual(3);
        expect(question.answer, question.id).toBeLessThan(
          question.choices.length,
        );
      }
      if (question.type === "number")
        expect(readError(question.answer), question.id).toBeNull();
      if (question.type === "proof")
        expect(question.rubric.length, question.id).toBeGreaterThanOrEqual(3);
      if (question.type === "code")
        expect(question.tests.length, question.id).toBeGreaterThanOrEqual(3);
      if (question.type === "card")
        expect(question.back.length, question.id).toBeGreaterThan(0);
    }
  });

  it("asks each pretest and mastery check from the unit's own lessons, never both", () => {
    for (const unit of units) {
      const own = new Set(
        unit.lessons.flatMap((lesson) =>
          (lesson.questions ?? []).map((question) => question.id),
        ),
      );
      const pretest = unit.pretest ?? [];
      const mastery = unit.mastery ?? [];
      for (const id of [...pretest, ...mastery])
        expect(own.has(id), `${unit.id} ${id}`).toBe(true);
      expect(pretest.filter((id) => mastery.includes(id))).toEqual([]);
    }
  });

  it("leaves to Alcumus what Alcumus covers", () => {
    expect(alcumus.length).toBe(26);
    for (const unit of alcumus) {
      expect(unit.pretest, unit.id).toBeUndefined();
      expect(unit.mastery, unit.id).toBeUndefined();
      const named = unit.lessons
        .flatMap((lesson) => lesson.items)
        .filter((item) => item.system === "alcumus")
        .map((item) => item.title)
        .join(" ");
      for (const topic of unit.externalMastery?.topics ?? [])
        expect(named, `${unit.id} ${topic}`).toContain(topic);
      for (const lesson of unit.lessons)
        for (const question of lesson.questions ?? [])
          expect(["card", "proof"], question.id).toContain(question.type);
    }
  });

  it("tests every unit of Phases 0 to 2 but bands, contests, and papers", () => {
    const bare = units
      .filter((unit) => unit.phase <= 2 && !unit.id.startsWith("band-"))
      .filter((unit) => !untested.includes(unit.id))
      .filter(
        (unit) =>
          !unit.lessons.some((lesson) => (lesson.questions ?? []).length > 0),
      )
      .map((unit) => unit.id);
    expect(bare).toEqual([]);
  });
});

describe("the helpers", () => {
  it("finds a lesson and an item by id", () => {
    expect(locate("cp-3")?.lesson?.title).toBe(
      "Chapter 3: Correcting for Overcounting",
    );
    expect(locate("cp-3-read")?.item?.title).toContain("pp. 49 to 62");
    expect(locate("cf-2263B")?.unit.id).toBe("band-900");
    expect(locate("nope")).toBeNull();
  });

  it("finds the phase of a date, and the nearest one outside them", () => {
    expect(phaseOn("2026-11-09").phase).toBe(0);
    expect(phaseOn("2027-01-01").phase).toBe(1);
    expect(phaseOn("2027-09-30").phase).toBe(1);
    expect(phaseOn("2027-10-01").phase).toBe(2);
    expect(phaseOn("2020-01-01").phase).toBe(0);
    expect(phaseOn("2040-01-01").phase).toBe(3);
  });

  it("counts in words that agree with the number", () => {
    expect(countOf(1, "lesson")).toBe("1 lesson");
    expect(countOf(4, "lesson")).toBe("4 lessons");
    const math = syllabus.units.find((unit) => unit.id === "prealgebra-2");
    const code = syllabus.units.find((unit) => unit.id === "bronze-1");
    expect(math && checkpointLabel(math)).toBe("2 problem sets in 120 minutes");
    expect(code && checkpointLabel(code)).toBe("3 problems in 60 minutes");
  });

  it("formats minutes", () => {
    expect(formatMinutes(45)).toBe("45 min");
    expect(formatMinutes(60)).toBe("1 h");
    expect(formatMinutes(95)).toBe("1 h 35 min");
  });
});
