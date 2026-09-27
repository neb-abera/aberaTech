import { guides } from "./sections";

/**
 * The routes baked to HTML at build time.
 *
 * Derived from sections.ts so a new guide is prerendered by being listed, not
 * by someone remembering a second list. The structural pages are named here.
 *
 * The app pages are baked too, all but /planner. Each loads in an effect and
 * its first render is always the spinner, so the baked page is the title,
 * the intro and the spinner, which cannot go stale. Unbaked, each was blank
 * until the bundle ran, then "Loading...", then the frame. /planner draws
 * its whole board before its data, sized to the screen, so it stays out.
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
  ...guides.filter((entry) => !entry.external).map((entry) => entry.to),
];
