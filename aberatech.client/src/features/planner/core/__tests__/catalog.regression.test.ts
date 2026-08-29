/**
 * Guards the real catalog. These assertions encode prerequisite readings that
 * were wrong at some point and must never regress.
 */
import { describe, expect, test } from "vitest";
import rawCatalog from "../../data/catalog.json";
import {
  ADMISSION,
  COMPOSITE,
  PREP_SOURCE,
  prepId,
  withBackground,
} from "../background";
import { CatalogData } from "../catalog";
import { Plan } from "../plan";
import { closure } from "../prereq";
import type { RawCatalog } from "../types";

const data = new CatalogData(rawCatalog as unknown as RawCatalog);
const C = data.courses;
const COMPOSITE_IDS = Object.keys(COMPOSITE);

describe("catalog shape", () => {
  test("every course has the fields the core relies on", () => {
    for (const [code, c] of Object.entries(C)) {
      for (const f of [
        "title",
        "groups",
        "level",
        "gradeable",
        "external",
        "excl",
        "bg",
        "areas",
      ]) {
        expect(f in c, `${code} is missing ${f}`).toBe(true);
      }
      expect(Array.isArray(c.groups), `${code}.groups must be an array`).toBe(
        true,
      );
      c.groups.forEach((g) => {
        expect(
          Array.isArray(g) && g.length > 0,
          `${code} has a malformed group`,
        ).toBe(true);
      });
    }
  });
  test("every course belongs to at least one group so the whole program is reachable", () => {
    for (const [code, c] of Object.entries(C))
      expect(c.areas.length, `${code} is in no area`).toBeGreaterThan(0);
  });
  test("no course lists itself as its own prerequisite", () => {
    for (const [code, c] of Object.entries(C)) {
      c.groups.forEach((g) => {
        expect(g.includes(code), `${code} requires itself`).toBe(false);
      });
    }
  });
  test("exactly one course carries no graduate credit", () => {
    expect(
      Object.values(C)
        .filter((c) => !c.gradeable)
        .map((c) => c.code),
    ).toEqual(["EN.525.201"]);
  });
});

describe("prerequisite readings that were wrong before", () => {
  const cases: [string, string[][], string][] = [
    [
      "EN.525.677",
      [["EN.525.627"], ["EN.525.642"]],
      '"and ... or equivalent to each" is an AND, not a choice',
    ],
    [
      "EN.525.742",
      [["EN.525.642"]],
      "written as a bare 525.642 with no EN prefix",
    ],
    [
      "EN.525.657",
      [["EN.525.201"], ["EN.525.624"]],
      "two bare codes joined by and",
    ],
    [
      "EN.525.724",
      [["EN.525.614"]],
      "the second clause only recommends a course",
    ],
    [
      "EN.525.618",
      [["EN.525.605", "EN.615.642"]],
      "a real choice, including a cross listed course",
    ],
    [
      "EN.525.751",
      [["EN.525.638", "EN.525.616"], ["EN.525.627"]],
      "a choice and a requirement together",
    ],
    [
      "EN.525.774",
      [["EN.525.623", "EN.525.620"]],
      "either microwave circuits or transmission systems",
    ],
    [
      "EN.525.707",
      [["EN.625.609"], ["EN.525.614"], ["EN.525.616"]],
      "three separate requirements",
    ],
    [
      "EN.525.623",
      [],
      "its prerequisite line is an exclusion, not a prerequisite",
    ],
    [
      "EN.525.783",
      [["EN.525.616"], ["EN.525.201", "bg_circ"], ["EN.525.202", "bg_sig"]],
      "one requirement plus two assumed bridge courses, each satisfiable by the background it teaches",
    ],
    [
      "EN.525.789",
      [
        ["EN.525.616"],
        ["EN.525.640"],
        ["EN.525.201", "bg_circ"],
        ["EN.525.202", "bg_sig"],
      ],
      "the same assumed bridge courses as 525.783",
    ],
    [
      "EN.525.664",
      [["EN.525.201", "bg_circ"]],
      '"or equivalent course in AC Circuit Theory" is a real choice',
    ],
    [
      "EN.525.674",
      [["EN.525.202", "bg_sig"]],
      '"EN.525.202 (Signals and Systems), or equivalent" is a real choice',
    ],
  ];
  for (const [code, expected, why] of cases) {
    test(`${code} ${data.title(code)}: ${why}`, () => {
      expect(C[code].groups).toEqual(expected);
    });
  }
  test("EN.525.623 records the exclusion instead", () => {
    expect(C["EN.525.623"].excl).toEqual(["EN.525.674"]);
  });
  test("EN.525.622 is a genuine zero prerequisite entry point", () => {
    expect(C["EN.525.622"].groups).toEqual([]);
    expect(C["EN.525.622"].gradeable).toBe(true);
  });
});

describe("every group schedules completely", () => {
  for (const name of Object.keys(data.areas)) {
    test(name, () => {
      const chosen = data.select([name]);
      const plan = Plan.autoSchedule(C, chosen, 2);
      expect(plan.courses()).toHaveLength(closure(C, chosen).size);
      expect(
        plan.violations().filter((v) => v.kind === "prerequisite"),
      ).toEqual([]);
    });
  }
  test("all groups at once", () => {
    const chosen = data.select(
      Object.keys(data.areas),
      Object.keys(data.concentrations),
    );
    const plan = Plan.autoSchedule(C, chosen, 2);
    expect(plan.courses()).toHaveLength(Object.keys(C).length);
    expect(plan.violations().filter((v) => v.kind === "prerequisite")).toEqual(
      [],
    );
  });
});

/**
 * The five courses that state a degree rather than a course number. Each was
 * wrong in a different way, and each way is guarded here.
 */
describe("a degree stated as a prerequisite", () => {
  const held = new Set<string>();

  test("no course ever schedules a degree, because a degree is not a course", () => {
    const cat = withBackground(
      C,
      data.background,
      held,
      new Set(COMPOSITE_IDS),
    );
    for (const id of COMPOSITE_IDS)
      expect(cat[prepId(id)], `${id} was scheduled`).toBeUndefined();
  });

  test("every composite decomposes into items the catalog actually names", () => {
    const known = new Set(data.background.map(([k]) => k));
    for (const [id, parts] of Object.entries(COMPOSITE)) {
      expect(known.has(id), `${id} has no label`).toBe(true);
      for (const p of parts)
        expect(known.has(p), `${id} names an unknown part ${p}`).toBe(true);
    }
  });

  test("525.608 is reachable without a degree, because the catalog offers 525.616 instead", () => {
    // "Either an undergraduate degree in electrical engineering or 525.616
    // Communications Systems Engineering" is an OR and was being read as an AND.
    const cat = withBackground(C, data.background, held);
    expect(cat["EN.525.608"].groups).toEqual([["EN.525.616"]]);
    expect(data.missingBackground("EN.525.608", held)).toEqual([]);
    expect(closure(cat, ["EN.525.608"]).has("EN.525.616")).toBe(true);
  });

  test("525.608 asks for nothing once the degree is held", () => {
    expect(
      withBackground(C, data.background, new Set(["bg_ee"]))["EN.525.608"]
        .groups,
    ).toEqual([]);
  });

  test("525.743 accepts computer engineering or computer science, not only EE", () => {
    // "An undergraduate degree in electrical or computer engineering or computer
    // science, EN.525.612 ..., and working knowledge of C or C++".
    expect(C["EN.525.743"].bg).toContain("bg_eecs");
    expect(C["EN.525.743"].bg).not.toContain("bg_ee");
    expect(C["EN.525.743"].bg).toContain("bg_c");
  });

  test("the courses that genuinely assume a degree say so, and are not blocked by it", () => {
    const cat = withBackground(C, data.background, held);
    for (const code of ["EN.525.621", "EN.525.684", "EN.525.771"]) {
      expect(
        data.missingBackground(code, held),
        `${code} should report the degree`,
      ).toContain("bg_ee");
      expect(cat[code].groups, `${code} should not be blocked by it`).toEqual(
        C[code].groups,
      );
    }
  });

  test("opting in turns the degree into the coursework that stands for it", () => {
    const cat = withBackground(C, data.background, held, new Set(["bg_ee"]));
    // A part the catalog can teach offers the JHU course as the alternative.
    const parts = COMPOSITE.bg_ee.map((p) => {
      const jhu = PREP_SOURCE[p]?.jhu;
      return jhu && C[jhu] ? [prepId(p), jhu] : [prepId(p)];
    });
    expect(cat["EN.525.771"].groups).toEqual(parts);
    expect(closure(cat, ["EN.525.771"]).has(prepId("bg_ugem"))).toBe(true);
  });

  test("a gate describes itself well enough to act on", () => {
    const [g] = data.gates("EN.525.771", held);
    expect(g.composite).toBe(true);
    expect(g.label).toBe(data.backgroundLabel("bg_ee"));
    expect(g.missing.length).toBe(COMPOSITE.bg_ee.length);
  });
});

/**
 * The programme's admission prerequisites, from the ECE master's degree
 * requirements page. Circuits and signals and systems were missing from the
 * catalog entirely, so no plan ever showed them.
 */
describe("the admission prerequisites", () => {
  test("all five subjects are background items with a source", () => {
    const known = new Set(data.background.map(([k]) => k));
    for (const id of ADMISSION) {
      expect(known.has(id), `${id} has no label`).toBe(true);
      expect(PREP_SOURCE[id], `${id} has no source`).toBeDefined();
    }
  });
  test("the bridge courses stand behind circuits and signals and systems", () => {
    expect(PREP_SOURCE.bg_circ.jhu).toBe("EN.525.201");
    expect(PREP_SOURCE.bg_sig.jhu).toBe("EN.525.202");
  });
  test("an EE degree supplies circuits and signals and systems", () => {
    expect(COMPOSITE.bg_ee).toContain("bg_circ");
    expect(COMPOSITE.bg_ee).toContain("bg_sig");
  });
  test("the signal processing foundation assumes signals and systems", () => {
    for (const code of [
      "EN.525.613",
      "EN.525.614",
      "EN.525.616",
      "EN.525.627",
    ]) {
      expect(C[code].bg, `${code} should assume signals and systems`).toContain(
        "bg_sig",
      );
    }
  });
  test("the circuit design courses assume circuit theory", () => {
    for (const code of ["EN.525.624", "EN.525.634"]) {
      expect(C[code].bg, `${code} should assume circuit theory`).toContain(
        "bg_circ",
      );
    }
  });
});
