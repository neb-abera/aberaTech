/**
 * What the site contains, in one place.
 *
 * The nav, the two index pages and the footer all read from here. Before this
 * existed each of them held its own copy of the list, so adding a project meant
 * editing three files and the nav grew by one item every time. Now a new entry
 * is one object, and the nav does not grow at all.
 */

export interface Entry {
  title: string;
  /** An internal route, or an absolute URL for anything on another origin. */
  to: string;
  blurb: string;
  /** Set when `to` leaves this site, so links can open in a new tab and say so. */
  external?: boolean;
}

/** Long-form reading. */
export const guides: Entry[] = [
  {
    title: 'Military Transition Guide',
    to: '/transition',
    blurb: 'What to do and when, from eighteen months before ETS to long after it.'
  },
  {
    title: 'Learning Software Development',
    to: '/technical',
    blurb: 'The path from no background to working software, and the parts worth skipping.'
  }
];

/**
 * Things that do something. The scheduler is here as well as in the bar: it is
 * a project, and it is also the one thing on the site a visitor might want to
 * act on, so it gets a button of its own rather than waiting to be browsed to.
 */
export const projects: Entry[] = [
  {
    title: 'Learning RF and Signal Processing',
    to: '/planner',
    blurb:
      'Plan a Johns Hopkins Engineering for Professionals ECE master’s. All 138 courses, with prerequisites and the degree rules checked as you go.'
  },
  {
    title: 'Schedule time with me',
    to: '/schedule',
    blurb: 'A booking queue that confirms by text rather than leaving you to wonder.'
  },
  {
    title: 'Facewoof',
    to: 'https://facewoof.abera.tech',
    blurb: 'Lives on its own subdomain.',
    external: true
  }
];

/** The one action in the bar, rather than another place to browse. */
export const primaryAction: Entry = projects[1];
