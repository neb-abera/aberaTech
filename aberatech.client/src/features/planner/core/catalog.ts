/** Loading and querying the parsed JHU catalog. */
import type { Catalog, Course, RawCatalog } from './types';

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

  /** Background items a course assumes but the planner cannot enforce directly. */
  missingBackground(code: string, held: Set<string>): string[] {
    return (this.courses[code]?.bg ?? []).filter((b) => !held.has(b));
  }

  backgroundLabel(id: string): string {
    return (this.background.find(([k]) => k === id) ?? [null, id])[1] ?? id;
  }
}
