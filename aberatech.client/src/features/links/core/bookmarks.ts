/**
 * Bookmarks in and out of the list, as a file.
 *
 * The file format is the one every browser exports and imports, the
 * Netscape bookmark file: HTML, with an H3 per folder and an A per link.
 * Reading it needs no DOM — the file is not well-formed HTML and browsers
 * do not parse it as such either — so this is a small tokenizer over the
 * five tags that matter. The list's own export is the same format, so a
 * download can be taken into a browser and brought back here without loss:
 * a note travels as the DD line Firefox writes, and the day a link was
 * added as ADD_DATE.
 *
 * Bringing a file in is a merge, never a replace: an address already here
 * keeps its place and its id, takes a new title when the file has one, and
 * gains a note when it had none; an address not here is added under the
 * file's folder. Nothing is ever added twice.
 */

import {
  addConflict,
  addLink,
  type Conflict,
  empty,
  folderPath,
  hasTag,
  type LinkEntry,
  type LinksDocument,
  type NewLink,
  normalizeGroup,
  normalizeTags,
  normalizeUrl,
  urlKey,
} from "./links";

/**
 * Folders every browser wraps its bookmarks in. They say where a bookmark
 * sat in that browser, not what it is about, so they are not headings here.
 */
const BROWSER_ROOTS = new Set([
  "bookmarks bar",
  "bookmarks toolbar",
  "bookmarks menu",
  "other bookmarks",
  "mobile bookmarks",
  "favorites bar",
  "favourites bar",
  "favorites",
  "favourites",
  "reading list",
  "unfiled bookmarks",
  "bookmarks",
]);

const ENTITIES: Record<string, string> = {
  amp: "&",
  lt: "<",
  gt: ">",
  quot: '"',
  apos: "'",
  nbsp: " ",
};

export function decodeEntities(text: string): string {
  return text.replace(
    /&(#x[0-9a-f]+|#\d+|[a-z]+);/gi,
    (whole, entity: string) => {
      const lower = entity.toLowerCase();
      if (lower.startsWith("#x")) {
        return String.fromCodePoint(Number.parseInt(lower.slice(2), 16));
      }
      if (lower.startsWith("#")) {
        return String.fromCodePoint(Number.parseInt(lower.slice(1), 10));
      }
      return ENTITIES[lower] ?? whole;
    },
  );
}

function encode(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/**
 * The text of a fragment: every tag removed, then entities decoded. The
 * removal runs until nothing changes, so a tag nested inside another's
 * brackets cannot survive one pass; what comes out is shown as text by
 * React, never parsed as HTML.
 */
export function textOf(fragment: string): string {
  let text = fragment;
  let previous: string;
  do {
    previous = text;
    text = text.replace(/<[^>]*>/g, "");
  } while (text !== previous);
  return decodeEntities(text).trim();
}

function attribute(attributes: string, name: string): string | null {
  const match = new RegExp(`\\b${name}\\s*=\\s*"([^"]*)"`, "i").exec(
    attributes,
  );
  return match === null ? null : decodeEntities(match[1]);
}

function isoDate(unixSeconds: string | null): string | undefined {
  if (unixSeconds === null || !/^\d+$/.test(unixSeconds)) return undefined;
  const seconds = Number(unixSeconds);
  if (seconds <= 0) return undefined;
  // Some browsers write microseconds. A date past the year 3000 is that.
  const millis = seconds > 1e11 ? seconds / 1000 : seconds * 1000;
  const date = new Date(millis);
  return Number.isNaN(date.getTime())
    ? undefined
    : date.toISOString().slice(0, 10);
}

/** What one link looks like on the way in: a NewLink whose URL is not yet checked. */
export type ImportedLink = NewLink;

const TOKEN =
  /<H3\b[^>]*>([\s\S]*?)<\/H3>|<(DL)\b|<\/(DL)\b|<A\b([^>]*)>([\s\S]*?)<\/A>|<DD\b[^>]*>([^<]*)/gi;

/**
 * The links in a Netscape bookmark file, with each link's folder path as
 * its group. Browser root folders are left out of the path; nested folders
 * join with " / ".
 */
export function parseNetscape(html: string): ImportedLink[] {
  const links: ImportedLink[] = [];
  const stack: string[] = [];
  let pendingFolder: string | null = null;

  for (const match of html.matchAll(TOKEN)) {
    const [, h3, dlOpen, dlClose, aAttributes, aText, dd] = match;
    if (h3 !== undefined) {
      pendingFolder = textOf(h3);
    } else if (dlOpen !== undefined) {
      stack.push(pendingFolder ?? "");
      pendingFolder = null;
    } else if (dlClose !== undefined) {
      stack.pop();
    } else if (aAttributes !== undefined) {
      const href = attribute(aAttributes, "HREF");
      if (href === null) continue;
      const group = stack
        .filter((name) => name !== "" && !BROWSER_ROOTS.has(name.toLowerCase()))
        .join(" / ");
      links.push({
        url: href,
        title: textOf(aText ?? ""),
        group,
        note: "",
        tags: normalizeTags(attribute(aAttributes, "TAGS") ?? ""),
        addedAt: isoDate(attribute(aAttributes, "ADD_DATE")),
      });
    } else if (dd !== undefined) {
      const last = links[links.length - 1];
      if (last !== undefined && last.note === "") {
        last.note = decodeEntities(dd).trim();
      }
    }
  }
  return links;
}

/** The links in this list's own JSON, or in a plain array of {url, title, group, note}. */
export function parseJson(text: string): ImportedLink[] {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return [];
  }
  const raw = Array.isArray(parsed)
    ? parsed
    : typeof parsed === "object" && parsed !== null
      ? (parsed as { links?: unknown }).links
      : undefined;
  if (!Array.isArray(raw)) return [];
  const links: ImportedLink[] = [];
  for (const item of raw) {
    if (typeof item !== "object" || item === null) continue;
    const r = item as Record<string, unknown>;
    if (typeof r.url !== "string") continue;
    links.push({
      url: r.url,
      title: typeof r.title === "string" ? r.title : "",
      group: typeof r.group === "string" ? r.group : "",
      note: typeof r.note === "string" ? r.note : "",
      tags: Array.isArray(r.tags)
        ? r.tags.filter((t): t is string => typeof t === "string")
        : [],
      addedAt: typeof r.addedAt === "string" ? r.addedAt : undefined,
    });
  }
  return links;
}

/** Whatever the file turns out to be: a bookmark export, or this list's JSON. */
export function parseBookmarks(text: string): ImportedLink[] {
  const trimmed = text.trim();
  if (trimmed.startsWith("{") || trimmed.startsWith("[")) {
    return parseJson(trimmed);
  }
  return parseNetscape(trimmed);
}

export interface MergeReport {
  document: LinksDocument;
  /** Addresses that were not here and are now. */
  added: number;
  /** Addresses that were here and took something without a question: an empty title or note filled, http moved to https, a tag joined. */
  updated: number;
  /** Addresses that were here and needed nothing. */
  unchanged: number;
  /** Lines that were not web addresses. */
  refused: number;
  /** Differences recorded for the owner to settle, not counting ones already waiting. */
  conflicts: number;
}

/**
 * The list with a file's links folded in. Matching is by address (see
 * urlKey). A match keeps its id and its place in the list. What the file
 * says is taken without a question only where it cannot lose anything: an
 * empty title or note is filled, http moves to https when the file has the
 * https address, and the file's tags join the link's. A different title, a
 * different note, or a different folder is a conflict: recorded against the
 * link with the file's version, shown on the page, and applied only when
 * the owner chooses it. A new address is added under the file's folder. The
 * same address twice in one file is one link, and the second copy is a
 * conflict if it says something different.
 */
export function mergeLinks(
  document: LinksDocument,
  incoming: ImportedLink[],
  now: Date = new Date(),
  source = "upload",
): MergeReport {
  const byKey = new Map<string, number>();
  document.links.forEach((link, index) => {
    byKey.set(urlKey(link.url), index);
  });
  const links: LinkEntry[] = document.links.slice();
  let next: LinksDocument = { ...document, links };
  let added = 0;
  let updated = 0;
  let unchanged = 0;
  let refused = 0;
  let conflicts = 0;
  const seenAt = now.toISOString().slice(0, 10);

  for (const candidate of incoming) {
    const url = normalizeUrl(candidate.url);
    if (url === null) {
      refused += 1;
      continue;
    }
    const key = urlKey(url);
    const index = byKey.get(key);
    if (index === undefined) {
      const one = addLink(empty, { ...candidate, url }, now);
      links.push(one.links[0]);
      byKey.set(key, links.length - 1);
      added += 1;
      continue;
    }
    const existing = links[index];
    const title = candidate.title.trim();
    const note = (candidate.note ?? "").trim();
    const group = normalizeGroup(candidate.group);
    const tags = normalizeTags([
      ...existing.tags,
      ...normalizeTags(candidate.tags),
    ]);
    const merged: LinkEntry = {
      ...existing,
      title: existing.title === "" && title !== "" ? title : existing.title,
      note: existing.note === "" && note !== "" ? note : existing.note,
      tags,
      url:
        existing.url.startsWith("http:") && url.startsWith("https:")
          ? url
          : existing.url,
    };
    const theirs: Conflict["theirs"] = {};
    if (title !== "" && merged.title !== title) theirs.title = title;
    if (note !== "" && merged.note !== note) theirs.note = note;
    if (group !== "" && merged.group !== group) theirs.group = group;

    const changed =
      merged.title !== existing.title ||
      merged.note !== existing.note ||
      merged.url !== existing.url ||
      merged.tags.length !== existing.tags.length;
    if (changed) {
      links[index] = merged;
      updated += 1;
    } else {
      unchanged += 1;
    }
    if (Object.keys(theirs).length > 0) {
      const before = next.conflicts.length;
      next = addConflict(
        { ...next, links },
        { linkId: existing.id, source, seenAt, theirs },
      );
      if (next.conflicts.length > before) conflicts += 1;
    }
  }

  return {
    document: { ...next, links },
    added,
    updated,
    unchanged,
    refused,
    conflicts,
  };
}

/** The links carrying one tag, for a download of just those. */
export function onlyTag(
  document: LinksDocument,
  tag: string | null,
): LinksDocument {
  if (tag === null) return document;
  return { ...document, links: document.links.filter((l) => hasTag(l, tag)) };
}

function unixSeconds(isoDate: string): string {
  const millis = Date.parse(isoDate);
  return Number.isNaN(millis) ? "" : String(Math.floor(millis / 1000));
}

interface Folder {
  folders: Map<string, Folder>;
  links: LinkEntry[];
}

/**
 * The list as a Netscape bookmark file: the general list at the top level,
 * a folder per group, nested where a group is spelled as a path ("Work /
 * Tools" is a Tools folder inside Work), notes as DD lines, and the day
 * each link was added as ADD_DATE. Every browser's import dialog reads
 * this, and parseNetscape above reads the same nesting back into the same
 * group names.
 */
export function exportBookmarks(document: LinksDocument): string {
  const root: Folder = { folders: new Map(), links: [] };
  for (const link of document.links) {
    let node = root;
    for (const name of folderPath(link.group)) {
      let child = node.folders.get(name);
      if (child === undefined) {
        child = { folders: new Map(), links: [] };
        node.folders.set(name, child);
      }
      node = child;
    }
    node.links.push(link);
  }
  const lines: string[] = [
    "<!DOCTYPE NETSCAPE-Bookmark-file-1>",
    "<!-- This is an automatically generated file.",
    "     It will be read and overwritten.",
    "     DO NOT EDIT! -->",
    '<META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">',
    "<TITLE>Bookmarks</TITLE>",
    "<H1>Bookmarks</H1>",
    "<DL><p>",
  ];
  const entry = (link: LinkEntry, indent: string) => {
    const date = unixSeconds(link.addedAt);
    const dateAttribute = date === "" ? "" : ` ADD_DATE="${date}"`;
    const tagsAttribute =
      link.tags.length === 0 ? "" : ` TAGS="${encode(link.tags.join(","))}"`;
    lines.push(
      `${indent}<DT><A HREF="${encode(link.url)}"${dateAttribute}${tagsAttribute}>${encode(link.title)}</A>`,
    );
    if (link.note !== "") lines.push(`${indent}<DD>${encode(link.note)}`);
  };
  const emit = (node: Folder, indent: string) => {
    for (const link of node.links) entry(link, indent);
    const names = [...node.folders.keys()].sort((a, b) => a.localeCompare(b));
    for (const name of names) {
      lines.push(`${indent}<DT><H3>${encode(name)}</H3>`);
      lines.push(`${indent}<DL><p>`);
      emit(node.folders.get(name) as Folder, `${indent}    `);
      lines.push(`${indent}</DL><p>`);
    }
  };
  emit(root, "    ");
  lines.push("</DL><p>", "");
  return lines.join("\n");
}
