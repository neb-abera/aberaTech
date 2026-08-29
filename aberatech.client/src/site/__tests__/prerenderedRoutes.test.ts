/**
 * Which routes get baked to HTML at build time.
 *
 * The list is derived from sections.ts, the site's single source of truth, so
 * adding a guide there prerenders it without anyone remembering a second list.
 * The app pages stay out: /schedule shows live queue state and /planner is an
 * interactive tool, so a build-time snapshot of either would be a lie.
 */
import { describe, expect, it } from "vitest";
import { prerenderedRoutes } from "../prerenderedRoutes";

describe("prerenderedRoutes", () => {
  it("includes the home page and both index pages", () => {
    expect(prerenderedRoutes).toContain("/");
    expect(prerenderedRoutes).toContain("/guides");
    expect(prerenderedRoutes).toContain("/projects");
  });

  it("includes every internal guide from sections.ts", () => {
    expect(prerenderedRoutes).toContain("/transition");
    expect(prerenderedRoutes).toContain("/technical");
  });

  it("leaves the live app pages client-rendered", () => {
    expect(prerenderedRoutes).not.toContain("/schedule");
    expect(prerenderedRoutes).not.toContain("/schedule/admin");
    expect(prerenderedRoutes).not.toContain("/planner");
  });

  it("never names an external URL", () => {
    for (const route of prerenderedRoutes) {
      expect(route.startsWith("/")).toBe(true);
    }
  });
});
