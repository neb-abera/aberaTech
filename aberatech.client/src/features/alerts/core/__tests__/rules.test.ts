/**
 * What a rule matches and what an event with no choice of its own sends,
 * as AlertTypeRule.Matches and AlertTypes.Resolve do on the server.
 */

import { describe, expect, it } from "vitest";
import type { AlertItem, TypeRule } from "../api";
import { ruleMatches, ruleProblems, unchosenType } from "../rules";

const drill = {
  key: "drill|1",
  title: "Fire drill",
  location: null,
  organizer: "OPS@army.mil",
  critical: false,
} as AlertItem;

describe("ruleMatches", () => {
  it("matches a field that contains the text, in any case", () => {
    expect(
      ruleMatches({ field: "title", text: "DRILL", type: "alarm" }, drill),
    ).toBe(true);
    expect(
      ruleMatches(
        { field: "organizer", text: "army.mil", type: "alarm" },
        drill,
      ),
    ).toBe(true);
    expect(
      ruleMatches(
        { field: "organizer", text: "mitre.org", type: "alarm" },
        drill,
      ),
    ).toBe(false);
  });

  it("matches any value with blank text, and never a missing field", () => {
    expect(
      ruleMatches({ field: "organizer", text: "", type: "alarm" }, drill),
    ).toBe(true);
    expect(
      ruleMatches({ field: "location", text: "", type: "alarm" }, drill),
    ).toBe(false);
    expect(
      ruleMatches(
        { field: "organizer", text: "", type: "alarm" },
        { ...drill, organizer: undefined },
      ),
    ).toBe(false);
  });
});

describe("unchosenType", () => {
  const rules: TypeRule[] = [
    { field: "title", text: "lunch", type: "none" },
    { field: "organizer", text: "army.mil", type: "alarm" },
    { field: "title", text: "drill", type: "notification" },
  ];

  it("is #critical first, then the first rule that matches, then the default", () => {
    expect(unchosenType({ ...drill, critical: true }, rules, "none")).toEqual({
      type: "alarm",
      typeFrom: "critical",
    });
    expect(unchosenType(drill, rules, "none")).toEqual({
      type: "alarm",
      typeFrom: "rule",
    });
    expect(
      unchosenType(
        { ...drill, title: "Review", organizer: null },
        rules,
        "notification",
      ),
    ).toEqual({
      type: "notification",
      typeFrom: "default",
    });
  });
});

describe("ruleProblems", () => {
  it("names a title rule with no text and text past 100 characters", () => {
    expect(
      ruleProblems([
        { field: "title", text: "  ", type: "alarm" },
        { field: "location", text: "x".repeat(101), type: "none" },
        { field: "organizer", text: "", type: "alarm" },
      ]),
    ).toEqual([
      "Rule 1: a title rule needs a word to look for.",
      "Rule 2: at most 100 characters.",
    ]);
  });
});
