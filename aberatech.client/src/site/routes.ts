/// <reference types="vite/client" />
import { type ComponentType, type LazyExoticComponent, lazy } from "react";

/**
 * Every page this app serves, in one place.
 *
 * App.tsx builds its `<Routes>` from this list and nothing else, so a page
 * cannot exist without being named here — and routes.test.ts holds each name to
 * the rule that a page is either reachable from the site's own navigation or
 * listed in `unlisted` with the reason it is not.
 *
 * The rule is written down because /fitness was live, public and linked from
 * nowhere: the route existed, sections.ts did not know about it, and the only
 * way to reach the page was to already know its address.
 */

/** Every view takes the same props, so the theme can be disabled under test. */
type PageProps = { disableCustomTheme?: boolean };

export interface PageRoute {
  /** The path, spelled exactly as sections.ts spells it in a link. */
  path: string;
  /**
   * The file in src/views that renders it, without the extension.
   * tools/prerender.mjs finds the view's chunks in the build manifest by
   * this name and names them in the page's head.
   */
  view: string;
  Page: LazyExoticComponent<ComponentType<PageProps>>;
}

type View = { default: ComponentType<PageProps> };
const views = import.meta.glob<View>("../views/*.tsx");

function page(path: string, view: string): PageRoute {
  const load = views[`../views/${view}.tsx`];
  if (!load) throw new Error(`${path}: there is no src/views/${view}.tsx`);
  return { path, view, Page: lazy(load) };
}

export const routes: PageRoute[] = [
  page("/", "Home"),
  page("/guides", "Guides"),
  page("/projects", "Projects"),
  page("/transition", "MilitaryTransitionGuide"),
  page("/technical", "TechnicalTransitionGuide"),
  page("/rf-training", "RfTraining"),
  page("/signal-processing", "SignalProcessing"),
  page("/quantum-cryptography", "QuantumCryptography"),
  page("/planner", "CoursePlanner"),
  page("/schedule", "ScheduleTime"),
  page("/fitness", "Fitness"),
  page("/schedule/admin", "ScheduleAdmin"),
  page("/links", "Links"),
  page("/plan", "Plan"),
  page("/devbox", "DevBox"),
  page("/alerts", "Alerts"),
  page("/dates", "Dates"),
];

/**
 * The pages that are the navigation, rather than entries within it. They are
 * reachable from the app bar on every page, so sections.ts does not list them.
 * /schedule is the bar's one button, on every page, which is why it is here
 * and not among the projects.
 */
export const structural: string[] = ["/", "/guides", "/projects", "/schedule"];

/**
 * Routes deliberately absent from the navigation, and why.
 *
 * A reason here is a decision, not a hiding place: everything in this map is
 * served publicly to anyone who types the address. Nothing that actually needs
 * protecting belongs here — it belongs behind the account check.
 */
export const unlisted: Record<string, string> = {
  "/schedule/admin":
    "Useful only when signed in as the queue owner. Everyone else would open a panel they cannot act on, so it is reached by address and gated by the API.",
  "/links":
    "The owner's bookmarks, kept on the server so they follow the owner between devices. A visitor gets a sign-in button and nothing else, so it is reached by address and gated by the API; the app bar adds a Links entry only once the owner is signed in.",
  "/plan":
    "The owner's plan, a Markdown document kept on the server. Same shape as /links: a visitor gets a sign-in button, the app bar shows a Plan entry only to the signed-in owner.",
  "/devbox":
    "The owner's dev box: its power state, a Start button, the terminal and desktop in a browser tab (devbox.abera.tech, devbox-desktop.abera.tech, behind Cloudflare Access) and the runbook for getting a session back from a phone or a locked-down work computer. A visitor gets a sign-in button; the app bar shows a Dev box entry only to the signed-in owner.",
  "/alerts":
    "The owner's calendar alerts: mute, skip and a test send. Same shape as /devbox: a visitor gets a sign-in button, the app bar shows an Alerts entry only to the signed-in owner.",
};
