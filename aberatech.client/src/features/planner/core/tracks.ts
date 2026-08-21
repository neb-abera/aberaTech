/**
 * Curated course tracks.
 *
 * A focus area is a bulk listing of everything JHU files under a heading, which
 * is useful for browsing and useless as a plan. A track is a specific, ordered
 * set of courses chosen for a purpose, prerequisite closed, and checked against
 * the degree rules by the test suite.
 */
import { Plan } from './plan';
import { closureCost, earliestTerm } from './prereq';
import type { Catalog, RawTracks, Track } from './types';

export class Tracks {
  readonly map: Record<string, Track> = {};

  constructor(raw: RawTracks) {
    for (const [id, t] of Object.entries(raw)) {
      this.map[id] = { id, ...t, courses: t.stages.flatMap((s) => s.courses) };
    }
  }

  all(): Track[] {
    return Object.values(this.map);
  }

  get(id: string): Track | undefined {
    return this.map[id];
  }

  byKind(kind: Track['kind']): Track[] {
    return this.all().filter((t) => t.kind === kind);
  }

  /**
   * Schedule a track in its curated order, bumping any course whose prerequisite
   * is not yet placed. Ordered rather than optimised, because a track's order is
   * the pedagogy: the collector track walks the signal chain from the aperture in.
   */
  toPlan(catalog: Catalog, id: string, perTerm = 2): Plan {
    const t = this.get(id);
    if (!t) return Plan.empty(catalog);
    return placeInOrder(catalog, expandInOrder(catalog, t.courses), perTerm);
  }
}

/**
 * The same order, with anything a course needs inserted just before it.
 *
 * A track is prerequisite closed against the plain catalog, but not against the
 * derived one: leaving a background item unticked adds a preparation course that
 * no track lists. Expanding here keeps the curated order intact and puts the
 * newcomer immediately ahead of whatever asked for it, rather than handing the
 * whole set to a scheduler that would sort the pedagogy away.
 */
export function expandInOrder(cat: Catalog, order: string[]): string[] {
  const out: string[] = [];
  const seen = new Set<string>();
  const emit = (code: string, depth = 0) => {
    if (seen.has(code) || !cat[code] || depth > 50) return;
    seen.add(code); // marked before recursing, so a cycle cannot spin
    for (const g of cat[code].groups) {
      if (g.some((m) => seen.has(m))) continue;
      const opts = g.filter((m) => cat[m]);
      if (!opts.length) continue;
      opts.sort((a, b) => closureCost(cat, a) - closureCost(cat, b) || a.localeCompare(b));
      emit(opts[0], depth + 1);
    }
    out.push(code);
  };
  for (const c of order) emit(c);
  return out;
}

/**
 * Place courses in the given order, each in the earliest term that is legal and
 * has room, never before a course listed ahead of it.
 */
export function placeInOrder(cat: Catalog, order: string[], perTerm = 2): Plan {
  const terms: string[][] = [];
  const placed = new Map<string, number>();
  let floor = 0; // never place earlier than the previous course
  for (const code of order) {
    if (!cat[code] || placed.has(code)) continue;
    const e = earliestTerm(cat, code, placed);
    let ti = Math.max(floor, e ?? 0);
    for (;;) {
      while (terms.length <= ti) terms.push([]);
      if (terms[ti].length < perTerm) break;
      ti++;
    }
    terms[ti].push(code);
    placed.set(code, ti);
    floor = Math.max(0, ti - 1); // allow pairing within a term, not backtracking
  }
  return new Plan(cat, terms.length ? terms : [[]]);
}
