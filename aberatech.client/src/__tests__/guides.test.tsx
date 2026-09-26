/**
 * The two guides as a crawler and a first visit receive them.
 *
 * /transition shipped 22 embedded viewers (Google Docs and YouTube) in its
 * prerendered HTML, all inside collapsed sections, and every one loaded on
 * every visit. The frames now mount when their section opens. The text stays
 * in the HTML: 97% of the guide's words sit inside collapsed sections, and a
 * byte saving that hides them from search is a loss.
 */
import { describe, expect, it } from "vitest";
import licences from "../components/third-party-images.json";
import { render } from "../entry-server";
import { headFor, pagePreconnects } from "../site/meta";

/**
 * The words a crawler reads in a page: markup, inline styles and scripts
 * removed, entities decoded. React's `<!-- -->` separators between adjacent
 * text nodes join rather than split.
 */
function words(html: string): string[] {
  return html
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1>/g, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) =>
      String.fromCodePoint(Number.parseInt(hex, 16)),
    )
    .replace(/&#(\d+);/g, (_, dec) => String.fromCodePoint(Number(dec)))
    .replace(/&nbsp;/g, " ")
    .replace(/&quot;/g, '"')
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&amp;/g, "&")
    .split(/\s+/)
    .filter(Boolean);
}

/**
 * Every word and how often it occurs, sorted, under the total. Order is left
 * out on purpose: a section title put the right way round keeps its words,
 * and the file diffs to exactly the words gained or lost.
 */
function wordCounts(html: string): string {
  const all = words(html);
  const counts = new Map<string, number>();
  for (const word of all) counts.set(word, (counts.get(word) ?? 0) + 1);
  const lines = [...counts]
    .sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0))
    .map(([word, count]) => `${count} ${word}`);
  return `${all.length} words\n${lines.join("\n")}\n`;
}

const imgTags = (html: string) => html.match(/<img\b[^>]*>/g) ?? [];
const attr = (tag: string, name: string) =>
  tag.match(new RegExp(`\\s${name}="([^"]*)"`))?.[1];

describe("the transition guide as first delivered", () => {
  it("embeds no document viewer or video until a section opens", async () => {
    const html = await render("/transition");

    expect(html.match(/<iframe\b/g) ?? []).toEqual([]);
    expect(html).not.toContain("embedded=true");
    expect(html).not.toContain("youtube.com/embed");
  });

  it("links every closed document instead, so a crawler still finds it", async () => {
    const html = await render("/transition");

    expect(html).toContain(
      'href="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/csp/CSP%20Checklist%2023%20MAR.pdf"',
    );
    expect(html).toContain(
      'href="https://www.youtube.com/watch?v=v1ybHS-Zmlg"',
    );
  });

  it("sizes every third-party image so nothing shifts when it lands", async () => {
    const html = await render("/transition");
    const external = imgTags(html).filter((tag) =>
      /^https:\/\//.test(attr(tag, "src") ?? ""),
    );

    expect(external.length).toBe(5);
    for (const tag of external) {
      expect(attr(tag, "width"), tag).toMatch(/^\d+$/);
      expect(attr(tag, "height"), tag).toMatch(/^\d+$/);
      expect(attr(tag, "loading"), tag).toBe("lazy");
    }
  });

  it("fetches no image through an affiliate tracking redirect", async () => {
    const html = await render("/transition");

    expect(html).not.toContain("lduhtrp.net");
  });

  it("preconnects to exactly the hosts its images come from", async () => {
    const html = await render("/transition");
    const hosts = new Set(
      imgTags(html)
        .map((tag) => attr(tag, "src") ?? "")
        .filter((src) => src.startsWith("https://"))
        .map((src) => new URL(src).origin),
    );

    expect([...(pagePreconnects["/transition"] ?? [])].sort()).toEqual(
      [...hosts].sort(),
    );
    const head = headFor("/transition");
    for (const origin of hosts) {
      expect(head).toContain(`<link rel="preconnect" href="${origin}" />`);
    }
    expect(headFor("/technical")).not.toContain("preconnect");
  });
});

describe("the technical guide as first delivered", () => {
  // Its opening video loaded on every visit, above every section, under the
  // title "test". It now mounts with a section of its own.
  it("embeds no video until its section opens", async () => {
    const html = await render("/technical");

    expect(html.match(/<iframe\b/g) ?? []).toEqual([]);
    expect(html).not.toContain("youtube.com/embed");
  });

  it("links the closed video by its own title, so a crawler still finds it", async () => {
    const html = await render("/technical");

    expect(html).toContain(
      'href="https://www.youtube.com/watch?v=ueXjGMrmn8k"',
    );
    expect(html).toContain("Why 95% of Self-Taught Programmers Fail");
    expect(html).not.toContain('title="test"');
  });
});

describe("the transition guide's mentoring advice", () => {
  // Veterati, the mentoring service the guide quotes, shut down in August
  // 2024. American Corporate Partners does the same job for free.
  it("links a mentoring service that still operates", async () => {
    const html = await render("/transition");

    expect(html).toContain('href="https://www.acp-usa.org/"');
  });
});

describe("the transition guide's third-party images", () => {
  // Every image the guide draws from another site, and the URLs its srcset
  // offers, have a licence record: the owner, the owner's terms, the date
  // they were read, and whether they allow a copy here.
  const external = async () => {
    const html = await render("/transition");
    return imgTags(html).flatMap((tag) => [
      ...[attr(tag, "src") ?? ""].filter((src) => src.startsWith("https://")),
      ...(attr(tag, "srcSet") ?? "")
        .split(",")
        .map((candidate) => candidate.trim().split(/\s+/)[0])
        .filter(Boolean),
    ]);
  };

  it("records the licence basis of every one", async () => {
    const urls = await external();
    const recorded = licences.images.flatMap((image) => [
      image.src,
      ...(image.variants ?? []),
    ]);

    expect([...new Set(urls)].sort()).toEqual([...new Set(recorded)].sort());
    for (const image of licences.images) {
      expect(image.owner, image.src).not.toBe("");
      expect(image.terms, image.src).toMatch(/^https:\/\//);
      expect(image.checked, image.src).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(image.basis, image.src).not.toBe("");
      // A hot-linked image keeps its host in the preconnects and img-src.
      if (!image.copied)
        expect(pagePreconnects["/transition"]).toContain(
          new URL(image.src).origin,
        );
    }
  });

  it("asks for the Hiring Our Heroes photo at its display size", async () => {
    // 1200 by 800 and 303 KB, shown at 438 CSS pixels at most. The owner
    // serves 300, 768 and 1024 pixel copies of the same file.
    const html = await render("/transition");
    const tag = imgTags(html).find((t) =>
      (attr(t, "src") ?? "").includes("hiringourheroes.org"),
    );

    expect(attr(tag ?? "", "src")).toContain("-768x512.jpg");
    expect(attr(tag ?? "", "srcSet")).toContain("-1024x683.jpg 1024w");
    expect(attr(tag ?? "", "sizes")).toBeTruthy();
  });

  it("draws the RecruitMilitary logo from the owner's smaller file", async () => {
    // rm_logo_new_large.png is 6167 by 2778 and 75 KB, shown at 400 pixels
    // at most. rm_logo_new_small.png is the same logo at 2047 by 481.
    const html = await render("/transition");
    const tag = imgTags(html).find((t) =>
      (attr(t, "src") ?? "").includes("recruitmilitary.com"),
    );

    expect(attr(tag ?? "", "src")).toBe(
      "https://assets.recruitmilitary.com/images/rm_logo_new_small.png",
    );
    expect(attr(tag ?? "", "width")).toBe("2047");
    expect(attr(tag ?? "", "height")).toBe("481");
  });
});

describe("what a crawler reads", () => {
  // The word counts of the prerendered guides, committed. A change that
  // drops a word from the HTML shows up here as a line lost, which is the
  // measurement the design baseline asks for before anything is hidden.
  // Deliberate copy edits update the file with `vitest -u`.
  it.each([
    ["/transition", "transition"],
    ["/technical", "technical"],
  ])("%s keeps its words", async (route, name) => {
    const html = await render(route);

    await expect(wordCounts(html)).toMatchFileSnapshot(
      `./__snapshots__/${name}.words.txt`,
    );
  });
});
