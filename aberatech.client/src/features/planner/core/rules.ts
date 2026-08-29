/**
 * Degree rules and the five year clock for the JHU Engineering for Professionals
 * MS in Electrical and Computer Engineering.
 *
 * Source: JHU e-catalogue, Engineering for Professionals academic regulations and
 * the ECE master's programme page, retrieved August 2026.
 *
 *  - Ten courses, 30 credits, within five years.
 *  - The five years begin "with the start of the first course applied to the
 *    student's program". Courses numbered 100 to 500 are undergraduate level and
 *    confer no graduate credit, so they are not applied and do not start it.
 *  - At least seven courses from the program, meaning EN.525 or EN.520.
 *  - At least four courses at the 700 level or above.
 *  - At most three courses from outside the program.
 *  - A voluntary leave of absence extends the limit by its length, capped at two years.
 *  - A documented extenuating circumstances exception may extend it by up to two years.
 */
import { Calendar } from "./calendar";
import type { Plan } from "./plan";
import { chooseDegreeCourses } from "./select";

export const LIMITS = Object.freeze({
  COURSES: 10,
  IN_PROGRAM: 7,
  LEVEL_700: 4,
  MAX_OUTSIDE: 3,
  YEARS: 5,
  MAX_LEAVE_MONTHS: 24,
  MAX_EXTENSION_MONTHS: 24,
});

export interface RuleRow {
  id: string;
  label: string;
  have: number;
  need: number;
  met: boolean;
  /** True when the rule is a ceiling rather than a floor. */
  inverse?: boolean;
}

export interface Blocker {
  id: string;
  detail: string;
}

export interface Lever {
  id: string;
  title: string;
  detail: string;
}

export interface Clock {
  startTerm: number | null;
  startDate: Date | null;
  finishTerm: number | null;
  projected: Date | null;
  deadline: Date | null;
  slackMonths: number | null;
  onTime: boolean | null;
  leaveMonths: number;
  extensionMonths: number;
  graduationTerm: number | null;
  graduationLabel: string | null;
  deadlineTerm: number | null;
}

export interface DegreeAudit {
  counted: string[];
  excluded: { code: string; why: string }[];
  rules: RuleRow[];
  blockers: Blocker[];
  selection: { automatic: boolean; why: string };
  readyToGraduate: boolean;
  clock: Clock;
  /** Levers available if the clock is tight, in the order to reach for them. */
  levers: Lever[];
}

export interface AuditOptions {
  leaveMonths?: number;
  extensionMonths?: number;
  /**
   * The courses to apply to the degree. When absent, a rule satisfying ten is
   * chosen automatically. The clock spans the courses you apply, not everything
   * you take, so applying one that sits late in the plan can break the five year
   * limit even though the plan itself is fine.
   */
  picks?: Set<string>;
}

export function degreeAudit(
  plan: Plan,
  calendar: Calendar,
  opts: AuditOptions = {},
): DegreeAudit {
  const cat = plan.catalog;
  const leave = clamp(opts.leaveMonths ?? 0, 0, LIMITS.MAX_LEAVE_MONTHS);
  const extension = clamp(
    opts.extensionMonths ?? 0,
    0,
    LIMITS.MAX_EXTENSION_MONTHS,
  );

  const picks = opts.picks?.size ? opts.picks : null;
  const candidates: { code: string; term: number }[] = [];
  const excluded: { code: string; why: string }[] = [];
  const termOf = new Map<string, number>();
  plan.terms.forEach((t, i) => {
    for (const code of [...t].sort()) {
      const c = cat[code];
      if (!c) continue;
      termOf.set(code, i);
      if (!c.gradeable) {
        excluded.push({
          code,
          why: "carries no graduate credit, so it cannot be applied to the degree",
        });
        continue;
      }
      candidates.push({ code, term: i });
    }
  });

  let counted: string[];
  let selection: { automatic: boolean; why: string };
  if (picks) {
    counted = candidates.filter((c) => picks.has(c.code)).map((c) => c.code);
    selection = { automatic: false, why: "you chose these by hand" };
  } else {
    const r = chooseDegreeCourses(cat, candidates, {
      courses: LIMITS.COURSES,
      level700: LIMITS.LEVEL_700,
      maxOutside: LIMITS.MAX_OUTSIDE,
      inProgram: LIMITS.IN_PROGRAM,
    });
    counted = r.picked;
    selection = { automatic: true, why: r.why };
  }
  const countedSet = new Set(counted);
  for (const c of candidates) {
    if (!countedSet.has(c.code))
      excluded.push({
        code: c.code,
        why: "taken, but not applied to the degree",
      });
  }
  counted.sort(
    (a, b) => (termOf.get(a) ?? 0) - (termOf.get(b) ?? 0) || a.localeCompare(b),
  );

  const startTerm = counted.length ? (termOf.get(counted[0]) ?? null) : null;
  const finishTerm = counted.length
    ? (termOf.get(counted[counted.length - 1]) ?? null)
    : null;
  const startDate = startTerm === null ? null : calendar.startDate(startTerm);
  const deadline =
    startDate === null
      ? null
      : addMonths(
          Calendar.addYears(startDate, LIMITS.YEARS),
          leave + extension,
        );
  const projected = finishTerm === null ? null : calendar.endDate(finishTerm);
  const slackMonths =
    deadline && projected ? Calendar.monthsBetween(projected, deadline) : null;

  const inProgram = counted.filter((c) => !cat[c].external).length;
  const level700 = counted.filter((c) => cat[c].level >= 7).length;
  const outside = counted.filter((c) => cat[c].external).length;

  const rules: RuleRow[] = [
    {
      id: "count",
      label: `${LIMITS.COURSES} courses applied to the degree`,
      have: counted.length,
      need: LIMITS.COURSES,
      met: counted.length >= LIMITS.COURSES,
    },
    {
      id: "inProgram",
      label: `at least ${LIMITS.IN_PROGRAM} from the program`,
      have: inProgram,
      need: LIMITS.IN_PROGRAM,
      met: inProgram >= LIMITS.IN_PROGRAM,
    },
    {
      id: "level700",
      label: `at least ${LIMITS.LEVEL_700} at the 700 level`,
      have: level700,
      need: LIMITS.LEVEL_700,
      met: level700 >= LIMITS.LEVEL_700,
    },
    {
      id: "outside",
      label: `at most ${LIMITS.MAX_OUTSIDE} from outside the program`,
      have: outside,
      need: LIMITS.MAX_OUTSIDE,
      met: outside <= LIMITS.MAX_OUTSIDE,
      inverse: true,
    },
  ];

  const blockers: Blocker[] = rules
    .filter((r) => !r.met)
    .map((r) => ({ id: r.id, detail: unmetDetail(r) }));
  const onTime = slackMonths === null ? null : slackMonths >= 0;
  if (onTime === false && slackMonths !== null) {
    blockers.push({
      id: "clock",
      detail: `the five year limit is exceeded by ${Math.abs(slackMonths)} months; the tenth course finishes ${fmt(projected)} against a deadline of ${fmt(deadline)}`,
    });
  }
  for (const v of plan.violations()) {
    blockers.push({
      id: v.kind,
      detail:
        v.kind === "exclusion"
          ? `${v.code} and ${v.detail} are mutually exclusive`
          : `${v.code} sits before its prerequisite ${v.detail}`,
    });
  }

  return {
    counted,
    excluded,
    rules,
    blockers,
    selection,
    readyToGraduate: blockers.length === 0,
    clock: {
      startTerm,
      startDate,
      finishTerm,
      projected,
      deadline,
      slackMonths,
      onTime,
      leaveMonths: leave,
      extensionMonths: extension,
      graduationTerm: finishTerm,
      graduationLabel: finishTerm === null ? null : calendar.label(finishTerm),
      deadlineTerm: deadline === null ? null : termForDate(calendar, deadline),
    },
    levers: buildLevers(slackMonths, leave, extension),
  };
}

function unmetDetail(r: RuleRow): string {
  return r.inverse
    ? `${r.label}: you have ${r.have}`
    : `${r.label}: you have ${r.have} of ${r.need}`;
}

function buildLevers(
  slack: number | null,
  leave: number,
  extension: number,
): Lever[] {
  if (slack === null || slack >= 0) return [];
  const out: Lever[] = [
    {
      id: "compress",
      title: "Take more courses per term",
      detail:
        "The cheapest fix. Moving from one course a term to two roughly halves the elapsed time.",
    },
  ];
  if (leave < LIMITS.MAX_LEAVE_MONTHS) {
    out.push({
      id: "leave",
      title: "Voluntary leave of absence",
      detail: `Extends the limit by the length of the leave, up to ${LIMITS.MAX_LEAVE_MONTHS} months in total. Requires approval from the Associate Vice Dean.`,
    });
  }
  if (extension < LIMITS.MAX_EXTENSION_MONTHS) {
    out.push({
      id: "extension",
      title: "Time limitation exception",
      detail: `Up to ${LIMITS.MAX_EXTENSION_MONTHS} more months for documented extenuating circumstances. The regulations name extended military deployment specifically.`,
    });
  }
  out.push({
    id: "revalidate",
    title: "Course revalidation",
    detail:
      "Up to two courses, or 20 percent of the requirement, may be revalidated rather than retaken. Nothing older than seven years qualifies.",
  });
  return out;
}

function termForDate(cal: Calendar, date: Date): number | null {
  for (let i = 0; i < 200; i++) if (cal.startDate(i) > date) return i - 1;
  return null;
}

const clamp = (n: number, lo: number, hi: number) =>
  Math.min(hi, Math.max(lo, Number(n) || 0));
const addMonths = (d: Date, m: number) =>
  new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + m, d.getUTCDate()));
const fmt = (d: Date | null) => (d ? d.toISOString().slice(0, 10) : "unknown");
