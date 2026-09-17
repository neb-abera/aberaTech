import heroAvatarUrl from "../assets/headshot-336.webp";
import { guides, primaryAction, projects } from "./sections";

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

/**
 * The one line under the name: the footer, the home page description, the
 * preview card and the Person record all say this and nothing more. Kept the
 * same on LinkedIn, so the two profiles agree.
 */
export const tagline =
  "Senior Computer Scientist at MITRE. Talk to me about secure embedded systems, cryptography, RF and signal processing, and systems engineering. I’m always open to book recommendations.";

/**
 * The picture the home page draws: the headshot at twice the avatar's largest
 * size. A hashed asset, unlike /headshot.jpg, which keeps its stable URL for
 * the Person record below. Named here because two things must agree on it:
 * the Hero that renders it and the preload that fetches it early.
 */
export const heroAvatar = {
  src: heroAvatarUrl,
  size: 336,
  type: "image/webp",
} as const;

export interface PageMeta {
  /** The <title>. The site name is appended to every page but the home page. */
  title: string;
  /** One or two sentences for search results and link previews. */
  description: string;
}

const structuralMeta: Record<string, PageMeta> = {
  "/": {
    title: siteName,
    description: tagline,
  },
  "/guides": {
    title: "Guides",
    description:
      "Guides I wrote and work from: leaving the Army, learning to program, field radio, signal processing, and post-quantum cryptography.",
  },
  "/projects": {
    title: "Projects",
    description:
      "Tools I built and run: a graduate course planner that solves prerequisites and degree rules, a military fitness console, and a social app for dog owners.",
  },
  [primaryAction.to]: {
    title: primaryAction.title,
    description: primaryAction.blurb,
  },
  "/schedule/admin": {
    title: "Queue",
    description: "The scheduling queue, for its owner.",
  },
  "/links": {
    title: "Links",
    description: "The owner's bookmarks.",
  },
  "/plan": {
    title: "Plan",
    description: "The owner's plan, kept on the server.",
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
    // The avatar is the largest thing in the home page's first screen. React
    // emits a preload of its own for a high-priority image, but at the top of
    // the body; this one is in the head, ahead of the scripts, and names the
    // type so a browser without WebP does not fetch what it cannot draw. The
    // file preloaded is the file Hero renders: both read heroAvatar.
    lines.push(
      `<link rel="preload" as="image" href="${escapeHtml(heroAvatar.src)}" type="${heroAvatar.type}" fetchpriority="high" />`,
    );
    lines.push(
      `<script type="application/ld+json">${JSON.stringify(person)}</script>`,
    );
  }
  return lines.join("\n    ");
}
