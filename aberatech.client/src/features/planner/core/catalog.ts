/** Loading and querying the parsed JHU catalog. */
import { holdsBackground, isBackgroundToken, isComposite, missingParts } from './background';
import type { Catalog, Course, RawCatalog } from './types';

/** A background item a course assumes, and what is still outstanding under it. */
export interface Gate {
  id: string;
  label: string;
  /** Composites only: the parts not yet held. Empty for a plain item. */
  missing: string[];
  composite: boolean;
}

export class CatalogData {
  readonly courses: Catalog;
  readonly areas: Record<string, string[]>;
  readonly concentrations: Record<string, string[]>;
  readonly background: [string, string][];

  constructor(raw: RawCatalog) {
    this.courses = raw.courses;
    this.areas = raw.areas;
    this.concentrations = raw.concentrations;
    this.background = raw.background;
  }

  get codes(): string[] {
    return Object.keys(this.courses);
  }

  get(code: string): Course | undefined {
    return this.courses[code];
  }

  title(code: string): string {
    return this.courses[code]?.title ?? code;
  }

  /** Courses in the given area and concentration names. */
  select(areaNames: string[], concNames: string[] = []): Set<string> {
    const s = new Set<string>();
    for (const a of areaNames) for (const c of this.areas[a] ?? []) if (this.courses[c]) s.add(c);
    for (const a of concNames) for (const c of this.concentrations[a] ?? []) if (this.courses[c]) s.add(c);
    return s;
  }

  /**
   * Background items a course assumes but the planner cannot enforce directly.
   *
   * A background item written inside a group alongside real courses is an
   * alternative, not an assumption: the catalog's "either an undergraduate
   * degree in electrical engineering or EN.525.616" is satisfiable by taking
   * EN.525.616, so reporting it as assumed would be a lie. Only a group with
   * nothing else in it counts.
   */
  missingBackground(code: string, held: Set<string>): string[] {
    const c = this.courses[code];
    if (!c) return [];
    const ids = new Set<string>(c.bg);
    for (const g of c.groups) {
      if (g.some((m) => !isBackgroundToken(m))) continue;
      for (const m of g) if (isBackgroundToken(m)) ids.add(m);
    }
    return [...ids].filter((b) => !holdsBackground(b, held));
  }

  /** The same, described well enough for the interface to offer a way through. */
  gates(code: string, held: Set<string>): Gate[] {
    return this.missingBackground(code, held).map((id) => ({
      id,
      label: this.backgroundLabel(id),
      missing: missingParts(id, held).map((p) => this.backgroundLabel(p)),
      composite: isComposite(id)
    }));
  }

  backgroundLabel(id: string): string {
    return (this.background.find(([k]) => k === id) ?? [null, id])[1] ?? id;
  }
}
