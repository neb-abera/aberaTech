/**
 * Background requirements as plannable preparation.
 *
 * Much of the JHU catalog states prerequisites in prose rather than course
 * numbers: "an undergraduate degree in electrical engineering", "a solid
 * understanding of digital logic fundamentals". Those cannot be enforced against
 * a course list, so an earlier version of this planner surfaced them as warnings
 * and left you to sort it out.
 *
 * Instead, each background item becomes a preparation course you can schedule.
 * Some map to a real JHU course, most are taken anywhere. Ticking "I already
 * have this" removes the requirement; leaving it unticked makes it a genuine
 * prerequisite that the scheduler, the drag legality check and the automatic
 * placer all respect, because it is expressed in exactly the same shape as any
 * other prerequisite.
 *
 * With one exception, which is what COMPOSITE below is for. "An undergraduate
 * degree in electrical engineering" is not a course. It is four years, not a
 * term, and an earlier version of this file turned it into a single preparation
 * course anyway, which put a whole degree in one slot of one term and read as
 * nonsense. A composite is never scheduled. It is the sum of its parts, it is
 * satisfied when its parts are, and until then the courses that ask for it are
 * flagged rather than blocked, so you can see exactly which parts you are
 * missing and choose to schedule those instead.
 */
import type { Catalog, Course } from "./types";

export const PREP_PREFIX = "prep.";

export function prepId(bgKey: string): string {
  return PREP_PREFIX + bgKey;
}

export function isPrep(code: string): boolean {
  return code.startsWith(PREP_PREFIX);
}

/**
 * Background items that are a whole undergraduate degree rather than a subject,
 * and the parts a degree of that kind actually supplies. Nothing in here is ever
 * scheduled.
 *
 * The parts are the ones this catalog already names elsewhere, so ticking them
 * off one at a time is a real path rather than a gesture: a reader who has the
 * mathematics, the physics, the electromagnetics and the digital logic has the
 * part of an EE degree these courses are reaching for.
 */
export const COMPOSITE: Record<string, string[]> = {
  bg_ee: [
    "bg_calc",
    "bg_de",
    "bg_la",
    "bg_cx",
    "bg_phys",
    "bg_circ",
    "bg_ugem",
    "bg_sig",
    "bg_dig",
  ],
  bg_eecs: ["bg_calc", "bg_de", "bg_dig", "bg_c"],
};

/**
 * The programme's admission prerequisites, from the ECE master's degree
 * requirements page (ep.jhu.edu, retrieved August 2026): mathematics through
 * vector calculus and differential equations, calculus-based physics, linear
 * and non-linear circuits, electromagnetics, and signals and systems. Each
 * needs a B- or better wherever it is taken.
 *
 * Missing ones do not bar enrolment - admission is provisional until they are
 * complete - so the planner schedules them rather than blocking on them: any
 * item neither ticked nor covered by a planned bridge course travels with
 * every plan as preparation.
 */
export const ADMISSION = [
  "bg_calc",
  "bg_de",
  "bg_phys",
  "bg_circ",
  "bg_ugem",
  "bg_sig",
];

export function isComposite(id: string): boolean {
  return id in COMPOSITE;
}

/** A composite is satisfied by holding it outright, or by holding every part. */
export function holdsBackground(id: string, held: Set<string>): boolean {
  if (held.has(id)) return true;
  const parts = COMPOSITE[id];
  return parts?.every((p) => held.has(p));
}

/** Parts of a composite still outstanding. Empty when it is satisfied. */
export function missingParts(id: string, held: Set<string>): string[] {
  if (holdsBackground(id, held)) return [];
  return (COMPOSITE[id] ?? []).filter((p) => !held.has(p));
}

/** True when a group member names a background item rather than a course. */
export function isBackgroundToken(member: string): boolean {
  return member.startsWith("bg_");
}

/**
 * Where each background item can be satisfied. A JHU course number means the
 * program teaches it; otherwise it is taken elsewhere and the note says where.
 */
export const PREP_SOURCE: Record<
  string,
  { jhu: string | null; where: string }
> = {
  bg_calc: {
    jhu: null,
    where:
      "Any community college. Montgomery College and Northern Virginia Community College both run the full sequence.",
  },
  bg_de: {
    jhu: null,
    where:
      "Community college, or the differential equations content inside EN.525.201 Circuits, Devices and Fields if you take that bridge course.",
  },
  bg_la: {
    jhu: "EN.625.609",
    where:
      "EN.625.609 Matrix Theory in the Applied and Computational Mathematics program covers this properly, including the singular value decomposition and the matrix exponential. A first linear algebra course usually stops short of both.",
  },
  bg_cx: {
    jhu: null,
    where:
      "Community college or self study. Named explicitly by EN.525.738 Advanced Antenna Systems and quietly assumed by everything with a transform in it.",
  },
  bg_phys: {
    jhu: null,
    where: "Calculus based physics, two semesters. Community college.",
  },
  bg_circ: {
    jhu: "EN.525.201",
    where:
      "Linear and non-linear circuits, an admission prerequisite for the degree. EN.525.201 Circuits, Devices and Fields is the bridge course JHU runs for exactly this gap; a community college circuit analysis sequence also serves.",
  },
  bg_ugem: {
    jhu: null,
    where:
      "Undergraduate electromagnetics, before Intermediate Electromagnetics. Ulaby, Fundamentals of Applied Electromagnetics, is the engineering standard.",
  },
  bg_sig: {
    jhu: "EN.525.202",
    where:
      "Signals and systems, an admission prerequisite for the degree. EN.525.202 Signals and Systems is the bridge course, and the signal processing foundation courses all assume the material.",
  },
  bg_dig: {
    jhu: null,
    where:
      "Digital logic and state machines. Harris and Harris, Digital Design and Computer Architecture, teaches logic and hardware description language together.",
  },
  bg_c: {
    jhu: null,
    where:
      "C and C++. Required outright by the two field programmable gate array laboratory courses.",
  },
  bg_matlab: {
    jhu: "EN.525.617",
    where:
      "EN.525.617 Computation for Engineers is the closest thing the program offers, though most courses simply assume you can drive MATLAB.",
  },
  bg_ee: {
    jhu: null,
    where:
      "A full undergraduate electrical engineering background. Several courses state this as their only prerequisite, which is not something you can schedule; treat it as the sum of the other items here.",
  },
};

/**
 * Return a catalog in which every unheld background item is a real course and a
 * real prerequisite. Held items vanish entirely, as though satisfied.
 *
 * `expanded` names composites the reader has asked to schedule the parts of. A
 * composite that is neither satisfied nor expanded adds no prerequisite at all:
 * the course stays takeable and the interface flags it, because refusing to
 * schedule a course on the strength of a prose sentence about a degree is a
 * judgement the reader should make, not the planner.
 */
export function withBackground(
  catalog: Catalog,
  background: [string, string][],
  held: Set<string>,
  expanded = new Set<string>(),
): Catalog {
  const out: Catalog = {};

  // Composites are never scheduled, so they never become preparation courses.
  for (const [id, label] of background) {
    if (isComposite(id) || held.has(id)) continue;
    const src = PREP_SOURCE[id] ?? {
      jhu: null,
      where: "Taken outside Johns Hopkins.",
    };
    const course: Course = {
      code: prepId(id),
      title: label,
      credits: 0,
      desc: src.where,
      prereq_text: "",
      groups: [],
      areas: ["Preparation"],
      level: 0,
      gradeable: false,
      external: false,
      excl: [],
      bg: [],
      prep: true,
      bgKey: id,
      jhuEquivalent: src.jhu,
    };
    out[course.code] = course;
  }

  for (const [code, c] of Object.entries(catalog)) {
    if (c.prep) continue; // drop preparation from a previous pass
    const groups = resolveGroups(c, catalog, held, expanded, out);
    out[code] = sameGroups(groups, c.groups) ? c : { ...c, groups };
  }
  return out;
}

/**
 * The groups a course actually has, once background is taken into account.
 *
 * A group may name a background item alongside real courses, which is how the
 * catalog's "either an undergraduate degree in electrical engineering or
 * EN.525.616" is written down. Holding the background satisfies that group
 * outright; not holding it leaves the courses beside it as the way through.
 */
function resolveGroups(
  c: Course,
  real: Catalog,
  held: Set<string>,
  expanded: Set<string>,
  prep: Catalog,
): string[][] {
  const groups: string[][] = [];

  for (const g of c.groups) {
    const tokens = g.filter(isBackgroundToken);
    if (tokens.some((t) => holdsBackground(t, held))) continue; // group satisfied by background
    const courses = g.filter((m) => !isBackgroundToken(m) && !isPrep(m));
    if (courses.length) groups.push(courses);
    // A group of nothing but unheld background falls through to c.bg below,
    // rather than becoming an empty group that nothing could ever satisfy.
    else
      for (const t of tokens)
        groups.push(...backgroundGroups(t, real, held, expanded, prep));
  }

  for (const b of c.bg)
    groups.push(...backgroundGroups(b, real, held, expanded, prep));
  return groups;
}

/** What an unsatisfied background item adds to a course's prerequisites. */
function backgroundGroups(
  id: string,
  real: Catalog,
  held: Set<string>,
  expanded: Set<string>,
  prep: Catalog,
): string[][] {
  if (holdsBackground(id, held)) return [];
  if (!isComposite(id)) {
    const g = subjectGroup(id, real, prep);
    return g ? [g] : [];
  }
  // A composite only becomes a prerequisite once the reader opts in to it.
  if (!expanded.has(id)) return [];
  return missingParts(id, held)
    .map((part) => subjectGroup(part, real, prep))
    .filter((g): g is string[] => g !== null);
}

/**
 * The group an unheld subject contributes: its preparation course, with the
 * JHU course that teaches the same material as an alternative when this
 * catalog has one. That keeps a plan already carrying the bridge course from
 * also being handed a preparation placeholder that says the same thing.
 */
function subjectGroup(
  id: string,
  real: Catalog,
  prep: Catalog,
): string[] | null {
  if (!prep[prepId(id)]) return null;
  const jhu = PREP_SOURCE[id]?.jhu;
  return jhu && real[jhu] ? [prepId(id), jhu] : [prepId(id)];
}

function sameGroups(a: string[][], b: string[][]): boolean {
  return (
    a.length === b.length &&
    a.every(
      (g, i) => g.length === b[i].length && g.every((m, j) => m === b[i][j]),
    )
  );
}
