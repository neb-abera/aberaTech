import { describe, expect, test } from "vitest";
import { Calendar } from "../calendar";
import { Plan } from "../plan";
import { degreeAudit, LIMITS } from "../rules";
import type { Catalog } from "../types";
import { TOY, toyCourse } from "./fixtures";

const cal = new Calendar({
  startTerm: "Spring",
  startYear: 2027,
  termsPerYear: 3,
});
const audit = (terms: string[][], opts = {}) =>
  degreeAudit(Plan.fromTerms(TOY, terms), cal, opts);
const rule = (rules: { id: string; met: boolean }[], id: string) =>
  rules.find((r) => r.id === id)?.met;

describe("which courses count toward the degree", () => {
  test("a course carrying no graduate credit is excluded", () => {
    const a = audit([["P", "A"]]);
    expect(a.counted).toEqual(["A"]);
    expect(a.excluded).toHaveLength(1);
    expect(a.excluded[0].why).toMatch(/graduate credit/i);
  });

  test("only ten count, in term order, when nothing forces a different set", () => {
    const cat: Catalog = { ...TOY };
    const terms: string[][] = [];
    for (let i = 0; i < 12; i++) {
      cat[`X${i}`] = toyCourse(`X${i}`, `X${i}`, { areas: [] });
      terms.push([`X${i}`]);
    }
    const a = degreeAudit(Plan.fromTerms(cat, terms), cal, {});
    expect(a.counted).toHaveLength(LIMITS.COURSES);
    expect(a.counted[0]).toBe("X0");
    expect(a.counted[9]).toBe("X9");
  });
});

describe("the five year clock", () => {
  test("starts with the first course that counts, not the first course taken", () => {
    // P is a bridge course in term 0 and carries no graduate credit.
    const a = audit([["P"], ["A"], ["B"]]);
    expect(a.clock.startTerm).toBe(1);
    expect(a.clock.startDate?.toISOString().slice(0, 7)).toBe("2027-05");
  });
  test("the deadline is five years after the clock starts", () => {
    const a = audit([["A"]]);
    expect(a.clock.deadline?.toISOString().slice(0, 4)).toBe("2032");
    expect(monthsOfClock(a.clock.startDate, a.clock.deadline)).toBe(60);
  });
  test("a leave of absence pushes the deadline out by its length", () => {
    const a = audit([["A"]], { leaveMonths: 12 });
    expect(monthsOfClock(a.clock.startDate, a.clock.deadline)).toBe(72);
  });
  test("a leave of absence is capped at two years", () => {
    const a = audit([["A"]], { leaveMonths: 48 });
    expect(monthsOfClock(a.clock.startDate, a.clock.deadline)).toBe(
      60 + LIMITS.MAX_LEAVE_MONTHS,
    );
  });
  test("an extenuating circumstances extension adds up to two more years", () => {
    const a = audit([["A"]], { extensionMonths: 24 });
    expect(monthsOfClock(a.clock.startDate, a.clock.deadline)).toBe(84);
  });
  test("no clock runs until a countable course is placed", () => {
    expect(audit([[]]).clock.startTerm).toBeNull();
    expect(audit([["P"]]).clock.startTerm).toBeNull();
  });
});

describe("projected completion against the deadline", () => {
  const cat: Catalog = { ...TOY };
  for (let i = 0; i < 10; i++)
    cat[`Y${i}`] = toyCourse(`Y${i}`, `Y${i}`, {
      level: i < 4 ? 7 : 6,
      areas: [],
    });
  const tenIn = (spacing: number) => {
    const terms: string[][] = [];
    for (let i = 0; i < 10; i++) {
      terms.push([`Y${i}`]);
      for (let k = 1; k < spacing; k++) terms.push([]);
    }
    return degreeAudit(Plan.fromTerms(cat, terms), cal, {});
  };
  test("ten courses one per term finishes inside five years", () => {
    const a = tenIn(1);
    expect(a.clock.onTime).toBe(true);
    expect(a.clock.slackMonths ?? 0).toBeGreaterThan(0);
  });
  test("spreading them too thin blows the deadline and says by how much", () => {
    const a = tenIn(3);
    expect(a.clock.onTime).toBe(false);
    expect(a.clock.slackMonths ?? 0).toBeLessThan(0);
    expect(a.blockers.some((b) => /five year/i.test(b.detail))).toBe(true);
  });
  test("the last countable course sets the projected finish", () => {
    expect(tenIn(1).clock.finishTerm).toBe(9);
  });
});

describe("the four degree rules", () => {
  const mk = (n: number, opts: { sevens?: number; external?: number } = {}) => {
    const cat: Catalog = { ...TOY };
    const terms: string[][] = [];
    for (let i = 0; i < n; i++) {
      cat[`Z${i}`] = toyCourse(`Z${i}`, `Z${i}`, {
        areas: [],
        level: i < (opts.sevens ?? 0) ? 7 : 6,
        external: i < (opts.external ?? 0),
      });
      terms.push([`Z${i}`]);
    }
    return degreeAudit(Plan.fromTerms(cat, terms), cal, {});
  };
  test("ten courses required", () => {
    expect(rule(mk(9).rules, "count")).toBe(false);
    expect(rule(mk(10, { sevens: 4 }).rules, "count")).toBe(true);
  });
  test("at least four at the seven hundred level", () => {
    expect(rule(mk(10, { sevens: 3 }).rules, "level700")).toBe(false);
    expect(rule(mk(10, { sevens: 4 }).rules, "level700")).toBe(true);
  });
  test("at most three from outside the program", () => {
    expect(rule(mk(10, { sevens: 4, external: 3 }).rules, "outside")).toBe(
      true,
    );
    expect(rule(mk(10, { sevens: 4, external: 4 }).rules, "outside")).toBe(
      false,
    );
  });
  test("at least seven from within the program", () => {
    expect(rule(mk(10, { sevens: 4, external: 4 }).rules, "inProgram")).toBe(
      false,
    );
  });
  test("a complete plan reports ready to graduate", () => {
    const a = mk(10, { sevens: 4 });
    expect(a.readyToGraduate).toBe(true);
    expect(a.blockers).toEqual([]);
  });
});

describe("choosing which ten courses to apply", () => {
  const cat: Catalog = {};
  // Ten 600 level courses first, then four at the 700 level, which is what
  // taking courses in prerequisite order actually produces.
  for (let i = 0; i < 10; i++)
    cat[`S${i}`] = toyCourse(`S${i}`, `S${i}`, { areas: [] });
  for (let i = 0; i < 4; i++)
    cat[`S7${i}`] = toyCourse(`S7${i}`, `S7${i}`, { level: 7, areas: [] });
  cat.BR = toyCourse("BR", "bridge", { level: 2, gradeable: false, areas: [] });

  const build = () => {
    const terms: string[][] = [];
    for (let i = 0; i < 10; i++) terms.push([`S${i}`]);
    for (let i = 0; i < 4; i++) terms.push([`S7${i}`]);
    return Plan.fromTerms(cat, terms);
  };

  test("the naive first ten cannot satisfy the 700 level rule", () => {
    const firstTen = build()
      .terms.flat()
      .filter((c) => cat[c].gradeable)
      .slice(0, 10);
    expect(firstTen.filter((c) => cat[c].level >= 7)).toHaveLength(0);
  });

  test("the chosen ten satisfy every rule when a satisfying set exists", () => {
    const a = degreeAudit(build(), cal, {});
    expect(a.counted).toHaveLength(10);
    expect(
      a.counted.filter((c) => cat[c].level >= 7).length,
    ).toBeGreaterThanOrEqual(4);
    expect(a.rules.filter((r) => !r.met)).toEqual([]);
  });

  test("the chosen ten sit as close together as possible, to protect the clock", () => {
    const a = degreeAudit(build(), cal, {});
    const p = build().placement();
    const terms = a.counted.map((c) => p.get(c) ?? 0);
    expect(Math.max(...terms) - Math.min(...terms)).toBeLessThanOrEqual(12);
  });

  test("the audit explains why those ten were chosen", () => {
    const a = degreeAudit(build(), cal, {});
    expect(a.selection.automatic).toBe(true);
    expect(a.selection.why).toMatch(/700 level|span|rule/i);
  });

  test("an explicit pick overrides the automatic choice", () => {
    const a = degreeAudit(build(), cal, { picks: new Set(["S0", "S1", "S2"]) });
    expect([...a.counted].sort()).toEqual(["S0", "S1", "S2"]);
    expect(a.selection.automatic).toBe(false);
  });

  test("a course carrying no graduate credit is never chosen", () => {
    const p = Plan.fromTerms(cat, [
      ["BR"],
      ...Array.from({ length: 10 }, (_, i) => [`S${i}`]),
      ...Array.from({ length: 4 }, (_, i) => [`S7${i}`]),
    ]);
    expect(degreeAudit(p, cal, {}).counted).not.toContain("BR");
  });

  test("with fewer than ten countable courses it reports what it has", () => {
    const a = degreeAudit(Plan.fromTerms(cat, [["S0"], ["S1"]]), cal, {});
    expect(a.counted).toHaveLength(2);
    expect(rule(a.rules, "count")).toBe(false);
  });
});

function monthsOfClock(start: Date | null, deadline: Date | null): number {
  if (!start || !deadline) throw new Error("the clock has not started");
  return Calendar.monthsBetween(start, deadline);
}
