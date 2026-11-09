import { describe, expect, it } from "vitest";
import linkCheck from "../../data/link-check.json";
import { everyItem } from "../progress";
import {
  allLessons,
  formatMinutes,
  locate,
  phaseOn,
  syllabus,
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
        expect(problem.id, item.id).toMatch(/^(abc\d+_[a-g]|dp_[a-z])$/);
        expect(item.record).toBe("atcoder");
      }
      if (problem.judge === "cses") {
        expect(item.url).toBe(`https://cses.fi/problemset/task/${problem.id}`);
        expect(item.record).toBe("manual");
      }
    }
  });

  it("starts each track from the basics and keeps them", () => {
    expect(unitsOf("programming")[0].id).toBe("cpp-basics");
    expect(unitsOf("math").map((unit) => unit.id)).toEqual(
      expect.arrayContaining([
        "prealgebra-1",
        "prealgebra-2",
        "prealgebra-3",
        "algebra-1",
      ]),
    );
  });

  it("writes lessons for Phases 0 to 2 and locks Phase 3 with titles", () => {
    for (const unit of syllabus.units) {
      if (unit.phase <= 2) {
        expect(unit.lessons.length, unit.id).toBeGreaterThan(0);
        expect(unit.locked, unit.id).toBeUndefined();
      } else {
        expect(unit.lessons, unit.id).toEqual([]);
        expect(unit.locked, unit.id).toMatch(/before Phase 3 starts/);
      }
    }
  });

  it("gives a checkpoint to every unit of lessons a placement can skip", () => {
    const without = syllabus.units
      .filter((unit) => unit.lessons.length > 0 && !unit.checkpoint)
      .map((unit) => unit.id);
    expect(without).toEqual([
      "cpp-toolkit",
      "first-contest",
      "amc-baseline",
      "amc12-gate",
      "otis-apply",
      "zeitz",
      "edpc",
      "ac-bands",
    ]);
  });

  it("splits each written phase's week to its hours from the plan", () => {
    const two = syllabus.phases[2];
    expect(two.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(600);
    const one = syllabus.phases[1];
    expect(one.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(
      one.hoursPerWeek * 60,
    );
    const zero = syllabus.phases[0];
    expect(zero.week.reduce((sum, block) => sum + block.minutes, 0)).toBe(
      zero.hoursPerWeek * 60,
    );
  });

  it("has no interview-prep site in it", () => {
    const text = JSON.stringify(syllabus).toLowerCase();
    for (const site of ["leetcode", "hackerrank", "structy", "algomonster"])
      expect(text).not.toContain(site);
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

  it("formats minutes", () => {
    expect(formatMinutes(45)).toBe("45 min");
    expect(formatMinutes(60)).toBe("1 h");
    expect(formatMinutes(95)).toBe("1 h 35 min");
  });
});
