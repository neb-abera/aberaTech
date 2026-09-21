import { siteOrigin } from "./meta";
import { routes, unlisted } from "./routes";

/**
 * The pages a search engine may list: every route the app serves that the
 * navigation reaches. The unlisted ones (the owner's queue, bookmarks and
 * plan) show a visitor a sign-in button, which is nothing to index, and
 * robots.txt disallows them for the same reason.
 */
export function sitemapRoutes(): string[] {
  return routes
    .map((route) => route.path)
    .filter((path) => !(path in unlisted));
}

/** The sitemap document, written to dist/sitemap.xml by tools/prerender.mjs. */
export function sitemapXml(paths: string[] = sitemapRoutes()): string {
  const urls = paths.map(
    (path) =>
      `  <url><loc>${siteOrigin}${path === "/" ? "/" : path}</loc></url>`,
  );
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
    ...urls,
    "</urlset>",
    "",
  ].join("\n");
}
