/**
 * Which routes get baked to HTML at build time.
 *
 * The list is derived from sections.ts, the site's single source of truth, so
 * adding a guide there prerenders it without anyone remembering a second list.
 * The app pages are baked as their loading frame. /planner is baked as the
 * whole board a first visitor sees.
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
    expect(prerenderedRoutes).toContain("/rf-training");
    expect(prerenderedRoutes).toContain("/signal-processing");
    expect(prerenderedRoutes).toContain("/quantum-cryptography");
  });

  it("bakes the planner's board", () => {
    // The board is drawn from the catalog in the bundle. Until 2026-09-28
    // it was left out and its HTML was an empty root, blank until 376 KB
    // of script had run.
    expect(prerenderedRoutes).toContain("/planner");
  });

  it("bakes the frame of /links, whose first render holds no data", () => {
    // The list loads in an effect and the first render is always the
    // spinner, so the baked page is the title, the intro and the spinner:
    // nothing that can go stale. Before this the page was blank until the
    // bundle ran, then "Loading...", then the frame, then the list.
    expect(prerenderedRoutes).toContain("/links");
  });

  it("bakes the schedule, the queue and the fitness console the same way", () => {
    // Each starts loading and renders a spinner alone, and loads in an
    // effect. The schedule's live state arrives after load, as the owner
    // pages' data does.
    expect(prerenderedRoutes).toContain("/schedule");
    expect(prerenderedRoutes).toContain("/schedule/admin");
    expect(prerenderedRoutes).toContain("/fitness");
  });

  it("bakes the other owner pages the same way", () => {
    // /plan, /devbox and /alerts start in a loading state that renders the
    // spinner alone, and load in an effect, as /links does.
    expect(prerenderedRoutes).toContain("/plan");
    expect(prerenderedRoutes).toContain("/devbox");
    expect(prerenderedRoutes).toContain("/alerts");
  });

  it("never names an external URL", () => {
    for (const route of prerenderedRoutes) {
      expect(route.startsWith("/")).toBe(true);
    }
  });
});
