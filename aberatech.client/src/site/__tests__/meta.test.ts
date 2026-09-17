/**
 * Every page names itself. One title across the whole site is how the
 * cryptography guide came to be shared as "Built by Neb using .NET".
 */
import { describe, expect, it } from "vitest";
import {
  headFor,
  heroAvatar,
  meta,
  notFoundMeta,
  siteName,
  titleFor,
} from "../meta";
import { routes } from "../routes";

describe("page metadata", () => {
  it("covers every route the app serves", () => {
    const missing = routes
      .map((route) => route.path)
      .filter((path) => !(path in meta));
    expect(missing).toEqual([]);
  });

  it("names nothing the app does not serve", () => {
    const served = new Set(routes.map((route) => route.path));
    const stray = Object.keys(meta).filter((path) => !served.has(path));
    expect(stray).toEqual([]);
  });

  it("gives every page a description a search result can show", () => {
    for (const [path, page] of Object.entries(meta)) {
      expect(page.title.length, `${path} title`).toBeGreaterThan(2);
      expect(page.description.length, `${path} description`).toBeGreaterThan(
        20,
      );
      expect(page.description.length, `${path} description`).toBeLessThan(320);
    }
  });

  it("titles the home page with the name alone and every other page with both", () => {
    expect(titleFor("/")).toBe(siteName);
    expect(titleFor("/guides")).toBe(`Guides · ${siteName}`);
    expect(titleFor("/no-such-page")).toBe(
      `${notFoundMeta.title} · ${siteName}`,
    );
  });

  it("never mentions the framework in a title", () => {
    for (const path of Object.keys(meta)) {
      expect(titleFor(path)).not.toMatch(/\.NET|React/);
    }
  });
});

describe("the prerendered head", () => {
  it("carries the title, description, canonical URL and a preview card", () => {
    const head = headFor("/quantum-cryptography");

    expect(head).toContain(
      `<title>Learning Quantum and Post-Quantum Cryptography · ${siteName}</title>`,
    );
    expect(head).toContain('<meta name="description" content="');
    expect(head).toContain(
      '<link rel="canonical" href="https://abera.tech/quantum-cryptography" />',
    );
    expect(head).toContain(
      'property="og:image" content="https://abera.tech/og.png"',
    );
    expect(head).toContain('name="twitter:card" content="summary_large_image"');
  });

  it("describes the author as structured data on the home page only", () => {
    expect(headFor("/")).toContain('"@type":"Person"');
    expect(headFor("/")).toContain(
      '<link rel="canonical" href="https://abera.tech/" />',
    );
    expect(headFor("/guides")).not.toContain("application/ld+json");
  });

  it("preloads the home page's avatar, and only there", () => {
    const home = headFor("/");

    expect(home).toContain(
      `<link rel="preload" as="image" href="${heroAvatar.src}" type="image/webp" fetchpriority="high" />`,
    );
    expect(heroAvatar.src).toMatch(/headshot-336.*\.webp$/);
    expect(headFor("/guides")).not.toContain('rel="preload"');
  });

  it("still names the full-size headshot, at its stable address, in the structured data", () => {
    // Search engines were given /headshot.jpg; the smaller avatar the page
    // draws is a hashed file whose name changes with its bytes.
    expect(headFor("/")).toContain('"image":"https://abera.tech/headshot.jpg"');
  });

  it("escapes what it puts in an attribute", () => {
    // The planner's blurb carries a typographic apostrophe and the guides
    // carry colons; neither breaks an attribute. A quote or an angle bracket
    // would, so they are escaped rather than trusted.
    for (const path of Object.keys(meta)) {
      const head = headFor(path);
      for (const match of head.matchAll(/content="([^"]*)"/g)) {
        expect(match[1]).not.toMatch(/[<>]/);
      }
    }
  });
});
