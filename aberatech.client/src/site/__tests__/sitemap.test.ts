/**
 * The sitemap lists what the navigation reaches and nothing the owner keeps
 * behind a sign-in.
 */
import { describe, expect, it } from "vitest";
import { prerenderedRoutes } from "../prerenderedRoutes";
import { routes, unlisted } from "../routes";
import { sitemapRoutes, sitemapXml } from "../sitemap";

describe("sitemap", () => {
  it("lists every navigable route and none of the unlisted ones", () => {
    const listed = sitemapRoutes();
    const navigable = routes
      .map((route) => route.path)
      .filter((path) => !(path in unlisted));
    expect(listed).toEqual(navigable);
    for (const path of Object.keys(unlisted)) {
      expect(listed).not.toContain(path);
    }
  });

  it("includes every prerendered page", () => {
    const listed = sitemapRoutes();
    for (const path of prerenderedRoutes) {
      expect(listed).toContain(path);
    }
  });

  it("writes one absolute URL per page, the home page with its slash", () => {
    const xml = sitemapXml(["/", "/guides", "/schedule/admin"]);
    expect(xml.startsWith('<?xml version="1.0" encoding="UTF-8"?>')).toBe(true);
    expect(xml).toContain("<loc>https://abera.tech/</loc>");
    expect(xml).toContain("<loc>https://abera.tech/guides</loc>");
    expect(xml).toContain("<loc>https://abera.tech/schedule/admin</loc>");
    expect(xml.match(/<url>/g)).toHaveLength(3);
    expect(xml.endsWith("</urlset>\n")).toBe(true);
  });
});
