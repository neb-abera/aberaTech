import { describe, expect, test } from 'vitest';
import rawCatalog from '../../data/catalog.json';
import rawTracks from '../../data/tracks.json';
import { CatalogData } from '../../core/catalog';
import { Tracks } from '../../core/tracks';
import type { RawCatalog, RawTracks } from '../../core/types';
import { PlannerModel } from '../PlannerModel';

const data = new CatalogData(rawCatalog as unknown as RawCatalog);
const tracks = new Tracks(rawTracks as unknown as RawTracks);
const make = () => new PlannerModel(data, tracks);

/** A model browsing one focus area, scheduled. */
function rf(perTerm = 2) {
  const m = make();
  m.areas = new Set(['RF and Microwave Engineering']);
  m.perTerm = perTerm;
  m.rescheduleAll();
  return m;
}

describe('selection', () => {
  test('RF and Microwave alone pulls in its cross group prerequisites', () => {
    const auto = [...rf().autoAdded()].map((c) => data.title(c));
    expect(auto).toContain('Digital Signal Processing');
    expect(auto).toContain('Probability & Stochastic Processes for Engineers');
    expect(auto).toContain('Communication Systems Engineering');
  });
  test('scheduling leaves nothing unplaced for any single group', () => {
    for (const name of Object.keys(data.areas)) {
      const m = make();
      m.areas = new Set([name]);
      m.rescheduleAll();
      expect(m.unplaced(), `${name} left courses unplaced`).toEqual([]);
    }
  });
  test('turning automatic prerequisites off shrinks the selection to the raw choice', () => {
    const m = rf();
    const withAuto = m.selected().size;
    m.autoPrereq = false;
    expect(m.selected().size).toBeLessThan(withAuto);
    expect(m.autoAdded().size).toBe(0);
  });
  test('pulledBy names the course that forced an addition', () => {
    expect(rf().pulledBy('EN.525.627').length).toBeGreaterThan(0);
  });
  test('choosing a focus area clears the track, because they are alternatives', () => {
    const m = make();
    m.selectTrack('sp-rf');
    m.toggleArea('area', 'Signal Processing');
    expect(m.track).toBeNull();
    expect(m.areas.has('Signal Processing')).toBe(true);
  });
});

describe('preparation courses on the real catalog', () => {
  const bare = (perTerm = 2) => {
    const m = rf(perTerm);
    m.background = new Set();
    m.rebase();
    m.rescheduleAll();
    return m;
  };

  test('an unticked background item becomes a schedulable course', () => {
    const m = bare();
    expect(m.preparation().length).toBeGreaterThan(0);
    expect(m.preparation().every((c) => m.get(c)?.prep)).toBe(true);
  });
  test('it lands before the course that assumes it', () => {
    expect(bare().plan.violations()).toEqual([]);
  });
  test('ticking everything removes the preparation courses entirely', () => {
    const m = bare();
    m.background = new Set(data.background.map(([k]) => k));
    m.rebase();
    m.rescheduleAll();
    expect(m.preparation()).toEqual([]);
  });
  test('preparation never counts toward the ten', () => {
    const m = bare();
    expect(m.audit().counted.some((c) => m.get(c)?.prep)).toBe(false);
  });
  test('differential equations and linear algebra are not ticked by default', () => {
    const m = make();
    expect(m.background.has('bg_de')).toBe(false);
    expect(m.background.has('bg_la')).toBe(false);
  });
  test('setBackground reschedules so the plan never references a vanished course', () => {
    const m = bare();
    m.setBackground('bg_de', true);
    expect(m.plan.courses().every((c) => m.get(c))).toBe(true);
  });
});

describe('courses per term', () => {
  test('more than four a term is allowed', () => {
    const m = rf();
    m.setPerTerm(6);
    expect(m.plan.terms.some((t) => t.length > 4)).toBe(true);
    expect(m.plan.violations()).toEqual([]);
  });
  test('it is clamped to something a human could actually sit', () => {
    const m = rf();
    m.setPerTerm(99);
    expect(m.perTerm).toBe(8);
    m.setPerTerm(0);
    expect(m.perTerm).toBe(2);
  });
});

describe('the graduation clock on the real catalog', () => {
  test('a two course per term RF plan reports a graduation term and a deadline', () => {
    const a = rf().audit();
    expect(a.counted).toHaveLength(10);
    expect(a.clock.graduationLabel).toBeTruthy();
    expect(a.clock.deadline).toBeInstanceOf(Date);
    expect(a.clock.onTime).toBe(true);
  });
  test('taking the whole catalog does not itself blow the clock, because only ten are applied', () => {
    const m = make();
    m.areas = new Set(Object.keys(data.areas));
    m.perTerm = 1;
    m.rescheduleAll();
    const a = m.audit();
    expect(a.counted).toHaveLength(10);
    expect(a.clock.onTime).toBe(true);
    expect(m.plan.courses().length).toBeGreaterThan(100);
  });
  test('the clock spans the courses you APPLY, so a late pick can break it', () => {
    const m = make();
    m.areas = new Set(Object.keys(data.areas));
    m.perTerm = 1;
    m.rescheduleAll();
    const gradeable = m.plan.terms.flat().filter((c) => m.get(c)?.gradeable);
    m.degreePicks = new Set([...gradeable.slice(0, 9), ...gradeable.slice(-1)]);
    const a = m.audit();
    expect(a.counted).toHaveLength(10);
    expect(a.clock.onTime).toBe(false);
    expect(a.levers.length).toBeGreaterThan(0);
  });
  test('the bridge course does not start the clock', () => {
    const m = make();
    m.areas = new Set(['Bridge and other courses']);
    m.rescheduleAll();
    expect(m.audit().excluded.some((e) => e.code === 'EN.525.201')).toBe(true);
  });
});

describe('a track keeps its curated order', () => {
  const stageIndex = (m: PlannerModel, code: string) => {
    const t = m.track ? m.tracks.get(m.track) : undefined;
    return t ? t.stages.findIndex((s) => s.courses.includes(code)) : -1;
  };

  test('stages never run backwards, even with preparation courses inserted', () => {
    const m = make();
    m.background = new Set();
    m.selectTrack('collector-full');
    let highest = -1;
    for (const term of m.plan.terms) {
      for (const code of term) {
        const i = stageIndex(m, code);
        if (i < 0) continue; // a preparation course belongs to no stage
        expect(i, `${m.title(code)} runs before an earlier stage`).toBeGreaterThanOrEqual(highest);
        highest = Math.max(highest, i);
      }
    }
  });

  test('unticked background still schedules legally inside a track', () => {
    const m = make();
    m.background = new Set();
    m.selectTrack('sp-rf');
    expect(m.plan.violations()).toEqual([]);
    expect(m.preparation().length).toBeGreaterThan(0);
    expect(m.unplaced()).toEqual([]);
  });

  test('every track schedules with nothing left over once preparation is in play', () => {
    for (const t of tracks.all()) {
      const m = make();
      m.background = new Set();
      m.selectTrack(t.id);
      expect(m.plan.violations(), `${t.name} has a violation`).toEqual([]);
      expect(m.unplaced(), `${t.name} left courses unplaced`).toEqual([]);
    }
  });
});

describe('placing a course', () => {
  test('a blocked course is placed together with its prerequisites', () => {
    const m = make();
    m.selectTrack(null);
    m.areas = new Set(['Signal Processing']);
    m.clearPlan();
    const target = 'EN.525.728';
    const r = m.placeCourse(target, 0);
    expect(r.ok).toBe(true);
    expect(r.added.length).toBeGreaterThan(0);
    expect(m.plan.violations()).toEqual([]);
  });
  test('with automatic placement off, a blocked course is refused', () => {
    const m = make();
    m.clearPlan();
    m.autoOnDrop = false;
    expect(m.placeCourse('EN.525.728', 0).ok).toBe(false);
  });
  test('a course whose prerequisite is outside the catalog is never rescuable', () => {
    const m = make();
    expect(m.isRescuable('EN.525.622')).toBe(true);
  });
  test('removing a course clears the focus that pointed at it', () => {
    const m = rf();
    const code = m.plan.courses()[0];
    m.focus = code;
    m.removeCourse(code);
    expect(m.focus).toBeNull();
    expect(m.plan.has(code)).toBe(false);
  });
});

describe('why a term will or will not take a course', () => {
  /** A track plan, so the ordering cases are real ones. */
  const planned = () => {
    const m = make();
    m.background = new Set(data.background.map(([k]) => k));
    m.rebase();
    m.selectTrack('sp-rf');
    return m;
  };

  test('a term the course already sits in is legal', () => {
    const m = planned();
    const code = m.plan.terms[0][0];
    expect(m.placementNote(code, 0).kind).toBe('ok');
  });

  test('moving a foundation course past its dependents is a stranding, not a missing prerequisite', () => {
    const m = planned();
    const note = m.placementNote('EN.525.614', m.plan.terms.length - 1);
    expect(note.kind).toBe('strand');
    expect(m.acceptsDrop('EN.525.614', m.plan.terms.length - 1)).toBe(false);
  });

  test('moving a late course ahead of a prerequisite that is present names that prerequisite', () => {
    const m = planned();
    const note = m.placementNote('EN.525.738', 0);
    expect(note.kind).toBe('order');
    expect(note.courses).toContain('EN.525.618');
    // Nothing can be inserted to fix it, so the drop is refused rather than ignored.
    expect(m.acceptsDrop('EN.525.738', 0)).toBe(false);
  });

  test('an unplaced course whose prerequisites are absent can be rescued', () => {
    const m = planned();
    m.clearPlan();
    const note = m.placementNote('EN.525.728', 0);
    expect(note.kind).toBe('needs');
    expect(note.courses.length).toBeGreaterThan(0);
    expect(m.acceptsDrop('EN.525.728', 0)).toBe(true);
  });

  test('with automatic insertion off the same case is refused, and says why', () => {
    const m = planned();
    m.clearPlan();
    m.autoOnDrop = false;
    expect(m.placementNote('EN.525.728', 0).kind).toBe('needsOff');
    expect(m.acceptsDrop('EN.525.728', 0)).toBe(false);
  });

  test('every term a drop is accepted for actually accepts the course', () => {
    const m = planned();
    const code = 'EN.525.744';
    for (let i = 0; i < m.plan.terms.length; i++) {
      if (!m.acceptsDrop(code, i)) continue;
      const probe = planned();
      expect(probe.placeCourse(code, i).ok, `term ${i} was offered but refused the course`).toBe(true);
    }
  });
});

describe('degree picks', () => {
  test('the first manual pick materialises the automatic ten so editing starts from something real', () => {
    const m = rf();
    const extra = m.plan.courses().find((c) => m.get(c)?.gradeable && !m.audit().counted.includes(c));
    expect(extra).toBeDefined();
    if (!extra) return;
    m.toggleDegreePick(extra);
    expect(m.degreePicks.size).toBe(10);
    expect(m.degreePicks.has(extra)).toBe(true);
  });
  test('selecting a track clears any manual picks, because the set changed', () => {
    const m = rf();
    m.degreePicks = new Set(['EN.525.614']);
    m.selectTrack('sp-core');
    expect(m.degreePicks.size).toBe(0);
  });
});
