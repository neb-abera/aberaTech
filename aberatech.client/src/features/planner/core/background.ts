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
 */
import type { Catalog, Course } from './types';

export const PREP_PREFIX = 'prep.';

export function prepId(bgKey: string): string {
  return PREP_PREFIX + bgKey;
}

export function isPrep(code: string): boolean {
  return code.startsWith(PREP_PREFIX);
}

/**
 * Where each background item can be satisfied. A JHU course number means the
 * program teaches it; otherwise it is taken elsewhere and the note says where.
 */
export const PREP_SOURCE: Record<string, { jhu: string | null; where: string }> = {
  bg_calc: {
    jhu: null,
    where:
      'Any community college. Montgomery College and Northern Virginia Community College both run the full sequence.'
  },
  bg_de: {
    jhu: null,
    where:
      'Community college, or the differential equations content inside EN.525.201 Circuits, Devices and Fields if you take that bridge course.'
  },
  bg_la: {
    jhu: 'EN.625.609',
    where:
      'EN.625.609 Matrix Theory in the Applied and Computational Mathematics program covers this properly, including the singular value decomposition and the matrix exponential. A first linear algebra course usually stops short of both.'
  },
  bg_cx: {
    jhu: null,
    where:
      'Community college or self study. Named explicitly by EN.525.738 Advanced Antenna Systems and quietly assumed by everything with a transform in it.'
  },
  bg_phys: { jhu: null, where: 'Calculus based physics, two semesters. Community college.' },
  bg_ugem: {
    jhu: null,
    where:
      'Undergraduate electromagnetics, before Intermediate Electromagnetics. Ulaby, Fundamentals of Applied Electromagnetics, is the engineering standard.'
  },
  bg_dig: {
    jhu: null,
    where:
      'Digital logic and state machines. Harris and Harris, Digital Design and Computer Architecture, teaches logic and hardware description language together.'
  },
  bg_c: {
    jhu: null,
    where: 'C and C++. Required outright by the two field programmable gate array laboratory courses.'
  },
  bg_matlab: {
    jhu: 'EN.525.617',
    where:
      'EN.525.617 Computation for Engineers is the closest thing the program offers, though most courses simply assume you can drive MATLAB.'
  },
  bg_ee: {
    jhu: null,
    where:
      'A full undergraduate electrical engineering background. Several courses state this as their only prerequisite, which is not something you can schedule; treat it as the sum of the other items here.'
  }
};

/**
 * Return a catalog in which every unheld background item is a real course and a
 * real prerequisite. Held items vanish entirely, as though satisfied.
 */
export function withBackground(catalog: Catalog, background: [string, string][], held: Set<string>): Catalog {
  const needed = background.filter(([id]) => !held.has(id));
  if (!needed.length) return stripPrep(catalog);

  const out: Catalog = {};
  for (const [id, label] of needed) {
    const src = PREP_SOURCE[id] ?? { jhu: null, where: 'Taken outside Johns Hopkins.' };
    const course: Course = {
      code: prepId(id),
      title: label,
      credits: 0,
      desc: src.where,
      prereq_text: '',
      groups: [],
      areas: ['Preparation'],
      level: 0,
      gradeable: false,
      external: false,
      excl: [],
      bg: [],
      prep: true,
      bgKey: id,
      jhuEquivalent: src.jhu
    };
    out[course.code] = course;
  }
  for (const [code, c] of Object.entries(catalog)) {
    if (c.prep) continue; // drop preparation from a previous pass
    const extra = c.bg.filter((b) => !held.has(b)).map((b) => [prepId(b)]);
    out[code] = extra.length ? { ...c, groups: [...c.groups, ...extra] } : c;
  }
  return out;
}

function stripPrep(catalog: Catalog): Catalog {
  const out: Catalog = {};
  for (const [code, c] of Object.entries(catalog)) {
    if (c.prep) continue;
    out[code] = c.groups.some((g) => g.some(isPrep)) ? { ...c, groups: c.groups.filter((g) => !g.some(isPrep)) } : c;
  }
  return out;
}
