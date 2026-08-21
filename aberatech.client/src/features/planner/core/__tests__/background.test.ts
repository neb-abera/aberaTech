/**
 * Background requirements stated in prose become real, plannable prerequisites.
 * Ticking "I already have this" satisfies one; otherwise it must be placed.
 */
import { describe, expect, test } from 'vitest';
import { COMPOSITE, PREP_PREFIX, holdsBackground, missingParts, prepId, withBackground } from '../background';
import { CatalogData } from '../catalog';
import { closure, missingFor } from '../prereq';
import { Plan } from '../plan';
import { toyCourse, termOf } from './fixtures';
import type { Catalog, RawCatalog } from '../types';

const BG: [string, string][] = [
  ['bg_de', 'Differential Equations'],
  ['bg_la', 'Linear Algebra and Matrix Theory']
];

const BASE: Catalog = {
  A: toyCourse('A', 'Alpha', { bg: ['bg_de'] }),
  B: toyCourse('B', 'Bravo', { groups: [['A']], level: 7, bg: ['bg_de', 'bg_la'] }),
  C: toyCourse('C', 'Charlie')
};

describe('withBackground', () => {
  test('adds one preparation course per background item', () => {
    const cat = withBackground(BASE, BG, new Set());
    expect(cat[prepId('bg_de')]).toBeDefined();
    expect(cat[prepId('bg_de')].title).toBe('Differential Equations');
    expect(cat[prepId('bg_de')].prep).toBe(true);
  });
  test('a preparation course never counts toward the degree', () => {
    expect(withBackground(BASE, BG, new Set())[prepId('bg_de')].gradeable).toBe(false);
  });
  test('an unheld background item becomes a hard prerequisite', () => {
    expect(withBackground(BASE, BG, new Set()).A.groups).toEqual([[prepId('bg_de')]]);
  });
  test('a held background item adds no prerequisite at all', () => {
    const cat = withBackground(BASE, BG, new Set(['bg_de']));
    expect(cat.A.groups).toEqual([]);
    expect(cat[prepId('bg_de')]).toBeUndefined();
  });
  test('existing course prerequisites are preserved alongside', () => {
    expect(withBackground(BASE, BG, new Set()).B.groups).toEqual([['A'], [prepId('bg_de')], [prepId('bg_la')]]);
  });
  test('a course with no background is untouched', () => {
    expect(withBackground(BASE, BG, new Set()).C.groups).toEqual([]);
  });
  test('it does not mutate the catalog it was given', () => {
    withBackground(BASE, BG, new Set());
    expect(BASE.A.groups).toEqual([]);
  });
});

describe('planning with background', () => {
  test('a course cannot be placed before its preparation', () => {
    const cat = withBackground(BASE, BG, new Set());
    const p = Plan.fromTerms(cat, [[prepId('bg_de')], []]);
    expect(p.legalTermsFor('A').has(0)).toBe(false);
    expect(p.legalTermsFor('A').has(1)).toBe(true);
  });
  test('ticking the background frees the course immediately', () => {
    const cat = withBackground(BASE, BG, new Set(['bg_de']));
    expect(Plan.fromTerms(cat, [[]]).legalTermsFor('A').has(0)).toBe(true);
  });
  test('closure pulls preparation courses in like any other prerequisite', () => {
    const full = closure(withBackground(BASE, BG, new Set()), ['B']);
    expect(full.has(prepId('bg_de'))).toBe(true);
    expect(full.has(prepId('bg_la'))).toBe(true);
  });
  test('missingFor names the preparation you still owe', () => {
    expect(missingFor(withBackground(BASE, BG, new Set()), 'A', new Set())).toEqual([prepId('bg_de')]);
  });
  test('automatic placement schedules preparation ahead of the course', () => {
    const cat = withBackground(BASE, BG, new Set());
    const r = Plan.empty(cat).placeWithPrerequisites('B', 3, 2);
    expect(r.ok).toBe(true);
    expect(termOf(r.plan, prepId('bg_de'))).toBeLessThan(termOf(r.plan, 'A'));
    expect(termOf(r.plan, 'A')).toBeLessThan(termOf(r.plan, 'B'));
    expect(r.plan.violations()).toEqual([]);
  });
  test('preparation ids are recognisable so the interface can style them', () => {
    expect(prepId('bg_de').startsWith(PREP_PREFIX)).toBe(true);
  });
});

/**
 * A degree is not a course. "An undergraduate degree in electrical engineering"
 * is stated as a prerequisite by five courses in the real catalog, and an
 * earlier version scheduled it as a single preparation course, which put four
 * years of work in one slot of one term.
 */
describe('a composite background item', () => {
  const COMP: [string, string][] = [
    ['bg_calc', 'Calculus I through III'],
    ['bg_de', 'Differential Equations'],
    ['bg_la', 'Linear Algebra and Matrix Theory'],
    ['bg_cx', 'Complex Variables'],
    ['bg_phys', 'Calculus based Physics I and II'],
    ['bg_ugem', 'Undergraduate Electromagnetics'],
    ['bg_dig', 'Digital Logic and State Machines'],
    ['bg_ee', 'A full undergraduate EE degree']
  ];
  const PARTS = COMPOSITE.bg_ee;
  /** G asks for the degree outright; H offers a course as the alternative. */
  const CAT: Catalog = {
    G: toyCourse('G', 'Gated', { bg: ['bg_ee'] }),
    H: toyCourse('H', 'Either way', { groups: [['C', 'bg_ee']] }),
    C: toyCourse('C', 'Charlie')
  };

  test('is never scheduled, however little of it is held', () => {
    expect(withBackground(CAT, COMP, new Set())[prepId('bg_ee')]).toBeUndefined();
  });

  test('constrains nothing until the reader opts in, so the course stays takeable', () => {
    expect(withBackground(CAT, COMP, new Set()).G.groups).toEqual([]);
  });

  test('opting in schedules the parts that are missing, and only those', () => {
    const cat = withBackground(CAT, COMP, new Set(['bg_calc', 'bg_phys']), new Set(['bg_ee']));
    const want = PARTS.filter((p) => p !== 'bg_calc' && p !== 'bg_phys').map((p) => [prepId(p)]);
    expect(cat.G.groups).toEqual(want);
    expect(cat[prepId('bg_ee')]).toBeUndefined();
  });

  test('holding every part satisfies it without ticking it', () => {
    const held = new Set(PARTS);
    expect(holdsBackground('bg_ee', held)).toBe(true);
    expect(missingParts('bg_ee', held)).toEqual([]);
    expect(withBackground(CAT, COMP, held, new Set(['bg_ee'])).G.groups).toEqual([]);
  });

  test('ticking it outright satisfies it too, without ticking seven boxes', () => {
    expect(holdsBackground('bg_ee', new Set(['bg_ee']))).toBe(true);
  });

  test('names the parts still outstanding', () => {
    expect(missingParts('bg_ee', new Set(['bg_calc', 'bg_de']))).toEqual(
      PARTS.filter((p) => p !== 'bg_calc' && p !== 'bg_de')
    );
  });
});

/**
 * The catalog writes "either an undergraduate degree in electrical engineering
 * or EN.525.616" as one sentence. It is an OR, and storing it as a course
 * prerequisite plus a separate background requirement made it an AND, which
 * demanded both.
 */
describe('a background item written beside a course', () => {
  const COMP: [string, string][] = [['bg_ee', 'A full undergraduate EE degree']];
  const CAT: Catalog = {
    H: toyCourse('H', 'Either way', { groups: [['C', 'bg_ee']] }),
    C: toyCourse('C', 'Charlie')
  };

  test('leaves the course as the way through when the background is not held', () => {
    expect(withBackground(CAT, COMP, new Set()).H.groups).toEqual([['C']]);
  });

  test('satisfies the group outright when the background is held', () => {
    expect(withBackground(CAT, COMP, new Set(['bg_ee'])).H.groups).toEqual([]);
  });

  test('is an alternative rather than an assumption, so it is not reported as missing', () => {
    const data = new CatalogData({
      courses: CAT,
      areas: {},
      concentrations: {},
      background: COMP
    } as unknown as RawCatalog);
    expect(data.missingBackground('H', new Set())).toEqual([]);
    expect(data.missingBackground('G', new Set())).toEqual([]);
  });
});
