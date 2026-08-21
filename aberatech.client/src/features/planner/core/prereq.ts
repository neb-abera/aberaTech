/** Prerequisite logic. Pure functions over plain data, no DOM, no globals. */
import type { Catalog, Placement } from './types';

export function groupsOf(cat: Catalog, code: string): string[][] {
  return cat[code]?.groups ?? [];
}

/**
 * Groups not satisfied for a course sitting at `term`.
 * A group is satisfied when some member is placed in a strictly earlier term.
 */
export function unmetGroups(cat: Catalog, code: string, term: number, placed: Placement): string[][] {
  return groupsOf(cat, code).filter((g) => !g.some((m) => placed.has(m) && (placed.get(m) ?? 0) < term));
}

const costCaches = new WeakMap<Catalog, Map<string, number>>();

function costMemo(cat: Catalog): Map<string, number> {
  const found = costCaches.get(cat);
  if (found) return found;
  const made = new Map<string, number>();
  costCaches.set(cat, made);
  return made;
}

/**
 * How many courses this one's prerequisite chain drags in. Used only to pick the
 * cheaper side of an OR. Memoised per catalog, safe against cycles.
 */
export function closureCost(
  cat: Catalog,
  code: string,
  seen = new Set<string>(),
  memo: Map<string, number> = costMemo(cat)
): number {
  if (!cat[code]) return Number.MAX_SAFE_INTEGER;
  const cached = memo.get(code);
  if (cached !== undefined) return cached;
  if (seen.has(code)) return 0;
  seen.add(code);
  let n = 1;
  for (const g of groupsOf(cat, code)) {
    const opts = g.filter((m) => cat[m]);
    if (opts.length) n += Math.min(...opts.map((m) => closureCost(cat, m, new Set(seen), memo)));
  }
  memo.set(code, n);
  return n;
}

/**
 * Transitive prerequisite closure of a set of courses.
 *
 * For an OR group, keeps a member that is already present, otherwise adds the
 * cheapest one. Prerequisites naming courses outside the catalog are skipped,
 * because nothing in the app can satisfy them.
 */
export function closure(cat: Catalog, codes: Iterable<string>): Set<string> {
  const need = new Set([...codes].filter((c) => cat[c]));
  for (let pass = 0; pass < 100; pass++) {
    let grew = false;
    for (const code of [...need]) {
      for (const g of groupsOf(cat, code)) {
        if (g.some((m) => need.has(m))) continue;
        const opts = g.filter((m) => cat[m]);
        if (!opts.length) continue;
        opts.sort((a, b) => closureCost(cat, a) - closureCost(cat, b) || a.localeCompare(b));
        need.add(opts[0]);
        grew = true;
      }
    }
    if (!grew) break;
  }
  return need;
}

/**
 * Courses that must be added before `code` can be placed at all, given what is
 * already selected. This is what the interface highlights for a blocked course.
 */
export function missingFor(cat: Catalog, code: string, have: Set<string>): string[] {
  const full = closure(cat, [...have, code]);
  return [...full].filter((c) => c !== code && !have.has(c)).sort();
}

/**
 * Earliest term `code` may occupy. Null when some group has no placed member,
 * meaning it cannot be placed anywhere yet.
 */
export function earliestTerm(cat: Catalog, code: string, placed: Placement): number | null {
  let e = 0;
  for (const g of groupsOf(cat, code)) {
    const terms = g.filter((m) => placed.has(m)).map((m) => placed.get(m) ?? 0);
    if (!terms.length) return null;
    e = Math.max(e, Math.min(...terms) + 1);
  }
  return e;
}

/**
 * Latest term `code` may occupy without stranding something that depends on it.
 * Only binding when this course is the sole placed satisfier of that group.
 */
export function latestTerm(cat: Catalog, code: string, placed: Placement): number {
  let l = Infinity;
  for (const [dep, depTerm] of placed) {
    if (dep === code) continue;
    for (const g of groupsOf(cat, dep)) {
      if (!g.includes(code)) continue;
      const others = g.some((m) => m !== code && placed.has(m) && (placed.get(m) ?? 0) < depTerm);
      if (!others) l = Math.min(l, depTerm - 1);
    }
  }
  return l;
}

/**
 * Every term index where `code` could legally sit. Capacity is deliberately not
 * consulted: a full term is a preference, not a prerequisite violation.
 */
export function legalTerms(cat: Catalog, code: string, placed: Placement, termCount: number): Set<number> {
  const without = new Map(placed);
  without.delete(code);
  const e = earliestTerm(cat, code, without);
  if (e === null) return new Set();
  const l = latestTerm(cat, code, without);
  const out = new Set<number>();
  for (let i = Math.max(0, e); i <= Math.min(l, termCount - 1); i++) out.add(i);
  return out;
}
