import { guides } from "./sections";

/**
 * The routes baked to HTML at build time.
 *
 * Derived from sections.ts so a new guide is prerendered by being listed, not
 * by someone remembering a second list. The structural pages are named here;
 * the app pages stay out on purpose — /schedule shows live queue state,
 * /planner is an interactive tool and /fitness reads its training data from
 * the API, so a build-time snapshot of any of them would open stale.
 */
export const prerenderedRoutes: string[] = [
  "/",
  "/guides",
  "/projects",
  ...guides.filter((entry) => !entry.external).map((entry) => entry.to),
];
