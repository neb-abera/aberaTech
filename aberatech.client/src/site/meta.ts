import { guides, projects } from "./sections";

/**
 * What each page says about itself to a browser tab, a search result and a
 * link preview.
 *
 * Every page used to ship under one title, "Built by Neb using .NET", with
 * no description and no preview card: a link to the cryptography guide pasted
 * into an email rendered as a blank card carrying a framework's name. The
 * prerender bakes these into each page's <head>, and App.tsx sets the title
 * on client-side navigation, both from this one list.
 */

export const siteName = "Neb Abera";
export const siteOrigin = "https://abera.tech";

/** The one line under the name in the footer, and on the preview card. */
export const tagline =
  "Senior Computer Scientist at MITRE, specializing in secure embedded systems.";

export interface PageMeta {
  /** The <title>. The site name is appended to every page but the home page. */
  title: string;
  /** One or two sentences for search results and link previews. */
  description: string;
}

const structuralMeta: Record<string, PageMeta> = {
  "/": {
    title: siteName,
    description: `${tagline} Guides for soldiers leaving the Army, study plans for field radio, signal processing and post-quantum cryptography, and the tools I built.`,
  },
  "/guides": {
    title: "Guides",
    description:
      "Guides I wrote and work from: leaving the Army, learning to program, field radio, signal processing, and post-quantum cryptography.",
  },
  "/projects": {
    title: "Projects",
    description:
      "Tools I built and run: a course planner for a graduate degree, a scheduler, a military fitness console, and a social app for dog owners.",
  },
  "/schedule/admin": {
    title: "Queue",
    description: "The scheduling queue, for its owner.",
  },
  "/links": {
    title: "Links",
    description: "The owner's bookmarks.",
  },
};

const entryMeta = Object.fromEntries(
  [...guides, ...projects]
    .filter((entry) => !entry.external)
    .map((entry) => [
      entry.to,
      { title: entry.title, description: entry.blurb },
    ]),
);

/** Every route the app serves, with its title and description. */
export const meta: Record<string, PageMeta> = {
  ...entryMeta,
  ...structuralMeta,
};

/** For a page the app does not have. */
export const notFoundMeta: PageMeta = {
  title: "No page at that address",
  description: "Nothing lives at that address.",
};

/** The tab title for a route, or for a route the app does not have. */
export function titleFor(route: string): string {
  const page = meta[route] ?? notFoundMeta;
  return page.title === siteName ? siteName : `${page.title} · ${siteName}`;
}

const escapeHtml = (value: string): string =>
  value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");

/**
 * The structured description of the person behind the site, for search
 * engines. Only the home page carries it: it is about the author, not the
 * page, and one copy is enough. It says what the footer already says.
 */
const person = {
  "@context": "https://schema.org",
  "@type": "Person",
  name: "Neb Abera",
  url: siteOrigin,
  image: `${siteOrigin}/headshot.jpg`,
  jobTitle: "Senior Computer Scientist",
  worksFor: { "@type": "Organization", name: "MITRE" },
  sameAs: [
    "https://www.linkedin.com/in/neb-abera/",
    "https://github.com/neb-abera",
  ],
};

/**
 * The <head> markup for one prerendered route: title, description, canonical
 * URL, and the Open Graph and Twitter tags that turn a pasted link into a
 * card. The JSON-LD script is data, not code: browsers never execute it, so
 * the content security policy's script hashes do not apply to it.
 */
export function headFor(route: string): string {
  const page = meta[route] ?? notFoundMeta;
  const title = escapeHtml(titleFor(route));
  const description = escapeHtml(page.description);
  const url = escapeHtml(siteOrigin + (route === "/" ? "/" : route));
  const image = `${siteOrigin}/og.png`;
  const lines = [
    `<title>${title}</title>`,
    `<meta name="description" content="${description}" />`,
    `<link rel="canonical" href="${url}" />`,
    `<meta property="og:type" content="${route === "/" ? "profile" : "article"}" />`,
    `<meta property="og:site_name" content="${siteName}" />`,
    `<meta property="og:title" content="${title}" />`,
    `<meta property="og:description" content="${description}" />`,
    `<meta property="og:url" content="${url}" />`,
    `<meta property="og:image" content="${image}" />`,
    `<meta property="og:image:width" content="1200" />`,
    `<meta property="og:image:height" content="630" />`,
    `<meta name="twitter:card" content="summary_large_image" />`,
    `<meta name="twitter:title" content="${title}" />`,
    `<meta name="twitter:description" content="${description}" />`,
    `<meta name="twitter:image" content="${image}" />`,
  ];
  if (route === "/") {
    lines.push(
      `<script type="application/ld+json">${JSON.stringify(person)}</script>`,
    );
  }
  return lines.join("\n    ");
}
