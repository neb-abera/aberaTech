/**
 * Planner state and the values derived from it. Knows nothing about React or
 * the DOM, so every rule in here is testable without rendering anything.
 */
import { Calendar } from '../core/calendar';
import { CatalogData } from '../core/catalog';
import type { Gate } from '../core/catalog';
import { ADMISSION, holdsBackground, isPrep, PREP_SOURCE, prepId, withBackground } from '../core/background';
import { Plan } from '../core/plan';
import { closure, missingFor, unmetGroups } from '../core/prereq';
import { degreeAudit } from '../core/rules';
import type { DegreeAudit } from '../core/rules';
import { expandInOrder, placeInOrder, Tracks } from '../core/tracks';
import type { Catalog, Course } from '../core/types';

/** Stable key for the background sets, so the derived catalog is cached. */
function bgKey(held: Set<string>, expanded: Set<string>): string {
  return [...held].sort().join('|') + '::' + [...expanded].sort().join('|');
}

export const DEFAULT_TRACK = 'sp-rf';
export const MAX_PER_TERM = 8;

/**
 * Why a course may or may not sit in a term.
 *
 *  - `ok`          it is legal there
 *  - `needs`       prerequisites are absent, and the planner may insert them
 *  - `needsOff`    the same, but automatic insertion is switched off
 *  - `order`       the prerequisite IS in the plan, just not early enough
 *  - `strand`      moving it there would strand a course that depends on it
 *  - `unreachable` a prerequisite names a course that is not in the catalog
 *
 * The distinction matters because only `needs` can be fixed by dropping the
 * course. Offering the others would promise something the planner cannot do.
 */
export type PlacementKind = 'ok' | 'needs' | 'needsOff' | 'order' | 'strand' | 'unreachable';

export interface PlacementNote {
  kind: PlacementKind;
  /** Courses to add, for `needs`, or to move up, for `order`. */
  courses: string[];
}

export class PlannerModel {
  readonly data: CatalogData;
  readonly tracks: Tracks;

  /** Selected track id, or null when browsing focus areas instead. */
  track: string | null = null;
  areas = new Set<string>(['Signal Processing', 'RF and Microwave Engineering']);
  conc = new Set<string>();
  /**
   * Background the reader already has. Only what a West Point core genuinely
   * covers is ticked to begin with; everything else starts unticked, so the
   * planner asks rather than assumes.
   */
  background = new Set<string>(['bg_calc', 'bg_phys']);
  /**
   * Composite background the reader has asked to schedule the parts of. Until a
   * composite is in here it constrains nothing, because "an undergraduate degree
   * in electrical engineering" is a sentence about a person, not a prerequisite
   * the planner should enforce on its own initiative.
   */
  expandedBackground = new Set<string>();
  perTerm = 2;
  termsPerYear = 2;
  startTerm = 'Spring';
  startYear = 2027;
  autoPrereq = true;
  autoOnDrop = true;
  leaveMonths = 0;
  extensionMonths = 0;
  /** Course whose missing prerequisites are highlighted, or null. */
  focus: string | null = null;
  /** Courses applied to the degree. Empty means "chosen automatically". */
  degreePicks = new Set<string>();
  /** Courses inserted by the most recent automatic placement. */
  lastAdded: string[] = [];
  plan: Plan;

  private cachedBgKey: string | null = null;
  private cachedCourses: Catalog = {};

  constructor(data: CatalogData, tracks: Tracks) {
    this.data = data;
    this.tracks = tracks;
    this.plan = Plan.empty(this.courses);
  }

  /**
   * The catalog the planner actually works against: the real courses plus a
   * preparation course for every background item not yet ticked.
   */
  get courses(): Catalog {
    const key = bgKey(this.background, this.expandedBackground);
    if (this.cachedBgKey !== key) {
      this.cachedBgKey = key;
      this.cachedCourses = withBackground(
        this.data.courses,
        this.data.background,
        this.background,
        this.expandedBackground
      );
    }
    return this.cachedCourses;
  }

  get(code: string): Course | undefined {
    return this.courses[code];
  }

  title(code: string): string {
    return this.courses[code]?.title ?? code;
  }

  get calendar(): Calendar {
    return new Calendar({
      startTerm: this.startTerm,
      startYear: this.startYear,
      termsPerYear: this.termsPerYear
    });
  }

  /** What the reader ticked. A track, when one is selected, otherwise the areas. */
  chosen(): Set<string> {
    const t = this.track ? this.tracks.get(this.track) : undefined;
    const out = t ? new Set(t.courses.filter((c) => this.courses[c])) : new Set<string>();
    // A track and a focus area layer rather than replace. Picking a curated path
    // and then adding a whole area on top of it is a real thing to want, and an
    // earlier version threw the path away the moment an area was ticked.
    for (const c of this.data.select([...this.areas], [...this.conc])) {
      if (this.courses[c]) out.add(c);
    }
    // The admission prerequisites travel with every plan. One the reader has
    // not ticked, and is not covering with the JHU course that teaches it, is
    // planned as preparation; admission is provisional until they are done.
    if (out.size) {
      for (const id of ADMISSION) {
        if (holdsBackground(id, this.background)) continue;
        const jhu = PREP_SOURCE[id]?.jhu;
        if (jhu && out.has(jhu)) continue;
        if (this.courses[prepId(id)]) out.add(prepId(id));
      }
    }
    return out;
  }

  /**
   * Select a track. Tracks replace the selection rather than adding to it,
   * because the whole point of a track is that it is a specific set.
   */
  selectTrack(id: string | null): void {
    this.track = id;
    this.degreePicks.clear();
    const t = id ? this.tracks.get(id) : undefined;
    if (t) {
      this.areas.clear();
      this.conc.clear();
      // Through scheduleFor rather than tracks.toPlan, so the admission
      // preparation and the track's own background land on the board too.
      this.rescheduleAll();
    } else {
      this.plan = this.plan.withTerms([[]]);
    }
  }

  /** Turn a focus area or transcript concentration on or off. */
  /**
   * Turn a focus area or transcript concentration on or off.
   *
   * This changes what is *selected*, and deliberately does not touch the board.
   * It used to prune the plan down to the new selection, which only ever removed
   * courses: ticking an area emptied most of the plan and added nothing. Adding
   * is now an explicit act, so nothing you have arranged disappears behind a
   * checkbox. See addSelectionToPlan and replacePlanWithSelection.
   */
  toggleArea(kind: 'area' | 'conc', name: string): void {
    const set = kind === 'area' ? this.areas : this.conc;
    if (set.has(name)) set.delete(name);
    else set.add(name);
  }

  /** Selected courses that are not on the board yet. What "Add these" would add. */
  pendingFromSelection(): string[] {
    const have = new Set(this.plan.courses());
    return [...this.selected()].filter((c) => !have.has(c)).sort();
  }

  /**
   * Put the selection on the board, keeping what is already there.
   *
   * The union is rescheduled rather than appended, so the result is a legal plan
   * rather than the old one with courses bolted after it. Returns what arrived.
   */
  addSelectionToPlan(): string[] {
    const added = this.pendingFromSelection();
    if (!added.length) return [];
    this.plan = this.scheduleFor([...this.plan.courses(), ...this.selected()]);
    this.lastAdded = added;
    return added;
  }

  /** Clear the board and build it from the selection alone. */
  replacePlanWithSelection(): void {
    this.degreePicks.clear();
    this.rescheduleAll();
    this.lastAdded = [];
  }

  /** Record that the reader does or does not already hold a background item. */
  setBackground(id: string, held: boolean): void {
    if (held) {
      this.background.add(id);
      this.expandedBackground.delete(id); // nothing to schedule once it is held
    } else this.background.delete(id);
    this.rebase();
    this.rescheduleAll();
  }

  /** What a course assumes that the reader does not hold, ready to display. */
  gates(code: string): Gate[] {
    return this.data.gates(code, this.background);
  }

  /**
   * Schedule the parts of a composite the reader is missing, or stop doing so.
   * Turning it on makes those parts genuine prerequisites, so the automatic
   * placer inserts them exactly as it would any other.
   */
  expandBackground(id: string, on: boolean): void {
    if (on) this.expandedBackground.add(id);
    else this.expandedBackground.delete(id);
    this.rebase();
    this.rescheduleAll();
  }

  /** What the plan needs: the choice plus its prerequisite closure. */
  selected(): Set<string> {
    const base = this.chosen();
    return this.autoPrereq ? closure(this.courses, base) : base;
  }

  /** Courses present only because something else requires them. */
  autoAdded(): Set<string> {
    if (!this.autoPrereq) return new Set();
    const base = this.chosen();
    return new Set([...this.selected()].filter((c) => !base.has(c)));
  }

  /** Which chosen courses forced `code` into the set. */
  pulledBy(code: string): string[] {
    const sel = this.selected();
    return [...sel].filter((k) =>
      (this.courses[k]?.groups ?? []).some((g) => g.includes(code) && !g.some((m) => m !== code && sel.has(m)))
    );
  }

  unplaced(): string[] {
    return [...this.selected()].filter((c) => !this.plan.has(c));
  }

  audit(): DegreeAudit {
    return degreeAudit(this.plan, this.calendar, {
      leaveMonths: this.leaveMonths,
      extensionMonths: this.extensionMonths,
      picks: this.degreePicks
    });
  }

  /** Preparation courses currently sitting in the plan. */
  preparation(): string[] {
    return this.plan.courses().filter(isPrep);
  }

  /** Toggle whether a course is applied to the degree. */
  toggleDegreePick(code: string): void {
    if (this.degreePicks.has(code)) {
      this.degreePicks.delete(code);
      return;
    }
    if (!this.degreePicks.size) {
      // Materialise the automatic ten so the reader edits from a real starting point.
      this.audit().counted.forEach((c) => this.degreePicks.add(c));
    }
    if (this.degreePicks.size >= 10) this.degreePicks.delete([...this.degreePicks][0]);
    this.degreePicks.add(code);
  }

  setPerTerm(n: number): void {
    this.perTerm = Math.min(MAX_PER_TERM, Math.max(1, Math.round(n) || 2));
    this.rescheduleAll();
  }

  rescheduleAll(): void {
    this.plan = this.scheduleFor(this.selected());
  }

  /**
   * Schedule a set of courses.
   *
   * A track is laid out in its curated order, because that order is the
   * pedagogy: the receiver tracks walk the signal chain from the antenna inward.
   * Courses a focus area adds on top of a track follow it, by prerequisite
   * depth, so layering an area on a path does not shuffle the path. With no
   * track there is no curated order to keep, so depth alone decides.
   */
  private scheduleFor(codes: Iterable<string>): Plan {
    const want = new Set([...codes].filter((c) => this.courses[c]));
    const t = this.track ? this.tracks.get(this.track) : undefined;
    if (!t) return Plan.autoSchedule(this.courses, want, this.perTerm);
    // Preparation no course in the plan asks for - the admission prerequisites
    // - comes first, because admission comes first. Preparation a course does
    // need is left to expandInOrder, which puts it immediately ahead of the
    // course that asked.
    const needed = new Set([...want].flatMap((c) => this.courses[c]?.groups.flat() ?? []));
    const front = [...want].filter((c) => isPrep(c) && !needed.has(c)).sort();
    const onFront = new Set(front);
    const curated = t.courses.filter((c) => want.has(c));
    const onCurated = new Set(curated);
    const rest = [...want].filter((c) => !onCurated.has(c) && !onFront.has(c));
    return placeInOrder(this.courses, expandInOrder(this.courses, [...front, ...curated, ...rest]), this.perTerm);
  }

  clearPlan(): void {
    this.plan = this.plan.withTerms([[]]);
    this.degreePicks.clear();
  }

  /** Which curated stage a course belongs to in the active track, if any. */
  stageOf(code: string): string | null {
    const t = this.track ? this.tracks.get(this.track) : undefined;
    if (!t) return null;
    return t.stages.find((s) => s.courses.includes(code))?.name ?? null;
  }

  /** Rebuild the plan against the current derived catalog, dropping what vanished. */
  rebase(): void {
    const cat = this.courses;
    this.plan = new Plan(
      cat,
      this.plan.terms.map((t) => t.filter((c) => cat[c]))
    );
  }

  /** Drop everything no longer selected. */
  pruneToSelection(): void {
    const sel = this.selected();
    this.plan = this.plan.withTerms(this.plan.terms.map((t) => t.filter((c) => sel.has(c))));
  }

  /**
   * A course the planner could place if it first inserted the missing
   * prerequisites. False when some prerequisite names a course that is not in
   * the catalog at all, because nothing here can satisfy that.
   */
  isRescuable(code: string): boolean {
    const c = this.get(code);
    if (!c) return false;
    return c.groups.every((g) => g.some((m) => this.get(m)));
  }

  /** Whether `code` may sit at `term`, and if not, precisely why. */
  placementNote(code: string, term: number): PlacementNote {
    if (this.plan.legalTermsFor(code).has(term)) return { kind: 'ok', courses: [] };
    const cat = this.courses;
    const elsewhere = this.plan.placement();
    elsewhere.delete(code);
    const unmet = unmetGroups(cat, code, term, elsewhere);
    if (!unmet.length) return { kind: 'strand', courses: [] };
    const have = new Set(this.plan.courses().filter((c) => c !== code));
    const missing = missingFor(cat, code, have);
    if (!missing.length) {
      // Everything it needs is in the plan, just not far enough ahead.
      return { kind: 'order', courses: unmet.flat().filter((m) => cat[m]) };
    }
    if (!this.isRescuable(code)) return { kind: 'unreachable', courses: missing };
    return { kind: this.autoOnDrop ? 'needs' : 'needsOff', courses: missing };
  }

  /** Whether a drop on `term` should be accepted at all. */
  acceptsDrop(code: string, term: number): boolean {
    const kind = this.placementNote(code, term).kind;
    return kind === 'ok' || kind === 'needs';
  }

  /**
   * Place a course in a term, pulling in prerequisites when that is allowed and
   * necessary. Returns what was added so the interface can say so.
   */
  placeCourse(code: string, term: number): { ok: boolean; added: string[]; reason?: string } {
    if (this.plan.legalTermsFor(code).has(term)) {
      const r = this.plan.place(code, term);
      if (r.ok) {
        this.plan = r.plan;
        this.focus = null;
        this.lastAdded = [];
      }
      return { ok: r.ok, added: [], reason: r.reason };
    }
    if (!this.autoOnDrop) return { ok: false, added: [], reason: 'prerequisites are not in the plan yet' };
    const r = this.plan.placeWithPrerequisites(code, term, this.perTerm);
    if (r.ok) {
      this.plan = r.plan;
      this.lastAdded = r.added;
      this.focus = null;
    }
    return { ok: r.ok, added: r.added, reason: r.reason };
  }

  /**
   * Place a course where it belongs in the sequence, prerequisites and all.
   *
   * This used to mean the last term, unconditionally, which read as nonsense
   * for anything the sequence constrains: preparation re-added after a removal
   * landed after the coursework it exists to precede. Preparation goes into
   * the earliest term that has room. Anything else goes as late as its placed
   * dependents allow, which for a course nothing depends on is still the end.
   * A course whose prerequisites are not on the board yet keeps the old path,
   * so the automatic inserter can bring them along.
   */
  autoPlace(code: string): { ok: boolean; added: string[]; reason?: string } {
    const last = Math.max(this.plan.terms.length - 1, 0);
    const legal = [...this.plan.legalTermsFor(code)].sort((a, b) => a - b);
    if (!legal.length) return this.placeCourse(code, last);
    const room = legal.filter((t) => (this.plan.terms[t]?.length ?? 0) < this.perTerm);
    const pick = room.length ? room : legal;
    const target = isPrep(code) ? pick[0] : Math.min(last, pick[pick.length - 1]);
    return this.placeCourse(code, target);
  }

  removeCourse(code: string): void {
    this.plan = this.plan.remove(code).plan;
    if (this.focus === code) this.focus = null;
  }
}
