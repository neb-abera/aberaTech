import { guides } from "./sections";

/**
 * The routes baked to HTML at build time.
 *
 * Derived from sections.ts so a new guide is prerendered by being listed, not
 * by someone remembering a second list. The structural pages are named here;
 * the app pages stay out on purpose — /schedule shows live queue state,
 * /planner is an interactive tool and /fitness reads its training data from
 * the API, so a build-time snapshot of any of them would open stale.
 *
 * /links is the exception among the owner's pages. Its list loads in an
 * effect and its first render is always the spinner, so the baked page is
 * the title, the intro and the spinner, which cannot go stale. Unbaked, it
 * was blank until the bundle ran, then "Loading...", then the frame.
 */
export const prerenderedRoutes: string[] = [
  "/",
  "/guides",
  "/projects",
  "/links",
  ...guides.filter((entry) => !entry.external).map((entry) => entry.to),
];
