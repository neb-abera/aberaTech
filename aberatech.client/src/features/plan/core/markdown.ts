/**
 * The small Markdown the plan is written in, parsed to a tree the page
 * renders as React elements.
 *
 * A subset on purpose: headings, paragraphs, bullet and numbered lists,
 * pipe tables, fenced code, block quotes, rules, and inline bold, italic,
 * code and links. Nothing here produces HTML, so nothing in a document can
 * inject markup; a link is kept only when it is a web address. It is a
 * hand-written parser rather than a dependency because the dependency
 * would bring an HTML renderer, and an HTML renderer is exactly what an
 * owner-only page full of personal notes should not have.
 */

export type Inline =
  | { kind: "text"; text: string }
  | { kind: "bold"; children: Inline[] }
  | { kind: "italic"; children: Inline[] }
  | { kind: "code"; text: string }
  | { kind: "link"; href: string; children: Inline[] };

export type Block =
  | { kind: "heading"; level: 1 | 2 | 3 | 4; children: Inline[] }
  | { kind: "paragraph"; children: Inline[] }
  | { kind: "list"; ordered: boolean; items: Inline[][] }
  | { kind: "table"; header: Inline[][]; rows: Inline[][][] }
  | { kind: "code"; text: string }
  | { kind: "quote"; children: Inline[] }
  | { kind: "rule" };

const WEB = /^https?:\/\//i;

/** Inline markup: bold, italic, code and links, left to right, nested once. */
export function parseInline(text: string): Inline[] {
  const out: Inline[] = [];
  let buffer = "";
  const flush = () => {
    if (buffer !== "") {
      out.push({ kind: "text", text: buffer });
      buffer = "";
    }
  };
  let i = 0;
  while (i < text.length) {
    const rest = text.slice(i);
    const code = /^`([^`]+)`/.exec(rest);
    if (code) {
      flush();
      out.push({ kind: "code", text: code[1] });
      i += code[0].length;
      continue;
    }
    const bold = /^\*\*(.+?)\*\*/.exec(rest);
    if (bold) {
      flush();
      out.push({ kind: "bold", children: parseInline(bold[1]) });
      i += bold[0].length;
      continue;
    }
    const italic = /^(?:\*([^*\s][^*]*?)\*|_([^_\s][^_]*?)_)(?![\w])/.exec(
      rest,
    );
    if (italic) {
      flush();
      out.push({
        kind: "italic",
        children: parseInline(italic[1] ?? italic[2]),
      });
      i += italic[0].length;
      continue;
    }
    const link = /^\[([^\]]+)\]\(([^)\s]+)\)/.exec(rest);
    if (link) {
      flush();
      if (WEB.test(link[2])) {
        out.push({
          kind: "link",
          href: link[2],
          children: parseInline(link[1]),
        });
      } else {
        out.push({ kind: "text", text: link[1] });
      }
      i += link[0].length;
      continue;
    }
    const bare = /^https?:\/\/[^\s<>()]+[^\s<>().,;:!?'"]/.exec(rest);
    if (bare) {
      flush();
      out.push({
        kind: "link",
        href: bare[0],
        children: [{ kind: "text", text: bare[0] }],
      });
      i += bare[0].length;
      continue;
    }
    buffer += text[i];
    i += 1;
  }
  flush();
  return out;
}

function cells(line: string): string[] {
  return line
    .trim()
    .replace(/^\|/, "")
    .replace(/\|$/, "")
    .split("|")
    .map((c) => c.trim());
}

const isSeparator = (line: string) =>
  /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(line);

/** The document as blocks. Lines that fit no block become paragraphs. */
export function parseMarkdown(source: string): Block[] {
  const lines = source.replace(/\r\n?/g, "\n").split("\n");
  const blocks: Block[] = [];
  let paragraph: string[] = [];
  const closeParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push({
        kind: "paragraph",
        children: parseInline(paragraph.join(" ")),
      });
      paragraph = [];
    }
  };

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    if (trimmed === "") {
      closeParagraph();
      i += 1;
      continue;
    }

    if (/^```/.test(trimmed)) {
      closeParagraph();
      const body: string[] = [];
      i += 1;
      while (i < lines.length && !/^```/.test(lines[i].trim())) {
        body.push(lines[i]);
        i += 1;
      }
      blocks.push({ kind: "code", text: body.join("\n") });
      i += 1;
      continue;
    }

    const heading = /^(#{1,4})\s+(.*)$/.exec(trimmed);
    if (heading) {
      closeParagraph();
      blocks.push({
        kind: "heading",
        level: heading[1].length as 1 | 2 | 3 | 4,
        children: parseInline(heading[2].replace(/\s+#+$/, "")),
      });
      i += 1;
      continue;
    }

    if (/^(-{3,}|\*{3,}|_{3,})$/.test(trimmed)) {
      closeParagraph();
      blocks.push({ kind: "rule" });
      i += 1;
      continue;
    }

    if (/^>\s?/.test(trimmed)) {
      closeParagraph();
      const body: string[] = [];
      while (i < lines.length && /^>\s?/.test(lines[i].trim())) {
        body.push(lines[i].trim().replace(/^>\s?/, ""));
        i += 1;
      }
      blocks.push({ kind: "quote", children: parseInline(body.join(" ")) });
      continue;
    }

    if (
      trimmed.includes("|") &&
      i + 1 < lines.length &&
      isSeparator(lines[i + 1])
    ) {
      closeParagraph();
      const header = cells(trimmed).map(parseInline);
      i += 2;
      const rows: Inline[][][] = [];
      while (i < lines.length && lines[i].trim().includes("|")) {
        rows.push(cells(lines[i]).map(parseInline));
        i += 1;
      }
      blocks.push({ kind: "table", header, rows });
      continue;
    }

    const bullet = /^([-*+]|\d+[.)])\s+(.*)$/.exec(trimmed);
    if (bullet) {
      closeParagraph();
      const ordered = /^\d/.test(bullet[1]);
      const items: string[] = [];
      while (i < lines.length) {
        const item = /^([-*+]|\d+[.)])\s+(.*)$/.exec(lines[i].trim());
        if (item && /^\d/.test(item[1]) === ordered) {
          items.push(item[2]);
          i += 1;
        } else if (
          items.length > 0 &&
          lines[i].trim() !== "" &&
          /^\s{2,}/.test(lines[i]) &&
          !/^([-*+]|\d+[.)])\s+/.test(lines[i].trim())
        ) {
          // A wrapped line belongs to the item above it.
          items[items.length - 1] += ` ${lines[i].trim()}`;
          i += 1;
        } else {
          break;
        }
      }
      blocks.push({ kind: "list", ordered, items: items.map(parseInline) });
      continue;
    }

    paragraph.push(trimmed);
    i += 1;
  }
  closeParagraph();
  return blocks;
}

/** The plain text of inline content, for headings' ids and tests. */
export function plainText(inline: Inline[]): string {
  return inline
    .map((node) =>
      node.kind === "text" || node.kind === "code"
        ? node.text
        : plainText(node.children),
    )
    .join("");
}
