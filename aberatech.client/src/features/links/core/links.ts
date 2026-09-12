/**
 * The owner's bookmarks: the document the /links page keeps on the server.
 *
 * Pure. Every change is a new document, so the page hands the result to the
 * document hook and the hook saves it; nothing here touches the network or
 * the DOM. The shape is versioned because it lives in a database row that
 * outlives any one build of the page.
 */

export interface LinkEntry {
  id: string;
  title: string;
  url: string;
  /** A heading the link sits under. Empty means the general list. */
  group: string;
  note: string;
  /** ISO date the link was added. */
  addedAt: string;
}

export interface LinksDocument {
  version: 1;
  links: LinkEntry[];
}

export const GENERAL = "General";

export const empty: LinksDocument = { version: 1, links: [] };

export interface NewLink {
  title: string;
  url: string;
  group?: string;
  note?: string;
}

/**
 * A usable http(s) URL, or null. A bare host gets https in front of it,
 * because that is what a pasted "abera.tech" means; anything else that
 * fails to parse, or that is not a web URL, is refused rather than saved.
 */
export function normalizeUrl(input: string): string | null {
  const trimmed = input.trim();
  if (trimmed === "") return null;
  const candidate = /^[a-z][a-z0-9+.-]*:/i.test(trimmed)
    ? trimmed
    : `https://${trimmed}`;
  try {
    const url = new URL(candidate);
    if (url.protocol !== "https:" && url.protocol !== "http:") return null;
    if (url.hostname === "") return null;
    return url.toString();
  } catch {
    return null;
  }
}

/** The host a link points at, for the line under its title. */
export function hostOf(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, "");
  } catch {
    return url;
  }
}

/** A title to show: the one given, or the host when none was. */
export function titleOf(link: Pick<LinkEntry, "title" | "url">): string {
  return link.title.trim() === "" ? hostOf(link.url) : link.title;
}

/**
 * The document with one more link, or the same document when the URL is
 * not one. The id and the clock are parameters so a test can name them.
 */
export function addLink(
  document: LinksDocument,
  link: NewLink,
  now: Date = new Date(),
  id: string = newId(),
): LinksDocument {
  const url = normalizeUrl(link.url);
  if (url === null) return document;
  const entry: LinkEntry = {
    id,
    title: link.title.trim(),
    url,
    group: (link.group ?? "").trim(),
    note: (link.note ?? "").trim(),
    addedAt: now.toISOString().slice(0, 10),
  };
  return { version: 1, links: [...document.links, entry] };
}

export function removeLink(document: LinksDocument, id: string): LinksDocument {
  return { version: 1, links: document.links.filter((l) => l.id !== id) };
}

/**
 * The groups in use, the general list first and the rest alphabetically,
 * so the page's headings are stable as links come and go.
 */
export function groupsOf(document: LinksDocument): string[] {
  const named = new Set<string>();
  let general = false;
  for (const link of document.links) {
    if (link.group === "") general = true;
    else named.add(link.group);
  }
  const rest = [...named].sort((a, b) => a.localeCompare(b));
  return general ? [GENERAL, ...rest] : rest;
}

/** The links under one heading, newest first. */
export function inGroup(document: LinksDocument, group: string): LinkEntry[] {
  const key = group === GENERAL ? "" : group;
  return document.links
    .filter((l) => l.group === key)
    .slice()
    .reverse();
}

/** The links whose title, host, group or note contains the query. */
export function search(document: LinksDocument, query: string): LinksDocument {
  const q = query.trim().toLowerCase();
  if (q === "") return document;
  return {
    version: 1,
    links: document.links.filter((l) =>
      [l.title, hostOf(l.url), l.url, l.group, l.note].some((field) =>
        field.toLowerCase().includes(q),
      ),
    ),
  };
}

/**
 * Whatever the server holds, made safe to render: a row written by an
 * older or a newer build still opens, with unknown fields dropped and
 * missing ones defaulted, rather than a page that cannot load.
 */
export function coerce(value: unknown): LinksDocument {
  if (typeof value !== "object" || value === null) return empty;
  const raw = (value as { links?: unknown }).links;
  if (!Array.isArray(raw)) return empty;
  const links: LinkEntry[] = [];
  for (const item of raw) {
    if (typeof item !== "object" || item === null) continue;
    const r = item as Record<string, unknown>;
    const url = typeof r.url === "string" ? normalizeUrl(r.url) : null;
    if (url === null) continue;
    links.push({
      id: typeof r.id === "string" && r.id !== "" ? r.id : newId(),
      title: typeof r.title === "string" ? r.title : "",
      url,
      group: typeof r.group === "string" ? r.group : "",
      note: typeof r.note === "string" ? r.note : "",
      addedAt: typeof r.addedAt === "string" ? r.addedAt : "",
    });
  }
  return { version: 1, links };
}

function newId(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
