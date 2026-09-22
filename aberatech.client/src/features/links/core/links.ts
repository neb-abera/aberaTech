/**
 * The owner's bookmarks: the document the /links page keeps on the server.
 *
 * Pure. Every change is a new document, so the page hands the result to the
 * document hook and the hook saves it; nothing here touches the network or
 * the DOM. The shape is versioned because it lives in a database row that
 * outlives any one build of the page.
 *
 * A link has one folder (its group, a path spelled "Work / Tools") and any
 * number of tags. The folder is the hierarchy a browser export carries and
 * gets back; a tag cuts across folders, so "MITRE" can hold links from
 * several of them and be downloaded on its own.
 */

export interface LinkEntry {
  id: string;
  title: string;
  url: string;
  /** A heading the link sits under. Empty means the general list. */
  group: string;
  note: string;
  /** Labels across folders: "MITRE", "army". Matching ignores case. */
  tags: string[];
  /** ISO date the link was added. */
  addedAt: string;
}

/**
 * A file said something different about a link that is already here.
 * Nothing on the link changes until the owner chooses: keep what is here,
 * take the file's version, or edit. Only the fields that differed are kept.
 */
export interface Conflict {
  id: string;
  linkId: string;
  /** The file it came from. */
  source: string;
  /** ISO date it was noticed. */
  seenAt: string;
  theirs: { title?: string; note?: string; group?: string };
}

export interface LinksDocument {
  version: 1;
  links: LinkEntry[];
  conflicts: Conflict[];
}

export const GENERAL = "General";

export const empty: LinksDocument = { version: 1, links: [], conflicts: [] };

export interface NewLink {
  title: string;
  url: string;
  group?: string;
  note?: string;
  /** Comma separated as typed, or already a list. */
  tags?: string | string[];
  /** ISO date, when the source knows it; otherwise the day it is added. */
  addedAt?: string;
}

/**
 * A usable http(s) URL, or null. A bare host gets https in front of it,
 * because that is what a pasted "abera.tech" means, and so does a host with
 * a port, "localhost:5173"; anything with a scheme of its own that is not
 * http or https, and anything that fails to parse, is refused rather than
 * saved.
 */
export function normalizeUrl(input: string): string | null {
  const trimmed = input.trim();
  if (trimmed === "") return null;
  const scheme = /^([a-z][a-z0-9+.-]*):(.*)$/is.exec(trimmed);
  let candidate: string;
  if (scheme === null) {
    candidate = `https://${trimmed}`;
  } else if (scheme[2].startsWith("//")) {
    // A real scheme, to be checked below.
    candidate = trimmed;
  } else if (/^\d+(?:[/?#]|$)/.test(scheme[2])) {
    // "host:port", which the scheme pattern also matches.
    candidate = `https://${trimmed}`;
  } else {
    // "mailto:", "javascript:" and the like: parsed as they are, and
    // refused below for not being web addresses.
    candidate = trimmed;
  }
  try {
    const url = new URL(candidate);
    if (url.protocol !== "https:" && url.protocol !== "http:") return null;
    if (url.hostname === "") return null;
    return url.toString();
  } catch {
    return null;
  }
}

/**
 * What makes two addresses the same bookmark: the host without "www.", the
 * path without a trailing slash, the query, and the fragment. The scheme is
 * left out so an old http bookmark and its https successor are one entry.
 */
export function urlKey(url: string): string {
  try {
    const parsed = new URL(url);
    const host = parsed.hostname.replace(/^www\./, "");
    const path = parsed.pathname.replace(/\/+$/, "");
    return `${host}${path}${parsed.search}${parsed.hash}`;
  } catch {
    return url;
  }
}

/**
 * A heading as typed, or the general list. The general heading is a name
 * the page shows for links with no group, so a group typed as "General"
 * is that list, not a second heading with the same name.
 */
export function normalizeGroup(group: string | undefined): string {
  const trimmed = (group ?? "").trim();
  return trimmed.toLowerCase() === GENERAL.toLowerCase() ? "" : trimmed;
}

/**
 * Tags as typed ("MITRE, army,personal") or as a list, trimmed, without
 * empties, and each spelling once: "Army" after "army" is the same tag and
 * the first spelling stays.
 */
export function normalizeTags(tags: string | string[] | undefined): string[] {
  const raw =
    tags === undefined ? [] : Array.isArray(tags) ? tags : tags.split(",");
  const seen = new Set<string>();
  const out: string[] = [];
  for (const tag of raw) {
    const trimmed = tag.trim();
    const key = trimmed.toLowerCase();
    if (trimmed === "" || seen.has(key)) continue;
    seen.add(key);
    out.push(trimmed);
  }
  return out;
}

export function hasTag(link: Pick<LinkEntry, "tags">, tag: string): boolean {
  const key = tag.trim().toLowerCase();
  return link.tags.some((t) => t.toLowerCase() === key);
}

/** Every tag in use, each spelling once, alphabetically. */
export function tagsOf(document: LinksDocument): string[] {
  const byKey = new Map<string, string>();
  for (const link of document.links) {
    for (const tag of link.tags) {
      const key = tag.toLowerCase();
      if (!byKey.has(key)) byKey.set(key, tag);
    }
  }
  return [...byKey.values()].sort((a, b) => a.localeCompare(b));
}

/** The links carrying one tag; the whole list for no tag. */
export function withTag(
  document: LinksDocument,
  tag: string | null,
): LinksDocument {
  if (tag === null || tag.trim() === "") return document;
  return { ...document, links: document.links.filter((l) => hasTag(l, tag)) };
}

/** A tag as a file name part: "MITRE / Crypto" is "mitre-crypto". */
export function slugOf(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
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
    group: normalizeGroup(link.group),
    note: (link.note ?? "").trim(),
    tags: normalizeTags(link.tags),
    addedAt: link.addedAt ?? now.toISOString().slice(0, 10),
  };
  return { ...document, links: [...document.links, entry] };
}

/**
 * The document with one link's fields replaced, or the same document when
 * the new address is not one. The id, the place in the list and the day
 * added are kept: an edit is the same bookmark, corrected.
 */
export function updateLink(
  document: LinksDocument,
  id: string,
  fields: NewLink,
): LinksDocument {
  const url = normalizeUrl(fields.url);
  if (url === null) return document;
  if (!document.links.some((l) => l.id === id)) return document;
  return {
    ...document,
    links: document.links.map((l) =>
      l.id === id
        ? {
            ...l,
            title: fields.title.trim(),
            url,
            group: normalizeGroup(fields.group),
            note: (fields.note ?? "").trim(),
            tags:
              fields.tags === undefined ? l.tags : normalizeTags(fields.tags),
          }
        : l,
    ),
  };
}

/** The folder path a group name spells: "Work / Tools" is Work, then Tools. */
export function folderPath(group: string): string[] {
  return group
    .split("/")
    .map((s) => s.trim())
    .filter((s) => s !== "");
}

/** The document without one link, and without whatever it had to resolve. */
export function removeLink(document: LinksDocument, id: string): LinksDocument {
  return {
    ...document,
    links: document.links.filter((l) => l.id !== id),
    conflicts: document.conflicts.filter((c) => c.linkId !== id),
  };
}

/**
 * A conflict recorded, unless the same one is already waiting: the same
 * file uploaded twice asks once. Nothing on the link changes.
 */
export function addConflict(
  document: LinksDocument,
  conflict: Omit<Conflict, "id">,
  id: string = newId(),
): LinksDocument {
  const same = document.conflicts.some(
    (c) =>
      c.linkId === conflict.linkId &&
      c.theirs.title === conflict.theirs.title &&
      c.theirs.note === conflict.theirs.note &&
      c.theirs.group === conflict.theirs.group,
  );
  if (same) return document;
  return {
    ...document,
    conflicts: [...document.conflicts, { id, ...conflict }],
  };
}

export type Resolution =
  | { choice: "mine" }
  | { choice: "theirs" }
  | { choice: "edit"; fields: NewLink };

/**
 * One conflict settled. "mine" keeps the link as it is; "theirs" takes each
 * field the file offered; "edit" replaces the link with what the owner
 * typed. The conflict is gone either way, and so is any other conflict on
 * the same link that the choice made moot.
 */
export function resolveConflict(
  document: LinksDocument,
  conflictId: string,
  resolution: Resolution,
): LinksDocument {
  const conflict = document.conflicts.find((c) => c.id === conflictId);
  if (conflict === undefined) return document;
  let next = document;
  const link = document.links.find((l) => l.id === conflict.linkId);
  if (link !== undefined && resolution.choice === "theirs") {
    next = updateLink(document, link.id, {
      title: conflict.theirs.title ?? link.title,
      url: link.url,
      group: conflict.theirs.group ?? link.group,
      note: conflict.theirs.note ?? link.note,
    });
  } else if (link !== undefined && resolution.choice === "edit") {
    next = updateLink(document, link.id, resolution.fields);
  }
  return {
    ...next,
    conflicts: next.conflicts.filter((c) => c.id !== conflictId),
  };
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

/** The links whose title, host, group, tags or note contain the query. */
export function search(document: LinksDocument, query: string): LinksDocument {
  const q = query.trim().toLowerCase();
  if (q === "") return document;
  return {
    ...document,
    links: document.links.filter((l) =>
      [l.title, hostOf(l.url), l.url, l.group, l.note, ...l.tags].some(
        (field) => field.toLowerCase().includes(q),
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
  const raw = (value as { links?: unknown; conflicts?: unknown }).links;
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
      group: normalizeGroup(typeof r.group === "string" ? r.group : ""),
      note: typeof r.note === "string" ? r.note : "",
      tags: Array.isArray(r.tags)
        ? normalizeTags(
            r.tags.filter((t): t is string => typeof t === "string"),
          )
        : [],
      addedAt: typeof r.addedAt === "string" ? r.addedAt : "",
    });
  }
  const ids = new Set(links.map((l) => l.id));
  const conflicts: Conflict[] = [];
  const rawConflicts = (value as { conflicts?: unknown }).conflicts;
  if (Array.isArray(rawConflicts)) {
    for (const item of rawConflicts) {
      if (typeof item !== "object" || item === null) continue;
      const r = item as Record<string, unknown>;
      if (typeof r.linkId !== "string" || !ids.has(r.linkId)) continue;
      const theirs =
        typeof r.theirs === "object" && r.theirs !== null
          ? (r.theirs as Record<string, unknown>)
          : {};
      const fields: Conflict["theirs"] = {};
      if (typeof theirs.title === "string") fields.title = theirs.title;
      if (typeof theirs.note === "string") fields.note = theirs.note;
      if (typeof theirs.group === "string") fields.group = theirs.group;
      if (Object.keys(fields).length === 0) continue;
      conflicts.push({
        id: typeof r.id === "string" && r.id !== "" ? r.id : newId(),
        linkId: r.linkId,
        source: typeof r.source === "string" ? r.source : "",
        seenAt: typeof r.seenAt === "string" ? r.seenAt : "",
        theirs: fields,
      });
    }
  }
  return { version: 1, links, conflicts };
}

function newId(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
