/**
 * The Plan aggregate: an ordered list of terms, each holding course codes.
 *
 * Plans are immutable. Every operation returns `{ ok, plan, ... }` and a refused
 * operation returns the original plan object, so a caller can compare by identity.
 */
import { closure, earliestTerm, legalTerms, missingFor, unmetGroups } from './prereq';
import type { Catalog, Placement } from './types';

export interface PlaceResult {
  ok: boolean;
  plan: Plan;
  reason?: string;
}

export interface PlaceWithPrerequisitesResult extends PlaceResult {
  added: string[];
}

export type ViolationKind = 'prerequisite' | 'exclusion';

export interface Violation {
  kind: ViolationKind;
  code: string;
  detail: string;
  group?: string[];
}

export class Plan {
  readonly catalog: Catalog;
  readonly terms: readonly string[][];

  constructor(catalog: Catalog, terms: string[][]) {
    this.catalog = catalog;
    this.terms = terms.map((t) => [...t]);
  }

  static fromTerms(catalog: Catalog, terms: string[][]): Plan {
    return new Plan(catalog, terms.length ? terms : [[]]);
  }

  static empty(catalog: Catalog): Plan {
    return new Plan(catalog, [[]]);
  }

  placement(): Placement {
    const m: Placement = new Map();
    this.terms.forEach((t, i) => {
      t.forEach((c) => m.set(c, i));
    });
    return m;
  }

  courses(): string[] {
    return this.terms.flat();
  }

  termOf(code: string): number | undefined {
    return this.placement().get(code);
  }

  has(code: string): boolean {
    return this.placement().has(code);
  }

  /** Terms where `code` could legally sit. */
  legalTermsFor(code: string): Set<number> {
    return legalTerms(this.catalog, code, this.placement(), this.terms.length);
  }

  /** A copy with the given terms, trimmed to exactly one trailing empty term. */
  withTerms(terms: string[][]): Plan {
    const t = terms.map((x) => [...x]);
    while (t.length > 1 && t[t.length - 1].length === 0 && t[t.length - 2].length === 0) t.pop();
    if (t.length === 0 || t[t.length - 1].length > 0) t.push([]);
    return new Plan(this.catalog, t);
  }

  /** Move or add a course. Refuses anything that would break prerequisite order. */
  place(code: string, term: number): PlaceResult {
    if (!this.catalog[code]) return { ok: false, plan: this, reason: `${code} is not in the catalog` };
    const terms = this.terms.map((t) => t.filter((c) => c !== code));
    while (terms.length <= term) terms.push([]);
    const probe = new Plan(this.catalog, terms);
    if (!probe.legalTermsFor(code).has(term)) {
      const unmet = unmetGroups(this.catalog, code, term, probe.placement());
      const why = unmet.length
        ? `prerequisite not met: needs ${unmet.map((g) => g.join(' or ')).join(', then ')} in an earlier term`
        : 'prerequisite order: moving it here would strand a course that depends on it';
      return { ok: false, plan: this, reason: why };
    }
    terms[term] = [...terms[term], code];
    return { ok: true, plan: this.withTerms(terms) };
  }

  /**
   * The golden path: drop a blocked course and its missing prerequisites are
   * inserted ahead of it automatically, in a legal order.
   */
  placeWithPrerequisites(code: string, term: number, perTerm = Infinity): PlaceWithPrerequisitesResult {
    if (!this.catalog[code]) {
      return { ok: false, plan: this, added: [], reason: `${code} is not in the catalog` };
    }
    const unreachable = this.catalog[code].groups.filter((g) => !g.some((m) => this.catalog[m]));
    if (unreachable.length) {
      return {
        ok: false,
        plan: this,
        added: [],
        reason: `${code} requires ${unreachable.flat().join(', ')}, which is not in the catalog`
      };
    }
    const have = new Set(this.courses());
    const need = missingFor(this.catalog, code, have);
    if (!need.length) {
      return { ...this.place(code, term), added: [] };
    }
    // Order the additions so each one's own prerequisites land first.
    const ordered = topoOrder(this.catalog, need, have);
    const terms = this.terms.map((t) => [...t]);
    const added: string[] = [];
    let target = term;
    for (const c of ordered) {
      const found = earliestTerm(this.catalog, c, new Plan(this.catalog, terms).placement());
      const e = found ?? 0;
      // The first term at or after `e` that is still before the target and has room.
      let slot = -1;
      for (let i = e; i < Math.max(target, terms.length); i++) {
        if (i >= target) break;
        if ((terms[i]?.length ?? 0) < perTerm) {
          slot = i;
          break;
        }
      }
      if (slot < 0) {
        // No room ahead of the target, so open a new term just before it.
        terms.splice(Math.max(e, 0), 0, [c]);
        target += 1;
      } else {
        while (terms.length <= slot) terms.push([]);
        terms[slot] = [...terms[slot], c];
      }
      added.push(c);
    }
    const r = new Plan(this.catalog, terms).place(code, target);
    if (!r.ok) return { ok: false, plan: this, added: [], reason: r.reason };
    return { ok: true, plan: r.plan, added };
  }

  remove(code: string): PlaceResult {
    return { ok: true, plan: this.withTerms(this.terms.map((t) => t.filter((c) => c !== code))) };
  }

  /** Every rule broken by the plan as it stands. */
  violations(): Violation[] {
    const p = this.placement();
    const out: Violation[] = [];
    for (const [code, term] of p) {
      for (const g of unmetGroups(this.catalog, code, term, p)) {
        out.push({ kind: 'prerequisite', code, group: g, detail: g.join(' or ') });
      }
      for (const x of this.catalog[code]?.excl ?? []) {
        if (p.has(x) && code < x) out.push({ kind: 'exclusion', code, detail: x });
      }
    }
    return out;
  }

  /** Build a legal schedule from a set of courses. */
  static autoSchedule(catalog: Catalog, codes: Iterable<string>, perTerm = 2): Plan {
    const remaining = new Set(closure(catalog, codes));
    const terms: string[][] = [];
    const dependents = (c: string) =>
      [...remaining].filter((k) => (catalog[k]?.groups ?? []).some((g) => g.includes(c))).length;
    let guard = 0;
    while (remaining.size && guard++ < 1000) {
      const p = new Plan(catalog, terms).placement();
      const ti = terms.length;
      const ready = [...remaining].filter((c) => unmetGroups(catalog, c, ti, p).length === 0);
      if (!ready.length) break;
      ready.sort(
        (a, b) =>
          catalog[a].groups.length - catalog[b].groups.length || dependents(b) - dependents(a) || a.localeCompare(b)
      );
      const take = ready.slice(0, perTerm);
      terms.push(take);
      take.forEach((c) => remaining.delete(c));
    }
    return new Plan(catalog, terms.length ? terms : [[]]);
  }
}

/** Order `need` so each course follows its own prerequisites. */
function topoOrder(catalog: Catalog, need: string[], have: Set<string>): string[] {
  const set = new Set(need);
  const out: string[] = [];
  const seen = new Set(have);
  let guard = 0;
  while (set.size && guard++ < 500) {
    let progressed = false;
    for (const c of [...set].sort()) {
      const ready = (catalog[c]?.groups ?? []).every((g) => g.some((m) => seen.has(m)) || !g.some((m) => catalog[m]));
      if (ready) {
        out.push(c);
        seen.add(c);
        set.delete(c);
        progressed = true;
      }
    }
    if (!progressed) {
      out.push(...[...set].sort());
      break;
    }
  }
  return out;
}
