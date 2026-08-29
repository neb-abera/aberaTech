/** A small hand built catalog, so core tests do not depend on the real 138 courses. */
import type { Catalog, Course } from "../types";

function course(
  code: string,
  title: string,
  over: Partial<Course> = {},
): Course {
  return {
    code,
    title,
    credits: 3,
    desc: "",
    prereq_text: "",
    groups: [],
    areas: ["X"],
    level: 6,
    gradeable: true,
    external: false,
    excl: [],
    bg: [],
    ...over,
  };
}

export const TOY: Catalog = {
  A: course("A", "Alpha"),
  B: course("B", "Bravo", { groups: [["A"]] }),
  C: course("C", "Charlie", { groups: [["B"]], level: 7 }),
  D: course("D", "Delta", { groups: [["A"], ["B"]], level: 7, areas: ["Y"] }),
  E: course("E", "Echo", { groups: [["B", "F"]], level: 7, areas: ["Y"] }),
  F: course("F", "Foxtrot", { areas: ["Y"] }),
  G: course("G", "Golf", { groups: [["Z"]], areas: ["Y"] }),
  P: course("P", "Papa", { level: 2, gradeable: false }),
  Q: course("Q", "Quebec", { external: true }),
  M: course("M", "Mike", { excl: ["N"] }),
  N: course("N", "November"),
};

export { course as toyCourse };

/** The term a course sits in, or a failure. Keeps assertions free of optionals. */
export function termOf(
  plan: { termOf: (code: string) => number | undefined },
  code: string,
): number {
  const t = plan.termOf(code);
  if (t === undefined) throw new Error(`${code} is not in the plan`);
  return t;
}
