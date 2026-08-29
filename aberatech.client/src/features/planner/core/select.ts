/**
 * Choosing which ten courses to apply to the degree.
 *
 * Taking the first ten in term order is what a naive planner does and it is
 * almost always wrong: a plan ordered by prerequisite puts the foundation
 * courses first, so the first ten are all 600 level and the "at least four at
 * the 700 level" rule can never be met. This picks a set that satisfies the
 * rules, and among satisfying sets prefers the one spanning the fewest terms,
 * because the five year clock runs from the first applied course to the last.
 */
import type { Catalog } from "./types";

export interface Candidate {
  code: string;
  term: number;
}

export interface DegreeLimits {
  courses: number;
  level700: number;
  maxOutside: number;
  inProgram: number;
}

export interface DegreeSelection {
  picked: string[];
  automatic: true;
  why: string;
}

export function chooseDegreeCourses(
  cat: Catalog,
  candidates: Candidate[],
  limits: DegreeLimits,
): DegreeSelection {
  const items = [...candidates].sort(
    (a, b) => a.term - b.term || a.code.localeCompare(b.code),
  );
  if (items.length <= limits.courses) {
    return {
      picked: items.map((i) => i.code),
      automatic: true,
      why: shortfallWhy(items.length, limits),
    };
  }
  let best: { picked: string[]; span: number } | null = null;
  for (let i = 0; i < items.length; i++) {
    for (let j = i + limits.courses - 1; j < items.length; j++) {
      const pick = pickFrom(cat, items.slice(i, j + 1), limits);
      if (!pick) continue;
      const span = items[j].term - items[i].term;
      if (!best || span < best.span) best = { picked: pick, span };
      break; // the smallest j for this i is the tightest window starting here
    }
  }
  if (!best) {
    // No satisfying set exists, so show the earliest ten and let the audit
    // report precisely which rule is out of reach.
    return {
      picked: items.slice(0, limits.courses).map((i) => i.code),
      automatic: true,
      why: "no set of ten in this plan can satisfy every rule, so the earliest ten are shown and the failing rule is listed below",
    };
  }
  return {
    picked: best.picked,
    automatic: true,
    why: `chosen to satisfy every degree rule, including at least ${limits.level700} at the 700 level, while spanning the fewest terms so the five year clock stays as short as possible`,
  };
}

/** Draw a rule satisfying set of `limits.courses` from a window, or null. */
function pickFrom(
  cat: Catalog,
  window: Candidate[],
  limits: DegreeLimits,
): string[] | null {
  const inProg = window.filter((w) => !cat[w.code].external);
  const ext = window.filter((w) => cat[w.code].external);
  const sevens = inProg.filter((w) => cat[w.code].level >= 7);
  const rest = inProg.filter((w) => cat[w.code].level < 7);
  if (inProg.length < limits.inProgram) return null;
  if (
    sevens.length + ext.filter((w) => cat[w.code].level >= 7).length <
    limits.level700
  )
    return null;

  const chosen: Candidate[] = [];
  const take = (arr: Candidate[], n: number) => {
    for (const x of arr) {
      if (chosen.length >= n) break;
      if (!chosen.includes(x)) chosen.push(x);
    }
  };
  take(sevens, limits.level700); // the binding rule first
  if (chosen.filter((w) => cat[w.code].level >= 7).length < limits.level700) {
    take(
      [...chosen, ...ext.filter((w) => cat[w.code].level >= 7)],
      limits.level700,
    );
  }
  take([...chosen, ...rest], limits.courses); // fill from within the program
  take([...chosen, ...sevens, ...rest], limits.courses);
  if (chosen.length < limits.courses) {
    const room =
      limits.maxOutside - chosen.filter((w) => cat[w.code].external).length;
    take([...chosen, ...ext.slice(0, Math.max(0, room))], limits.courses);
  }
  if (chosen.length < limits.courses) return null;
  const set = chosen.slice(0, limits.courses);
  if (set.filter((w) => cat[w.code].level >= 7).length < limits.level700)
    return null;
  if (set.filter((w) => !cat[w.code].external).length < limits.inProgram)
    return null;
  if (set.filter((w) => cat[w.code].external).length > limits.maxOutside)
    return null;
  return set.map((w) => w.code);
}

function shortfallWhy(n: number, limits: DegreeLimits): string {
  return n === limits.courses
    ? `exactly ${limits.courses} countable courses are placed, so all of them are applied`
    : `only ${n} of the ${limits.courses} required courses are placed, so every countable course is applied`;
}
