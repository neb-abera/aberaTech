import { describe, expect, test } from "vitest";
import rawCatalog from "../../data/catalog.json";
import linkCheck from "../../data/link-check.json";
import { catalogueUrl, courseLinks, professionalsUrl } from "../links";
import type { RawCatalog } from "../types";

const data = rawCatalog as unknown as RawCatalog;

describe("catalogue links", () => {
  test("the address is the one the catalogue itself uses for a course", () => {
    expect(catalogueUrl("EN.525.614")).toBe(
      "https://e-catalogue.jhu.edu/search/?P=EN.525.614",
    );
  });

  test("every course in the catalog has a recorded check", () => {
    const codes = Object.keys(data.courses).sort();
    expect(Object.keys(linkCheck.courses).sort()).toEqual(codes);
  });

  test("every recorded check resolved and matched the title we display", () => {
    const failed = Object.entries(linkCheck.courses)
      .filter(([, v]) => !v.titleMatches)
      .map(([code]) => code);
    expect(failed).toEqual([]);
    expect(linkCheck.resolvedWithMatchingTitle).toBe(linkCheck.total);
    expect(linkCheck.total).toBe(Object.keys(data.courses).length);
  });

  test("the recorded catalogue title is the title we show, for every course", () => {
    const norm = (s: string) =>
      s
        .replace(/&/g, "and")
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, " ")
        .trim();
    const wrong = Object.entries(linkCheck.courses).filter(
      ([code, v]) =>
        norm(v.catalogueTitle ?? "") !== norm(data.courses[code].title),
    );
    expect(wrong.map(([c]) => c)).toEqual([]);
  });
});

describe("offerings links", () => {
  /**
   * The first version of this built `525-728-...`, with a hyphen inside the
   * course number, and every link 404ed. These three were opened by hand and
   * are the reason the pattern is written the way it is.
   */
  const confirmed: [string, string, string][] = [
    [
      "EN.525.728",
      "Detection & Estimation Theory",
      "https://ep.jhu.edu/courses/525728-detection-estimation-theory/",
    ],
    [
      "EN.525.618",
      "Antenna Systems",
      "https://ep.jhu.edu/courses/525618-antenna-systems/",
    ],
    [
      "EN.525.614",
      "Probability & Stochastic Processes for Engineers",
      "https://ep.jhu.edu/courses/525614-probability-stochastic-processes-for-engineers/",
    ],
  ];
  for (const [code, title, want] of confirmed) {
    test(`${code} ${title}`, () => {
      expect(professionalsUrl(code, title)).toBe(want);
    });
  }

  test("the course number keeps no separator, which is what the old bug got wrong", () => {
    expect(professionalsUrl("EN.525.001", "A Course")).toContain("/525001-");
    expect(professionalsUrl("EN.525.001", "A Course")).not.toContain(
      "/525-001-",
    );
  });

  test("runs of punctuation collapse rather than leaving empty segments", () => {
    expect(professionalsUrl("EN.525.001", "A -- B,  C")).toBe(
      "https://ep.jhu.edu/courses/525001-a-b-c/",
    );
  });
});

describe("how links are presented", () => {
  test("the measured link is the one marked verified, and the derived one is not", () => {
    const links = courseLinks("EN.525.618", "Antenna Systems");
    expect(links).toHaveLength(2);
    const verified = links.filter((l) => l.verified);
    expect(verified).toHaveLength(1);
    expect(verified[0].href).toContain("e-catalogue.jhu.edu");
    expect(links.find((l) => !l.verified)?.href).toContain("ep.jhu.edu");
  });

  test("an unverified link says so in its own note rather than relying on the flag", () => {
    const derived = courseLinks("EN.525.618", "Antenna Systems").find(
      (l) => !l.verified,
    );
    expect(derived?.note).toMatch(/derived|not measured/i);
  });
});
