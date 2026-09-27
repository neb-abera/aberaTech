import { guides } from "./sections";

/**
 * The routes baked to HTML at build time.
 *
 * Derived from sections.ts so a new guide is prerendered by being listed, not
 * by someone remembering a second list. The structural pages are named here.
 *
 * The app pages are baked too. Each loads in an effect and its first render
 * is always the spinner, so the baked page is the title, the intro and the
 * spinner, which cannot go stale. Unbaked, each was blank until the bundle
 * ran, then "Loading...", then the frame. /planner is baked as the board a
 * first visitor sees: it is drawn from the catalog in the bundle, and its
 * layout and colours follow the screen and the scheme in CSS.
 */
export const prerenderedRoutes: string[] = [
  "/",
  "/guides",
  "/projects",
  "/links",
  "/plan",
  "/devbox",
  "/alerts",
  "/schedule",
  "/schedule/admin",
  "/fitness",
  "/planner",
  ...guides.filter((entry) => !entry.external).map((entry) => entry.to),
];
