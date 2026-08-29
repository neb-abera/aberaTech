/**
 * Links out to Johns Hopkins for a course.
 *
 * History worth keeping, because it cost trust: the first version of this file
 * built a per-course address on ep.jhu.edu by guessing a slug pattern, and got
 * it wrong. Every course link 404ed. The address had never been opened.
 *
 * The rule now is that a link is either measured or it is labelled. The
 * catalogue address below is measured: `npm run check:links` requests all 138
 * and asserts the course block it returns carries the same title this catalog
 * holds. `data/link-check.json` is the recorded result of that run, and it is
 * regenerated rather than edited.
 */
import type { CourseLink } from "./types";

/** The whole Electrical and Computer Engineering course list. */
export const CATALOGUE_INDEX =
  "https://e-catalogue.jhu.edu/course-descriptions/electrical_and_computer_engineering/";

/**
 * One course in the JHU academic catalogue. The catalogue's own course bubbles
 * use this address, so it is the university's link, not a constructed one.
 */
export function catalogueUrl(code: string): string {
  return `https://e-catalogue.jhu.edu/search/?P=${encodeURIComponent(code)}`;
}

/**
 * The Engineering for Professionals course page, which carries what the
 * catalogue does not: which terms it next runs, the instructor and the cost.
 *
 * The address is derived, not measured: the course number with its dots
 * removed, then the title slugged. Confirmed against Detection & Estimation
 * Theory, Antenna Systems, and Probability & Stochastic Processes for
 * Engineers. It is reported as unverified because the whole set has not been
 * requested from an environment that can reach that host.
 */
export function professionalsUrl(code: string, title: string): string {
  const num = code.replace(/^EN\./, "").replace(/\./g, "");
  const slug = title
    .toLowerCase()
    .replace(/&/g, " ")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return `https://ep.jhu.edu/courses/${num}-${slug}/`;
}

export function courseLinks(code: string, title: string): CourseLink[] {
  return [
    {
      label: "Catalogue entry",
      href: catalogueUrl(code),
      verified: true,
      note: "the official course description in the JHU academic catalogue",
    },
    {
      label: "Offerings",
      href: professionalsUrl(code, title),
      verified: false,
      note: "terms, instructor and cost on ep.jhu.edu; the address is derived from the course number and title rather than measured",
    },
  ];
}
