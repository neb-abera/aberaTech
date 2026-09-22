/**
 * The bookmark document, as arithmetic: what counts as a URL, how a link
 * is added and removed, how the headings are ordered, and what a row from
 * another build of the page turns into.
 */
import { describe, expect, it } from "vitest";
import {
  addConflict,
  addLink,
  coerce,
  empty,
  GENERAL,
  groupsOf,
  hostOf,
  inGroup,
  normalizeGroup,
  normalizeTags,
  normalizeUrl,
  removeLink,
  resolveConflict,
  search,
  slugOf,
  tagsOf,
  titleOf,
  updateLink,
  urlKey,
  withTag,
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

  it("treats a host with a port as a web address", () => {
    expect(normalizeUrl("localhost:5173")).toBe("https://localhost:5173/");
    expect(normalizeUrl("abera.tech:8080/x?y=1")).toBe(
      "https://abera.tech:8080/x?y=1",
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
        tags: [],
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

  it("files a group typed as General under the general list, not a second heading", () => {
    const typed = addLink(
      doc,
      { title: "five", url: "five.example", group: " general " },
      day,
      "id-4",
    );
    expect(groupsOf(typed)).toEqual([GENERAL, "Admin", "Work"]);
    expect(inGroup(typed, GENERAL).map((l) => l.title)).toEqual([
      "five",
      "two",
    ]);
    expect(normalizeGroup("General")).toBe("");
    expect(normalizeGroup(" Work ")).toBe("Work");
  });
});

describe("updateLink", () => {
  const doc = addLink(
    empty,
    { title: "Old", url: "old.example", group: "Work", note: "n" },
    day,
    "a",
  );

  it("replaces the fields and keeps the id, the place and the day added", () => {
    const next = updateLink(doc, "a", {
      title: " New ",
      url: "new.example/path",
      group: " Home ",
      note: "",
    });
    expect(next.links).toEqual([
      {
        id: "a",
        title: "New",
        url: "https://new.example/path",
        group: "Home",
        note: "",
        tags: [],
        addedAt: "2026-09-12",
      },
    ]);
  });

  it("leaves the document alone for a bad address or an unknown id", () => {
    expect(updateLink(doc, "a", { title: "x", url: "not a url" })).toBe(doc);
    expect(updateLink(doc, "zzz", { title: "x", url: "ok.example" })).toBe(doc);
  });
});

describe("urlKey", () => {
  it("is the same for www, a trailing slash and http versus https", () => {
    expect(urlKey("https://www.example.org/a/")).toBe("example.org/a");
    expect(urlKey("http://example.org/a")).toBe("example.org/a");
    expect(urlKey("https://example.org/a?b=1#c")).toBe("example.org/a?b=1#c");
    expect(urlKey("https://example.org/a?b=2")).not.toBe(
      urlKey("https://example.org/a?b=1"),
    );
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

describe("tags", () => {
  const doc = [
    { title: "A", url: "https://a.example", tags: "MITRE, army" },
    { title: "B", url: "https://b.example", tags: ["mitre", "Personal"] },
    { title: "C", url: "https://c.example" },
  ].reduce((d, link, i) => addLink(d, link, day, `t-${i}`), empty);

  it("are trimmed, deduplicated regardless of case, and keep the first spelling", () => {
    expect(normalizeTags(" MITRE ,army, Army,,")).toEqual(["MITRE", "army"]);
    expect(normalizeTags(["x", "X"])).toEqual(["x"]);
    expect(normalizeTags(undefined)).toEqual([]);
  });

  it("are listed once each, alphabetically", () => {
    expect(tagsOf(doc)).toEqual(["army", "MITRE", "Personal"]);
  });

  it("narrow the list regardless of case, and no tag is the whole list", () => {
    expect(withTag(doc, "mitre").links.map((l) => l.title)).toEqual(["A", "B"]);
    expect(withTag(doc, null)).toBe(doc);
    expect(search(doc, "person").links.map((l) => l.title)).toEqual(["B"]);
  });

  it("become a file name part", () => {
    expect(slugOf("MITRE / Crypto")).toBe("mitre-crypto");
  });

  it("are kept by an edit that does not mention them", () => {
    const edited = updateLink(doc, "t-0", {
      title: "A2",
      url: "https://a.example",
    });
    expect(edited.links[0].tags).toEqual(["MITRE", "army"]);
  });
});

describe("conflicts", () => {
  const doc = addLink(
    empty,
    { title: "Mine", url: "https://c.example", group: "G", note: "n" },
    day,
    "c",
  );
  const theirs = {
    linkId: "c",
    source: "f.html",
    seenAt: "2026-09-22",
    theirs: { title: "Theirs", note: "m" },
  };

  it("are recorded once, however many times the same file is uploaded", () => {
    const once = addConflict(doc, theirs, "k1");
    const twice = addConflict(once, theirs, "k2");
    expect(twice.conflicts).toHaveLength(1);
    expect(twice.conflicts[0].id).toBe("k1");
    const other = addConflict(
      twice,
      { ...theirs, theirs: { title: "Third" } },
      "k3",
    );
    expect(other.conflicts).toHaveLength(2);
  });

  it("keep the link untouched until resolved", () => {
    const pending = addConflict(doc, theirs, "k1");
    expect(pending.links[0].title).toBe("Mine");
  });

  it("resolve to mine, to theirs, or to an edit", () => {
    const pending = addConflict(doc, theirs, "k1");
    const mine = resolveConflict(pending, "k1", { choice: "mine" });
    expect(mine.links[0].title).toBe("Mine");
    expect(mine.conflicts).toHaveLength(0);

    const taken = resolveConflict(pending, "k1", { choice: "theirs" });
    expect(taken.links[0]).toMatchObject({
      title: "Theirs",
      note: "m",
      group: "G",
    });
    expect(taken.conflicts).toHaveLength(0);

    const edited = resolveConflict(pending, "k1", {
      choice: "edit",
      fields: {
        title: "Both",
        url: "https://c.example",
        group: "G",
        note: "n",
      },
    });
    expect(edited.links[0].title).toBe("Both");
    expect(edited.conflicts).toHaveLength(0);

    expect(resolveConflict(pending, "nope", { choice: "mine" })).toBe(pending);
  });

  it("go when their link goes", () => {
    const pending = addConflict(doc, theirs, "k1");
    expect(removeLink(pending, "c").conflicts).toHaveLength(0);
  });

  it("survive a round trip through coerce, and drop ones for links that are gone", () => {
    const pending = addConflict(doc, theirs, "k1");
    const back = coerce(JSON.parse(JSON.stringify(pending)));
    expect(back.conflicts).toEqual(pending.conflicts);
    expect(back.links[0].tags).toEqual([]);
    const orphan = coerce({ ...pending, links: [] });
    expect(orphan.conflicts).toHaveLength(0);
  });
});
