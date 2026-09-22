/**
 * Bookmarks as a file: what a browser's export turns into, what the list's
 * own export looks like and that it reads back without loss, and how a file
 * folds into the list without adding anything twice.
 */
import { describe, expect, it } from "vitest";
import {
  decodeEntities,
  exportBookmarks,
  mergeLinks,
  parseBookmarks,
  parseNetscape,
  textOf,
} from "../bookmarks";
import { addLink, empty, type LinksDocument } from "../links";

const day = new Date("2026-09-12T15:00:00Z");

const chromeExport = `<!DOCTYPE NETSCAPE-Bookmark-file-1>
<!-- This is an automatically generated file.
     It will be read and overwritten.
     DO NOT EDIT! -->
<META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
<TITLE>Bookmarks</TITLE>
<H1>Bookmarks</H1>
<DL><p>
    <DT><H3 ADD_DATE="1700000000" LAST_MODIFIED="1700000000" PERSONAL_TOOLBAR_FOLDER="true">Bookmarks bar</H3>
    <DL><p>
        <DT><A HREF="https://abera.tech/" ADD_DATE="1725000000" ICON="data:image/png;base64,AAAA">abera.tech</A>
        <DT><H3 ADD_DATE="1700000000">Work</H3>
        <DL><p>
            <DT><A HREF="https://www.example.org/handbook?v=2" ADD_DATE="1725100000">Handbook &amp; guide</A>
            <DT><H3>Tools</H3>
            <DL><p>
                <DT><A HREF="http://tools.example.org/a">A tool</A>
            </DL><p>
        </DL><p>
    </DL><p>
    <DT><H3 ADD_DATE="1700000000">Other bookmarks</H3>
    <DL><p>
        <DT><A HREF="https://claude.ai/code/artifact/x" ADD_DATE="1757600000">Runway</A>
        <DD>the tracker
        <DT><A HREF="javascript:alert(1)">bad</A>
    </DL><p>
</DL><p>
`;

describe("parseNetscape", () => {
  const links = parseNetscape(chromeExport);

  it("reads every link with its folder path, leaving the browser's roots out", () => {
    expect(links.map((l) => [l.title, l.group])).toEqual([
      ["abera.tech", ""],
      ["Handbook & guide", "Work"],
      ["A tool", "Work / Tools"],
      ["Runway", ""],
      ["bad", ""],
    ]);
  });

  it("keeps the day a link was added and the note under it", () => {
    expect(links[0].addedAt).toBe("2024-08-30");
    expect(links[3].note).toBe("the tracker");
    expect(links[2].addedAt).toBeUndefined();
  });

  it("decodes entities in titles and addresses", () => {
    expect(decodeEntities("a &amp; b &lt; c &#39;d&#x27; &nbsp;")).toBe(
      "a & b < c 'd'  ",
    );
    expect(links[1].url).toBe("https://www.example.org/handbook?v=2");
  });

  it("keeps only the text of a title, however the tags are nested", () => {
    expect(textOf("A <b>bold</b> name")).toBe("A bold name");
    // Removing "<<script>" leaves "script>", and the second pass finds no
    // tag left to remove: what matters is that no "<" survives, so what
    // comes out can never be read as markup.
    const nested = textOf("<<script>script>alert(1)<</script>/script>");
    expect(nested).toBe("script>alert(1)/script>");
    expect(nested).not.toContain("<");
    expect(textOf("  Plain &amp; simple  ")).toBe("Plain & simple");
  });

  it("reads nothing from text that is not a bookmark file", () => {
    expect(parseNetscape("hello")).toEqual([]);
    expect(parseBookmarks("")).toEqual([]);
  });
});

describe("parseBookmarks with JSON", () => {
  it("reads this list's own shape and a plain array", () => {
    const own = JSON.stringify({
      version: 1,
      links: [{ url: "a.example", title: "A", group: "G", note: "n" }],
    });
    expect(parseBookmarks(own)).toEqual([
      {
        url: "a.example",
        title: "A",
        group: "G",
        note: "n",
        tags: [],
        addedAt: undefined,
      },
    ]);
    expect(
      parseBookmarks('[{"url":"b.example"}, {"title":"no url"}, 3]'),
    ).toEqual([
      {
        url: "b.example",
        title: "",
        group: "",
        note: "",
        tags: [],
        addedAt: undefined,
      },
    ]);
    expect(parseBookmarks("{not json")).toEqual([]);
  });
});

describe("mergeLinks", () => {
  const here: LinksDocument = [
    { title: "abera.tech", url: "https://abera.tech", group: "Mine" },
    {
      title: "Old name",
      url: "http://www.example.org/handbook?v=2",
      group: "Work",
    },
    { title: "Kept note", url: "https://kept.example", note: "mine" },
  ].reduce((d, link, i) => addLink(d, link, day, `id-${i}`), empty);

  const report = mergeLinks(here, parseNetscape(chromeExport), day);

  it("adds what is new, updates what cannot lose, records the rest, and refuses what is not an address", () => {
    expect(report.added).toBe(2);
    expect(report.updated).toBe(1);
    expect(report.unchanged).toBe(1);
    expect(report.refused).toBe(1);
    expect(report.conflicts).toBe(1);
    expect(report.document.links).toHaveLength(5);
  });

  it("keeps a matched link's id, group, place and title, and records the file's title to settle", () => {
    const handbook = report.document.links[1];
    expect(handbook.id).toBe("id-1");
    expect(handbook.group).toBe("Work");
    expect(handbook.title).toBe("Old name");
    // http became https without a question: nothing is lost by it.
    expect(handbook.url).toBe("https://www.example.org/handbook?v=2");
    const conflict = report.document.conflicts[0];
    expect(conflict.linkId).toBe("id-1");
    expect(conflict.theirs).toEqual({ title: "Handbook & guide" });
    expect(conflict.source).toBe("upload");
    expect(conflict.seenAt).toBe("2026-09-12");
  });

  it("fills an empty title without a question", () => {
    const untitled = addLink(
      empty,
      { title: "", url: "https://x.example" },
      day,
      "u",
    );
    const filled = mergeLinks(
      untitled,
      [{ title: "Named", url: "x.example" }],
      day,
    );
    expect(filled.updated).toBe(1);
    expect(filled.conflicts).toBe(0);
    expect(filled.document.links[0].title).toBe("Named");
  });

  it("joins the file's tags to the link's without a question", () => {
    const tagged = addLink(
      empty,
      { title: "T", url: "https://t.example", tags: ["MITRE"] },
      day,
      "t",
    );
    const joined = mergeLinks(
      tagged,
      [{ title: "T", url: "t.example", tags: ["mitre", "army"] }],
      day,
    );
    expect(joined.updated).toBe(1);
    expect(joined.document.links[0].tags).toEqual(["MITRE", "army"]);
  });

  it("does not move a link that the file has in another folder", () => {
    // abera.tech is under Mine here and at the top level in the file.
    expect(report.document.links[0].group).toBe("Mine");
    expect(report.document.links[0].id).toBe("id-0");
    expect(
      report.document.links.filter((l) => l.url.includes("abera.tech")),
    ).toHaveLength(1);
  });

  it("adds a new link under the file's folder with the file's date", () => {
    const tool = report.document.links.find((l) => l.title === "A tool");
    expect(tool?.group).toBe("Work / Tools");
    expect(tool?.addedAt).toBe("2026-09-12");
    const runway = report.document.links.find((l) => l.title === "Runway");
    expect(runway?.addedAt).toBe("2025-09-11");
    expect(runway?.note).toBe("the tracker");
  });

  it("is the same address twice in one file only once, and idempotent", () => {
    const twice = mergeLinks(
      empty,
      [
        { title: "one", url: "dup.example/" },
        { title: "two", url: "https://www.dup.example" },
      ],
      day,
    );
    expect(twice.added).toBe(1);
    expect(twice.updated).toBe(0);
    expect(twice.conflicts).toBe(1);
    expect(twice.document.links[0].title).toBe("one");
    expect(twice.document.conflicts[0].theirs).toEqual({ title: "two" });

    const again = mergeLinks(report.document, parseNetscape(chromeExport), day);
    expect(again.added).toBe(0);
    expect(again.updated).toBe(0);
    // The same file again asks nothing new.
    expect(again.conflicts).toBe(0);
    expect(again.document.conflicts).toHaveLength(1);
    expect(again.document.links).toHaveLength(5);
  });

  it("keeps a note it has and records the file's to settle", () => {
    const withNote = mergeLinks(
      here,
      [{ title: "Kept note", url: "kept.example", note: "theirs" }],
      day,
    );
    expect(withNote.unchanged).toBe(1);
    expect(withNote.conflicts).toBe(1);
    expect(withNote.document.links[2].note).toBe("mine");
    expect(withNote.document.conflicts[0].theirs).toEqual({ note: "theirs" });
  });

  it("records a different folder to settle, and never moves the link itself", () => {
    const moved = mergeLinks(
      here,
      [{ title: "abera.tech", url: "https://abera.tech", group: "Elsewhere" }],
      day,
    );
    expect(moved.document.links[0].group).toBe("Mine");
    expect(moved.document.conflicts[0].theirs).toEqual({ group: "Elsewhere" });
  });
});

describe("exportBookmarks", () => {
  const doc: LinksDocument = [
    { title: "Top <one>", url: "https://a.example/?q=1&r=2" },
    {
      title: "Grouped",
      url: "https://b.example",
      group: "Work",
      note: 'say "hi"',
    },
  ].reduce((d, link, i) => addLink(d, link, day, `id-${i}`), empty);

  const file = exportBookmarks(doc);

  it("is a Netscape bookmark file with a folder per group", () => {
    expect(file.startsWith("<!DOCTYPE NETSCAPE-Bookmark-file-1>")).toBe(true);
    expect(file).toContain("<DT><H3>Work</H3>");
    expect(file).toContain(
      '<DT><A HREF="https://a.example/?q=1&amp;r=2" ADD_DATE="1789171200">Top &lt;one&gt;</A>',
    );
    expect(file).toContain("<DD>say &quot;hi&quot;");
  });

  it("nests folders when a group is spelled as a path, and reads them back", () => {
    const nested: LinksDocument = [
      { title: "Top", url: "top.example", group: "Work" },
      { title: "Deep", url: "deep.example", group: "Work / Tools / CLI" },
      { title: "Sibling", url: "sib.example", group: "Work / Notes" },
    ].reduce((d, link, i) => addLink(d, link, day, `n-${i}`), empty);
    const out = exportBookmarks(nested);

    // One Work folder, with Notes and Tools inside it and CLI inside Tools.
    expect(out.match(/<H3>Work<\/H3>/g)).toHaveLength(1);
    const work = out.indexOf("<H3>Work</H3>");
    const notes = out.indexOf("<H3>Notes</H3>");
    const tools = out.indexOf("<H3>Tools</H3>");
    const cli = out.indexOf("<H3>CLI</H3>");
    expect(work).toBeLessThan(notes);
    expect(notes).toBeLessThan(tools);
    expect(tools).toBeLessThan(cli);
    expect(out).toContain("        <DT><H3>Tools</H3>");
    expect(out).toContain("            <DT><H3>CLI</H3>");

    const back = mergeLinks(empty, parseBookmarks(out), day);
    expect(back.document.links.map((l) => [l.title, l.group])).toEqual([
      ["Top", "Work"],
      ["Sibling", "Work / Notes"],
      ["Deep", "Work / Tools / CLI"],
    ]);
  });

  it("carries tags as the TAGS attribute, and reads them back", () => {
    const tagged = addLink(
      empty,
      { title: "T", url: "https://t.example", tags: ["MITRE", "army"] },
      day,
      "t",
    );
    const out = exportBookmarks(tagged);
    expect(out).toContain('TAGS="MITRE,army"');
    const back = parseBookmarks(out);
    expect(back[0].tags).toEqual(["MITRE", "army"]);
  });

  it("reads back into the same links", () => {
    const back = mergeLinks(empty, parseBookmarks(file), day);
    expect(back.added).toBe(2);
    expect(
      back.document.links.map((l) => [
        l.title,
        l.url,
        l.group,
        l.note,
        l.addedAt,
      ]),
    ).toEqual([
      ["Top <one>", "https://a.example/?q=1&r=2", "", "", "2026-09-12"],
      ["Grouped", "https://b.example/", "Work", 'say "hi"', "2026-09-12"],
    ]);
  });
});
