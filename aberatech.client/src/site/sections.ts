/**
 * What the site contains, in one place.
 *
 * The nav, the two index pages, the page headings and the footer all read from
 * here, so a card and the page it opens cannot end up with different titles.
 */

export interface Entry {
  /** The page's own heading. Cards and the page itself both use this. */
  title: string;
  /** A shorter label for the drawer and the footer, where the full title is unwieldy. */
  navLabel?: string;
  /** An internal route, or an absolute URL for anything on another origin. */
  to: string;
  blurb: string;
  /** Set when `to` leaves this site, so links open in a new tab and say so. */
  external?: boolean;
}

export const guides: Entry[] = [
  {
    title: "The Military Transition Guide I Wish I Had",
    navLabel: "Military Transition Guide",
    to: "/transition",
    blurb:
      "What to do and when, from eighteen months before ETS to long after it.",
  },
  {
    title: "Learning Software Development",
    to: "/technical",
    blurb: "The path from no background to working in software development.",
  },
];

export const projects: Entry[] = [
  {
    title: "Learning RF and Signal Processing",
    to: "/planner",
    blurb:
      "Plan a Johns Hopkins Engineering for Professionals Electrical and Computer Engineering master’s degree.",
  },
  {
    title: "Schedule time with me",
    to: "/schedule",
    blurb: "Book a time, or join the queue. Confirmed by text.",
  },
  {
    title: "Military athlete console",
    to: "/fitness",
    blurb:
      "Verified training data in, sourced predictions out. Model the dose, or price a goal against a date.",
  },
  {
    title: "Facewoof",
    to: "https://facewoof.abera.tech",
    blurb: "A play dating app for dogs.",
    external: true,
  },
];

/**
 * The one action in the bar, rather than another place to browse.
 *
 * Found by path rather than by index. This was `projects[1]`, which would have
 * quietly promoted a different project to the bar's button the first time
 * anyone inserted an entry above it.
 */
const scheduling = projects.find((entry) => entry.to === "/schedule");
if (!scheduling)
  throw new Error("sections: /schedule is missing from projects");
export const primaryAction: Entry = scheduling;

/** What to show where space is tight. */
export const label = (entry: Entry): string => entry.navLabel ?? entry.title;
