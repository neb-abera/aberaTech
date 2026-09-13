/**
 * The plan's Markdown, as a tree: every block kind the plan uses, the
 * inline marks, and the two rules that matter for an owner-only page of
 * notes — nothing becomes HTML, and only a web address becomes a link.
 */
import { describe, expect, it } from "vitest";
import { parseInline, parseMarkdown, plainText } from "../markdown";

describe("parseInline", () => {
  it("reads bold, italic, code and links, and leaves the rest as text", () => {
    const inline = parseInline(
      "Plain **bold** and *italic* and `code` and [site](https://abera.tech) end",
    );
    expect(inline.map((n) => n.kind)).toEqual([
      "text",
      "bold",
      "text",
      "italic",
      "text",
      "code",
      "text",
      "link",
      "text",
    ]);
    expect(plainText(inline)).toBe(
      "Plain bold and italic and code and site end",
    );
  });

  it("keeps only web links, and turns a bare address into one", () => {
    const [bad] = parseInline("[x](javascript:alert(1))");
    expect(bad).toEqual({ kind: "text", text: "x" });
    const [, link] = parseInline("see https://example.org/a?b=1, then");
    expect(link).toEqual({
      kind: "link",
      href: "https://example.org/a?b=1",
      children: [{ kind: "text", text: "https://example.org/a?b=1" }],
    });
  });

  it("does not read an underscore inside a word as italic", () => {
    expect(parseInline("snake_case_name")).toEqual([
      { kind: "text", text: "snake_case_name" },
    ]);
  });

  it("never produces markup from angle brackets", () => {
    expect(parseInline("a <script>b</script> c")).toEqual([
      { kind: "text", text: "a <script>b</script> c" },
    ]);
  });
});

describe("parseMarkdown", () => {
  const source = `# Title

Intro line one
line two.

## Phase 0

- first item
- second item
  wrapped
- **third**

1. one
2. two

| When | Action |
|---|---|
| Dec 1 | Apply |
| Jan 15 | Deadline |

> a quote

---

\`\`\`
code block
\`\`\`

Last paragraph.
`;
  const blocks = parseMarkdown(source);

  it("finds every block kind in order", () => {
    expect(blocks.map((b) => b.kind)).toEqual([
      "heading",
      "paragraph",
      "heading",
      "list",
      "list",
      "table",
      "quote",
      "rule",
      "code",
      "paragraph",
    ]);
  });

  it("joins wrapped lines into one paragraph or item", () => {
    const [, intro, , bullets] = blocks;
    expect(intro.kind === "paragraph" && plainText(intro.children)).toBe(
      "Intro line one line two.",
    );
    expect(bullets.kind === "list" && bullets.items.map(plainText)).toEqual([
      "first item",
      "second item wrapped",
      "third",
    ]);
  });

  it("reads a table's header and rows", () => {
    const table = blocks[5];
    expect(table.kind).toBe("table");
    if (table.kind !== "table") return;
    expect(table.header.map(plainText)).toEqual(["When", "Action"]);
    expect(table.rows.map((r) => r.map(plainText))).toEqual([
      ["Dec 1", "Apply"],
      ["Jan 15", "Deadline"],
    ]);
  });

  it("levels headings and keeps code verbatim", () => {
    expect(blocks[0]).toMatchObject({ kind: "heading", level: 1 });
    expect(blocks[2]).toMatchObject({ kind: "heading", level: 2 });
    expect(blocks[8]).toEqual({ kind: "code", text: "code block" });
  });

  it("reads an empty document as nothing", () => {
    expect(parseMarkdown("")).toEqual([]);
    expect(parseMarkdown("\n\n")).toEqual([]);
  });
});
