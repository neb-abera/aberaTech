/**
 * That every page can be found without knowing its address.
 *
 * /fitness shipped as a route nothing linked to: reachable, public, and
 * invisible unless you already knew it was there. These tests make that state
 * a failing build rather than something noticed months later.
 */
import { describe, expect, it } from "vitest";
import { routes, structural, unlisted } from "../routes";
import { type Entry, guides, projects } from "../sections";

const internal = (entries: Entry[]) =>
  entries.filter((entry) => !entry.external).map((entry) => entry.to);

const linked = new Set([
  ...structural,
  ...internal(guides),
  ...internal(projects),
]);

const paths = routes.map((route) => route.path);

describe("every page the app serves", () => {
  it("is linked from the navigation, or says in one place why it is not", () => {
    const orphans = paths.filter(
      (path) => !linked.has(path) && !(path in unlisted),
    );

    // Named rather than counted: the failure should say which page, and the
    // fix is either an entry in sections.ts or a reason in routes.ts.
    expect(orphans).toEqual([]);
  });

  it("appears exactly once", () => {
    expect(new Set(paths).size).toBe(paths.length);
  });
});

describe("the navigation", () => {
  it("never links a page that does not exist", () => {
    const served = new Set(paths);
    const broken = [...internal(guides), ...internal(projects)].filter(
      (to) => !served.has(to),
    );

    expect(broken).toEqual([]);
  });

  it("carries the fitness console, the page that was missing from it", () => {
    expect(internal(projects)).toContain("/fitness");
  });
});

describe("the unlisted routes", () => {
  it("name pages that are actually served", () => {
    const served = new Set(paths);

    for (const path of Object.keys(unlisted)) {
      expect(served.has(path)).toBe(true);
    }
  });

  it("give a reason rather than an empty excuse", () => {
    for (const [path, reason] of Object.entries(unlisted)) {
      expect(reason.length, `${path} has no reason`).toBeGreaterThan(20);
    }
  });

  it("do not also appear in the navigation", () => {
    for (const path of Object.keys(unlisted)) {
      expect(linked.has(path)).toBe(false);
    }
  });
});
