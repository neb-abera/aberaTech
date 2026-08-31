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
  Page: LazyExoticComponent<ComponentType<PageProps>>;
}

export const routes: PageRoute[] = [
  { path: "/", Page: lazy(() => import("../views/Home")) },
  { path: "/marketing", Page: lazy(() => import("../views/MarketingPage")) },
  { path: "/guides", Page: lazy(() => import("../views/Guides")) },
  { path: "/projects", Page: lazy(() => import("../views/Projects")) },
  {
    path: "/transition",
    Page: lazy(() => import("../views/MilitaryTransitionGuide")),
  },
  {
    path: "/technical",
    Page: lazy(() => import("../views/TechnicalTransitionGuide")),
  },
  { path: "/planner", Page: lazy(() => import("../views/CoursePlanner")) },
  { path: "/schedule", Page: lazy(() => import("../views/ScheduleTime")) },
  { path: "/fitness", Page: lazy(() => import("../views/Fitness")) },
  {
    path: "/schedule/admin",
    Page: lazy(() => import("../views/ScheduleAdmin")),
  },
];

/**
 * The pages that are the navigation, rather than entries within it. They are
 * reachable from the app bar on every page, so sections.ts does not list them.
 */
export const structural: string[] = ["/", "/guides", "/projects"];

/**
 * Routes deliberately absent from the navigation, and why.
 *
 * A reason here is a decision, not a hiding place: everything in this map is
 * served publicly to anyone who types the address. Nothing that actually needs
 * protecting belongs here — it belongs behind the account check.
 */
export const unlisted: Record<string, string> = {
  "/marketing":
    "The unedited Material UI template page — stock pricing tiers, invented testimonials, Sitemark's logos. Kept only as a reference for its section components, and not something to link anyone to.",
  "/schedule/admin":
    "Useful only when signed in as the queue owner. Everyone else would open a panel they cannot act on, so it is reached by address and gated by the API.",
};
