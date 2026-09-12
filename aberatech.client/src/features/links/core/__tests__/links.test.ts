/**
 * The bookmark document, as arithmetic: what counts as a URL, how a link
 * is added and removed, how the headings are ordered, and what a row from
 * another build of the page turns into.
 */
import { describe, expect, it } from "vitest";
import {
  addLink,
  coerce,
  empty,
  GENERAL,
  groupsOf,
  hostOf,
  inGroup,
  normalizeUrl,
  removeLink,
  search,
  titleOf,
} from "../links";

const day = new Date("2026-09-12T15:00:00Z");

describe("normalizeUrl", () => {
  it("keeps a web URL and adds https to a bare host", () => {
    expect(normalizeUrl("https://abera.tech/links")).toBe(
      "https://abera.tech/links",
    );
    expect(normalizeUrl("abera.tech")).toBe("https://abera.tech/");
    expect(normalizeUrl("  http://example.org/a?b=1 ")).toBe(
      "http://example.org/a?b=1",
    );
  });

  it("refuses what is not a web address", () => {
    expect(normalizeUrl("")).toBeNull();
    expect(normalizeUrl("   ")).toBeNull();
    expect(normalizeUrl("javascript:alert(1)")).toBeNull();
    expect(normalizeUrl("mailto:someone@example.org")).toBeNull();
    expect(normalizeUrl("https://")).toBeNull();
  });
});

describe("addLink and removeLink", () => {
  it("appends a normalised entry with the date it was added", () => {
    const doc = addLink(
      empty,
      {
        title: " Runway ",
        url: "claude.ai/code/artifact/abc",
        group: " Plans ",
      },
      day,
      "id-1",
    );
    expect(doc.links).toEqual([
      {
        id: "id-1",
        title: "Runway",
        url: "https://claude.ai/code/artifact/abc",
        group: "Plans",
        note: "",
        addedAt: "2026-09-12",
      },
    ]);
  });

  it("leaves the document alone when the URL is not one", () => {
    expect(addLink(empty, { title: "x", url: "not a url at all" })).toBe(empty);
  });

  it("removes by id and nothing else", () => {
    const doc = addLink(
      addLink(empty, { title: "a", url: "a.example" }, day, "a"),
      { title: "b", url: "b.example" },
      day,
      "b",
    );
    expect(removeLink(doc, "a").links.map((l) => l.id)).toEqual(["b"]);
    expect(removeLink(doc, "zzz").links).toHaveLength(2);
  });
});

describe("headings", () => {
  const doc = [
    { title: "one", url: "one.example", group: "Work" },
    { title: "two", url: "two.example" },
    { title: "three", url: "three.example", group: "Admin" },
    { title: "four", url: "four.example", group: "Work" },
  ].reduce((d, link, i) => addLink(d, link, day, `id-${i}`), empty);

  it("puts the general list first and the rest in order", () => {
    expect(groupsOf(doc)).toEqual([GENERAL, "Admin", "Work"]);
  });

  it("lists a heading's links newest first", () => {
    expect(inGroup(doc, "Work").map((l) => l.title)).toEqual(["four", "one"]);
    expect(inGroup(doc, GENERAL).map((l) => l.title)).toEqual(["two"]);
  });

  it("omits the general heading when every link is grouped", () => {
    expect(groupsOf(removeLink(doc, "id-1"))).toEqual(["Admin", "Work"]);
  });
});

describe("search", () => {
  const doc = [
    { title: "Tracker", url: "claude.ai/code/artifact/x", group: "Plans" },
    { title: "Payroll", url: "hr.example.org", note: "monthly" },
  ].reduce((d, link, i) => addLink(d, link, day, `id-${i}`), empty);

  it("matches title, host, group and note, case-insensitively", () => {
    expect(search(doc, "TRACK").links).toHaveLength(1);
    expect(search(doc, "claude.ai").links[0].title).toBe("Tracker");
    expect(search(doc, "plans").links[0].title).toBe("Tracker");
    expect(search(doc, "monthly").links[0].title).toBe("Payroll");
    expect(search(doc, "").links).toHaveLength(2);
    expect(search(doc, "nothing").links).toHaveLength(0);
  });
});

describe("what the page shows", () => {
  it("falls back to the host when a link has no title", () => {
    expect(hostOf("https://www.example.org/path")).toBe("example.org");
    expect(titleOf({ title: "", url: "https://www.example.org/path" })).toBe(
      "example.org",
    );
    expect(titleOf({ title: "Named", url: "https://example.org" })).toBe(
      "Named",
    );
  });
});

describe("coerce", () => {
  it("accepts a document this build wrote", () => {
    const doc = addLink(empty, { title: "a", url: "a.example" }, day, "a");
    expect(coerce(doc)).toEqual(doc);
  });

  it("drops rows that are not links and defaults missing fields", () => {
    const doc = coerce({
      version: 7,
      extra: true,
      links: [
        { url: "https://kept.example", title: 3, group: null },
        { title: "no url" },
        "text",
        null,
        { url: "javascript:alert(1)", title: "bad" },
      ],
    });
    expect(doc.version).toBe(1);
    expect(doc.links).toHaveLength(1);
    expect(doc.links[0].url).toBe("https://kept.example/");
    expect(doc.links[0].title).toBe("");
    expect(doc.links[0].group).toBe("");
    expect(doc.links[0].id).not.toBe("");
  });

  it("turns anything else into the empty document", () => {
    expect(coerce(null)).toEqual(empty);
    expect(coerce("x")).toEqual(empty);
    expect(coerce({ links: "no" })).toEqual(empty);
  });
});
