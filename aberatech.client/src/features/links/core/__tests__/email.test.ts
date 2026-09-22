/**
 * Emailing the list: a browser with a share sheet gets the file, one without
 * gets a mailto whose body is the instructions and the file, and a list too
 * long for a mail body goes to the clipboard instead.
 */

import { describe, expect, it } from "vitest";
import {
  bodyFor,
  fileNameFor,
  instructionsFor,
  mailtoHref,
  mailtoLimit,
  planEmail,
  subjectFor,
} from "../email";
import type { LinksDocument } from "../links";

const date = new Date("2026-09-22T14:00:00Z");

const list = (n: number): LinksDocument => ({
  version: 1,
  links: Array.from({ length: n }, (_, i) => ({
    id: `id-${i}`,
    title: `Link ${i}`,
    url: `https://example.com/${i}`,
    group: "Reading",
    note: "",
    addedAt: "2026-09-01T00:00:00Z",
  })),
});

describe("the mail", () => {
  it("is named and titled by the day", () => {
    expect(fileNameFor(date)).toBe("links-2026-09-22.html");
    expect(subjectFor(date)).toBe("Links 2026-09-22");
  });

  it("carries the instructions, a line, then the bookmark file", () => {
    const body = bodyFor(list(2), "links-2026-09-22.html");
    const [before, after] = body.split("\n-----\n");
    expect(before).toContain("save it");
    expect(before).toContain("links-2026-09-22.html");
    expect(before).toContain("https://abera.tech/links");
    expect(after.startsWith("<!DOCTYPE NETSCAPE-Bookmark-file-1>")).toBe(true);
    expect(after).toContain("https://example.com/1");
  });

  it("is a mailto with everything encoded", () => {
    const href = mailtoHref("Links 2026-09-22", "a&b\n<c>");
    expect(href).toBe(
      "mailto:?subject=Links%202026-09-22&body=a%26b%0A%3Cc%3E",
    );
  });
});

describe("planEmail", () => {
  it("shares the file where the browser can", () => {
    const plan = planEmail(list(3), date, {
      share: async () => {},
      canShare: () => true,
    });
    expect(plan.kind).toBe("share");
    if (plan.kind !== "share") return;
    expect(plan.file.name).toBe("links-2026-09-22.html");
    expect(plan.file.type).toBe("text/html");
    expect(plan.title).toBe("Links 2026-09-22");
  });

  it("falls back to a mailto where it cannot", () => {
    const plan = planEmail(list(3), date, undefined);
    expect(plan.kind).toBe("mailto");
    if (plan.kind !== "mailto") return;
    expect(
      plan.href.startsWith("mailto:?subject=Links%202026-09-22&body="),
    ).toBe(true);
    expect(decodeURIComponent(plan.href)).toContain(
      "<!DOCTYPE NETSCAPE-Bookmark-file-1>",
    );
  });

  it("goes to the clipboard when the body would not fit a mailto", () => {
    const plan = planEmail(list(400), date, {
      share: undefined,
      canShare: undefined,
    } as never);
    expect(plan.kind).toBe("copy");
    if (plan.kind !== "copy") return;
    expect(plan.text.length).toBeGreaterThan(mailtoLimit);
    expect(plan.text).toContain(instructionsFor("links-2026-09-22.html"));
  });

  it("treats a share sheet that refuses files as absent", () => {
    const plan = planEmail(list(1), date, {
      share: async () => {},
      canShare: () => false,
    });
    expect(plan.kind).toBe("mailto");
  });
});
