/**
 * Background requirements stated in prose become real, plannable prerequisites.
 * Ticking "I already have this" satisfies one; otherwise it must be placed.
 */
import { describe, expect, test } from 'vitest';
import { PREP_PREFIX, prepId, withBackground } from '../background';
import { closure, missingFor } from '../prereq';
import { Plan } from '../plan';
import { toyCourse, termOf } from './fixtures';
import type { Catalog } from '../types';

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
