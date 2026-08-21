import { describe, expect, test } from 'vitest';
import { TOY, termOf } from './fixtures';
import { Plan } from '../plan';

const planOf = (terms: string[][]) => Plan.fromTerms(TOY, terms);

describe('placement', () => {
  test('a legal move succeeds', () => {
    const r = planOf([['A'], ['B'], []]).place('B', 2);
    expect(r.ok).toBe(true);
    expect(r.plan.termOf('B')).toBe(2);
  });
  test('a move before a prerequisite is refused and the plan is unchanged', () => {
    const p = planOf([['A'], ['B']]);
    const r = p.place('B', 0);
    expect(r.ok).toBe(false);
    expect(r.reason).toMatch(/prerequisite/i);
    // A refused move returns the same plan object, so a caller can compare by identity.
    expect(r.plan).toBe(p);
  });
  test('a move that would strand a dependent is refused', () => {
    expect(planOf([['A'], ['B'], ['C']]).place('A', 2).ok).toBe(false);
  });
  test('plans are immutable: placing returns a new plan', () => {
    const p = planOf([['A'], [], []]);
    const r = p.place('A', 2);
    expect(p.termOf('A')).toBe(0);
    expect(r.plan.termOf('A')).toBe(2);
  });
});

describe('placeWithPrerequisites', () => {
  test('inserts the missing chain ahead of the course', () => {
    const r = planOf([[], [], []]).placeWithPrerequisites('C', 2);
    expect(r.ok).toBe(true);
    expect([...r.added].sort()).toEqual(['A', 'B']);
    const q = r.plan;
    expect(termOf(q, 'A')).toBeLessThan(termOf(q, 'B'));
    expect(termOf(q, 'B')).toBeLessThan(termOf(q, 'C'));
    expect(q.termOf('C')).toBe(2);
  });
  test('creates earlier terms when there is not enough room ahead', () => {
    const r = planOf([[]]).placeWithPrerequisites('C', 0);
    expect(r.ok).toBe(true);
    expect(termOf(r.plan, 'A')).toBeLessThan(termOf(r.plan, 'B'));
    expect(termOf(r.plan, 'B')).toBeLessThan(termOf(r.plan, 'C'));
  });
  test('adds nothing when the prerequisites are already placed', () => {
    const r = planOf([['A'], ['B'], []]).placeWithPrerequisites('C', 2);
    expect(r.added).toEqual([]);
    expect(r.plan.termOf('C')).toBe(2);
  });
  test('satisfies an or group with the cheaper branch', () => {
    expect(planOf([[], []]).placeWithPrerequisites('E', 1).added).toEqual(['F']);
  });
  test('reuses an or branch that is already in the plan', () => {
    expect(planOf([['A'], ['B'], []]).placeWithPrerequisites('E', 2).added).toEqual([]);
  });
  test('fails cleanly when a prerequisite is not a course in the catalog', () => {
    const r = planOf([[]]).placeWithPrerequisites('G', 0);
    expect(r.ok).toBe(false);
    expect(r.reason).toMatch(/not (a course )?in the catalog/i);
  });
  test('the resulting plan has no prerequisite violations', () => {
    expect(planOf([[], [], []]).placeWithPrerequisites('D', 2).plan.violations()).toEqual([]);
  });
});

describe('autoSchedule', () => {
  test('orders every course legally', () => {
    const p = Plan.autoSchedule(TOY, ['C', 'B', 'A', 'D'], 2);
    expect(p.violations()).toEqual([]);
    expect(p.courses()).toHaveLength(4);
  });
  test('respects the courses per term setting', () => {
    const p = Plan.autoSchedule(TOY, ['A', 'B', 'C'], 1);
    expect(p.terms.every((t) => t.length <= 1)).toBe(true);
  });
  test('leaves nothing behind when the set is closed', () => {
    expect(Plan.autoSchedule(TOY, ['A', 'B', 'C', 'D', 'E', 'F'], 2).courses()).toHaveLength(6);
  });
  test('more than four courses a term is allowed', () => {
    const p = Plan.autoSchedule(TOY, ['A', 'F', 'M', 'N', 'P', 'Q'], 6);
    expect(p.terms[0]).toHaveLength(6);
  });
});

describe('violations', () => {
  test('reports a mutual exclusion', () => {
    const v = planOf([['M'], ['N']]).violations();
    expect(v).toHaveLength(1);
    expect(v[0].kind).toBe('exclusion');
  });
  test('reports an unmet prerequisite', () => {
    expect(Plan.fromTerms(TOY, [['B']]).violations()[0].kind).toBe('prerequisite');
  });
});
