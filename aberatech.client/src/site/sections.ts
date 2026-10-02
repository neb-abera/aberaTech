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
    blurb: "From no programming background to a job in software.",
  },
  {
    title: "Tactically Relevant RF Training",
    navLabel: "Field RF Training",
    to: "/rf-training",
    blurb:
      "A one-year plan for field radio: wire antennas and HF, networks, mesh, spectrum, drones and power.",
  },
  {
    title: "Learning Signal Processing",
    navLabel: "Signal Processing",
    to: "/signal-processing",
    blurb:
      "A three-year plan, from the first Fourier transform to reproducing papers. Each block ends in a gate.",
  },
  {
    title: "Learning Quantum and Post-Quantum Cryptography",
    navLabel: "Quantum Cryptography",
    to: "/quantum-cryptography",
    blurb:
      "An eighteen-month plan, from the arithmetic under RSA to implementing ML-KEM from its FIPS.",
  },
];

export const projects: Entry[] = [
  {
    title: "Graduate course planner",
    navLabel: "Course planner",
    to: "/planner",
    blurb:
      "A constraint solver over the 138 courses of the Johns Hopkins electrical and computer engineering master’s: prerequisites, degree rules and the five-year clock, checked as you drag courses between terms.",
  },
  {
    title: "Military athlete console",
    to: "/fitness",
    blurb:
      "A training model for military fitness tests. Sessions in, a predicted score out, and the cost of a goal by a date. The data is mine, so it asks you to sign in.",
  },
  {
    title: "Dates and countdowns",
    to: "/dates",
    blurb:
      "The days between two dates, a date plus or minus a span, and countdowns the alarms app on my phone shows too.",
  },
  {
    title: "Facewoof",
    to: "https://facewoof.abera.tech",
    blurb:
      "A social app for dog owners: matches by distance, packs with a shared feed, and playdates on a calendar. Live, with a demo account.",
    external: true,
  },
];

/**
 * The one action in the bar, rather than another place to browse.
 *
 * Booking is reachable from every page, so it is a structural route in
 * routes.ts and not a project: listing it beside the things I built made a
 * calendar look like a piece of work.
 */
export const primaryAction: Entry = {
  title: "Schedule time with me",
  to: "/schedule",
  blurb: "Book a time, or join the queue. Confirmed by text.",
};

/**
 * The owner's pages, in the app bar on every page once signed in, desktop
 * and phone alike. A page the owner works from goes here, so it is one
 * click from anywhere. /dates shipped on 2026-10-01 linked only from
 * /projects and the footer, and Neb had to ask how to reach it.
 */
export const ownerPages: { label: string; to: string }[] = [
  { label: "Links", to: "/links" },
  { label: "Plan", to: "/plan" },
  { label: "Dev box", to: "/devbox" },
  { label: "Alerts", to: "/alerts" },
  { label: "Dates", to: "/dates" },
  { label: "Network", to: "/network" },
];

/**
 * Every page that is not structural, not a guide and not in ownerPages,
 * and why the app bar leaves it out. routes.test.ts fails on a page that
 * is in none of the four, so a new page gets a decision about the bar.
 */
export const notInBar: Record<string, string> = {
  "/planner":
    "A project for visitors, reached from Projects in the bar. The owner does not keep state there.",
  "/fitness":
    "A project for visitors, reached from Projects in the bar. Its data is the owner's, read on the page itself.",
  "/schedule/admin":
    "The queue's admin, reached from the booking flow and by address. Neb has not asked for it in the bar.",
};

/** What to show where space is tight. */
export const label = (entry: Entry): string => entry.navLabel ?? entry.title;
