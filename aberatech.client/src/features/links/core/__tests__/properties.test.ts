/**
 * Properties of the bookmark code under generated input, with fast-check.
 * A browser export is untrusted bytes; these hold for any of them:
 *
 * - the parsers never throw and every link they return has a usable
 *   address;
 * - a document survives export, parse and merge with its titles, addresses,
 *   folders, notes and tags intact, and asks nothing;
 * - merging the same file twice adds nothing and asks nothing new;
 * - coerce accepts anything JSON can spell.
 *
 * Seeded and bounded, so a failing run prints the seed and the shrunk
 * input, and a run in CI takes seconds.
 */

import fc from "fast-check";
import { describe, expect, it } from "vitest";
import {
  exportBookmarks,
  mergeLinks,
  parseBookmarks,
  parseNetscape,
} from "../bookmarks";
import {
  addLink,
  coerce,
  empty,
  type LinksDocument,
  normalizeUrl,
} from "../links";

const day = new Date("2026-09-22T12:00:00Z");

/** A word a title, folder or tag could be: no angle brackets, no slash, no comma. */
const word = fc
  .string({ minLength: 1, maxLength: 12 })
  .map((s) => s.replace(/[<>/,&"\s]+/g, "x").trim())
  .filter((s) => s !== "" && s.toLowerCase() !== "general");

const host = fc
  .tuple(
    fc.stringMatching(/^[a-z][a-z0-9]{0,8}$/),
    fc.constantFrom("com", "org", "mil", "tech"),
  )
  .map(([name, tld]) => `${name}.${tld}`);

const link = fc.record({
  title: word,
  url: fc
    .tuple(host, fc.stringMatching(/^[a-z0-9]{0,6}$/))
    .map(([h, p]) => `https://${h}/${p}`),
  group: fc.array(word, { maxLength: 2 }).map((parts) => parts.join(" / ")),
  note: fc.oneof(fc.constant(""), word),
  tags: fc.uniqueArray(word, {
    maxLength: 3,
    comparator: (a, b) => a.toLowerCase() === b.toLowerCase(),
  }),
});

/** A document whose addresses are distinct, as one the page holds always is. */
const document = fc
  .uniqueArray(link, { maxLength: 8, selector: (l) => l.url.toLowerCase() })
  .map((links) =>
    links.reduce((d, l, i) => addLink(d, l, day, `id-${i}`), empty),
  );

const settings = { numRuns: 200 };

describe("the parsers", () => {
  it("never throw on any text, and every link they return has an address", () => {
    fc.assert(
      fc.property(fc.string({ maxLength: 2000 }), (text) => {
        for (const parsed of [parseNetscape(text), parseBookmarks(text)]) {
          for (const l of parsed) {
            expect(typeof l.url).toBe("string");
            expect(typeof l.title).toBe("string");
          }
        }
      }),
      settings,
    );
  });

  it("never throw on Netscape-shaped text with junk in every slot", () => {
    const junk = fc.string({ maxLength: 40 });
    const fragment = fc.oneof(
      junk.map((s) => `<DT><H3>${s}</H3>`),
      fc.constant("<DL><p>"),
      fc.constant("</DL><p>"),
      fc
        .tuple(junk, junk, junk)
        .map(([a, t, tags]) => `<DT><A HREF="${a}" TAGS="${tags}">${t}</A>`),
      junk.map((s) => `<DD>${s}`),
    );
    fc.assert(
      fc.property(fc.array(fragment, { maxLength: 30 }), (parts) => {
        const links = parseNetscape(parts.join("\n"));
        for (const l of links) expect(l.url.length).toBeGreaterThan(0);
      }),
      settings,
    );
  });
});

describe("export and merge", () => {
  it("round-trip a document without loss and without a question", () => {
    fc.assert(
      fc.property(document, (doc) => {
        const back = mergeLinks(
          empty,
          parseBookmarks(exportBookmarks(doc)),
          day,
        );
        expect(back.refused).toBe(0);
        expect(back.conflicts).toBe(0);
        const key = (l: { url: string }) => normalizeUrl(l.url) ?? l.url;
        const mine = new Map(doc.links.map((l) => [key(l), l]));
        expect(back.document.links).toHaveLength(doc.links.length);
        for (const l of back.document.links) {
          const original = mine.get(key(l));
          expect(original).toBeDefined();
          if (!original) continue;
          expect(l.title).toBe(original.title);
          expect(l.group).toBe(original.group);
          expect(l.note).toBe(original.note);
          expect(l.tags).toEqual(original.tags);
        }
      }),
      settings,
    );
  });

  it("is idempotent: the same file twice adds nothing and asks nothing new", () => {
    fc.assert(
      fc.property(
        document,
        fc.array(link, { maxLength: 8 }),
        (doc, incoming) => {
          const once = mergeLinks(doc, incoming, day, "f.html");
          const twice = mergeLinks(once.document, incoming, day, "f.html");
          expect(twice.added).toBe(0);
          expect(twice.conflicts).toBe(0);
          expect(twice.document.links).toHaveLength(once.document.links.length);
          expect(twice.document.conflicts).toHaveLength(
            once.document.conflicts.length,
          );
        },
      ),
      settings,
    );
  });

  it("never changes a title, note or folder that was already here without a conflict", () => {
    fc.assert(
      fc.property(
        document,
        fc.array(link, { maxLength: 8 }),
        (doc, incoming) => {
          const merged = mergeLinks(doc, incoming, day, "f.html");
          const conflicted = new Set(
            merged.document.conflicts.map((c) => c.linkId),
          );
          for (const before of doc.links) {
            const after = merged.document.links.find((l) => l.id === before.id);
            expect(after).toBeDefined();
            if (!after) continue;
            expect(after.group).toBe(before.group);
            if (before.title !== "" && after.title !== before.title)
              expect(conflicted).toContain(before.id);
            if (before.note !== "" && after.note !== before.note)
              expect(conflicted).toContain(before.id);
            if (before.title !== "") expect(after.title).toBe(before.title);
            if (before.note !== "") expect(after.note).toBe(before.note);
          }
        },
      ),
      settings,
    );
  });
});

describe("coerce", () => {
  it("accepts anything JSON can spell and returns a document", () => {
    fc.assert(
      fc.property(fc.jsonValue(), (value) => {
        const doc: LinksDocument = coerce(value);
        expect(doc.version).toBe(1);
        expect(Array.isArray(doc.links)).toBe(true);
        expect(Array.isArray(doc.conflicts)).toBe(true);
        for (const l of doc.links) expect(normalizeUrl(l.url)).not.toBeNull();
        for (const c of doc.conflicts)
          expect(doc.links.some((l) => l.id === c.linkId)).toBe(true);
      }),
      settings,
    );
  });
});
