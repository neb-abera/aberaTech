import { describe, expect, test } from "vitest";
import {
  closure,
  earliestTerm,
  latestTerm,
  legalTerms,
  missingFor,
  unmetGroups,
} from "../prereq";
import { TOY } from "./fixtures";

const place = (obj: Record<string, number>) => new Map(Object.entries(obj));

describe("group satisfaction", () => {
  test("a group is met when a member sits in a strictly earlier term", () => {
    expect(unmetGroups(TOY, "B", 1, place({ A: 0 }))).toEqual([]);
  });
  test("the same term does not satisfy a prerequisite", () => {
    expect(unmetGroups(TOY, "B", 0, place({ A: 0 }))).toEqual([["A"]]);
  });
  test("an or group needs only one member", () => {
    expect(unmetGroups(TOY, "E", 1, place({ F: 0 }))).toEqual([]);
    expect(unmetGroups(TOY, "E", 1, place({ B: 0 }))).toEqual([]);
  });
  test("an and of two groups needs both", () => {
    expect(unmetGroups(TOY, "D", 1, place({ A: 0 }))).toEqual([["B"]]);
    expect(unmetGroups(TOY, "D", 2, place({ A: 0, B: 1 }))).toEqual([]);
  });
});

describe("closure", () => {
  test("pulls the whole chain in", () => {
    expect([...closure(TOY, ["C"])].sort()).toEqual(["A", "B", "C"]);
  });
  test("picks the cheaper side of an or group", () => {
    // E needs B or F. F costs 1, B drags in A so costs 2. F wins.
    expect([...closure(TOY, ["E"])].sort()).toEqual(["E", "F"]);
  });
  test("keeps a choice that is already selected instead of adding another", () => {
    expect([...closure(TOY, ["E", "B"])].sort()).toEqual(["A", "B", "E"]);
  });
  test("ignores prerequisites that are not courses in the catalog", () => {
    expect([...closure(TOY, ["G"])].sort()).toEqual(["G"]);
  });
  test("is idempotent", () => {
    const once = closure(TOY, ["C"]);
    expect([...closure(TOY, [...once])].sort()).toEqual([...once].sort());
  });
});

describe("missingFor", () => {
  test("lists what has to be added before a course can be placed", () => {
    expect(missingFor(TOY, "C", new Set()).sort()).toEqual(["A", "B"]);
  });
  test("is empty when everything is already present", () => {
    expect(missingFor(TOY, "C", new Set(["A", "B"]))).toEqual([]);
  });
  test("respects a satisfied or branch", () => {
    expect(missingFor(TOY, "E", new Set(["A", "B"]))).toEqual([]);
  });
  test("reports transitively: a selected prerequisite with its own gap still costs you", () => {
    // E needs B or F. B is selected, but B needs A, which is not. So A is missing.
    expect(missingFor(TOY, "E", new Set(["B"]))).toEqual(["A"]);
  });
  test("prefers the cheap or branch when neither side is selected", () => {
    expect(missingFor(TOY, "E", new Set())).toEqual(["F"]);
  });
});

describe("term bounds", () => {
  test("earliest is one past the earliest satisfying member", () => {
    expect(earliestTerm(TOY, "B", place({ A: 3 }))).toBe(4);
  });
  test("earliest is null when a prerequisite is unplaced", () => {
    expect(earliestTerm(TOY, "B", place({}))).toBeNull();
  });
  test("a course with no prerequisites can go first", () => {
    expect(earliestTerm(TOY, "A", place({}))).toBe(0);
  });
  test("latest is bounded by a placed dependent", () => {
    expect(latestTerm(TOY, "A", place({ B: 5 }))).toBe(4);
  });
  test("latest is unbounded when the dependent has another satisfier", () => {
    expect(latestTerm(TOY, "B", place({ E: 3, F: 0 }))).toBe(Infinity);
  });
  test("legalTerms is the closed interval between the bounds", () => {
    const legal = legalTerms(TOY, "B", place({ A: 0, C: 4 }), 6);
    expect([...legal].sort((x, y) => x - y)).toEqual([1, 2, 3]);
  });
  test("legalTerms is empty when a prerequisite is unplaced", () => {
    expect(legalTerms(TOY, "C", place({}), 6).size).toBe(0);
  });
});
