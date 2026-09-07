import { describe, expect, it } from "vitest";
import {
  bandLengths,
  decibels,
  formulas,
  phonetic,
  planTemplate,
  prowords,
  templateStandard,
  zuluOffsets,
} from "../reference";

describe("the reference cards", () => {
  it("compute the band lengths from the formulas they print", () => {
    for (const band of bandLengths) {
      expect(band.halfWaveFeet).toBeCloseTo(468 / band.mhz, 6);
      expect(band.legFeet).toBeCloseTo(band.halfWaveFeet / 2, 6);
      expect(band.ocfFeet).toBeCloseTo(0.14 * band.halfWaveFeet, 6);
      expect(band.metres).toBeCloseTo(300 / band.mhz, 6);
    }
  });

  it("have a decibel table that matches 10 log10", () => {
    for (const { db, ratio } of decibels) {
      const value = Number(ratio.replace(/\s/g, ""));
      expect(Math.abs(10 * Math.log10(value) - db)).toBeLessThan(0.05);
    }
  });

  it("spell the whole alphabet once", () => {
    expect(phonetic).toHaveLength(26);
    expect(phonetic.map(([letter]) => letter).join("")).toBe(
      "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
    );
  });

  it("define every proword and formula", () => {
    for (const [word, meaning] of prowords) {
      expect(word.length, word).toBeGreaterThan(2);
      expect(meaning.length, word).toBeGreaterThan(10);
    }
    for (const formula of formulas) {
      expect(formula.formula.length, formula.name).toBeGreaterThan(3);
    }
    expect(zuluOffsets.length).toBeGreaterThan(4);
  });

  it("template every section with at least one question, and set the clock", () => {
    expect(planTemplate.length).toBeGreaterThan(5);
    for (const section of planTemplate) {
      expect(section.prompts.length, section.title).toBeGreaterThan(0);
    }
    expect(templateStandard).toContain("four hours");
  });
});
