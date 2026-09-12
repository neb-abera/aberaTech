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
  addLink,
  type LinkEntry,
  type LinksDocument,
  type NewLink,
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
  /** Addresses that were here and took a new title or note. */
  updated: number;
  /** Addresses that were here and needed nothing. */
  unchanged: number;
  /** Lines that were not web addresses. */
  refused: number;
}

/**
 * The list with a file's links folded in. Matching is by address (see
 * urlKey): a match keeps its id, its group and its place in the list, takes
 * the file's title when the file has one that differs, keeps its own note
 * unless it had none, and moves from http to https when the file has the
 * https address. A new address is added under the file's folder. The same
 * address twice in one file is one link.
 */
export function mergeLinks(
  document: LinksDocument,
  incoming: ImportedLink[],
  now: Date = new Date(),
): MergeReport {
  const byKey = new Map<string, number>();
  document.links.forEach((link, index) => {
    byKey.set(urlKey(link.url), index);
  });
  const links: LinkEntry[] = document.links.slice();
  let added = 0;
  let updated = 0;
  let unchanged = 0;
  let refused = 0;

  for (const candidate of incoming) {
    const url = normalizeUrl(candidate.url);
    if (url === null) {
      refused += 1;
      continue;
    }
    const key = urlKey(url);
    const index = byKey.get(key);
    if (index === undefined) {
      const next = addLink(
        { version: 1, links: [] },
        { ...candidate, url },
        now,
      );
      links.push(next.links[0]);
      byKey.set(key, links.length - 1);
      added += 1;
      continue;
    }
    const existing = links[index];
    const title = candidate.title.trim();
    const note = (candidate.note ?? "").trim();
    const merged: LinkEntry = {
      ...existing,
      title: title !== "" && title !== existing.title ? title : existing.title,
      note: existing.note === "" && note !== "" ? note : existing.note,
      url:
        existing.url.startsWith("http:") && url.startsWith("https:")
          ? url
          : existing.url,
    };
    if (
      merged.title === existing.title &&
      merged.note === existing.note &&
      merged.url === existing.url
    ) {
      unchanged += 1;
    } else {
      links[index] = merged;
      updated += 1;
    }
  }

  return {
    document: { version: 1, links },
    added,
    updated,
    unchanged,
    refused,
  };
}

function unixSeconds(isoDate: string): string {
  const millis = Date.parse(isoDate);
  return Number.isNaN(millis) ? "" : String(Math.floor(millis / 1000));
}

/**
 * The list as a Netscape bookmark file: one folder per group, the general
 * list at the top level, notes as DD lines, and the day each link was added
 * as ADD_DATE. Every browser's import dialog reads this, and so does
 * parseNetscape above.
 */
export function exportBookmarks(document: LinksDocument): string {
  const groups = new Map<string, LinkEntry[]>();
  for (const link of document.links) {
    const list = groups.get(link.group) ?? [];
    list.push(link);
    groups.set(link.group, list);
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
    lines.push(
      `${indent}<DT><A HREF="${encode(link.url)}"${dateAttribute}>${encode(link.title)}</A>`,
    );
    if (link.note !== "") lines.push(`${indent}<DD>${encode(link.note)}`);
  };
  for (const link of groups.get("") ?? []) entry(link, "    ");
  const named = [...groups.keys()]
    .filter((group) => group !== "")
    .sort((a, b) => a.localeCompare(b));
  for (const group of named) {
    lines.push(`    <DT><H3>${encode(group)}</H3>`);
    lines.push("    <DL><p>");
    for (const link of groups.get(group) ?? []) entry(link, "        ");
    lines.push("    </DL><p>");
  }
  lines.push("</DL><p>", "");
  return lines.join("\n");
}
