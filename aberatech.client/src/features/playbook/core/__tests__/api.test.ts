// @vitest-environment jsdom
/**
 * What the page makes of each answer. A refusal is a visitor, not an
 * error. A deployment without a token is "not connected". A 503 is Notion
 * being busy. The shell a deployment without sign-in answers with is a
 * visitor.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import { fetchPage, fetchTree, fileUrl, findPage } from "../api";

afterEach(() => vi.unstubAllGlobals());

const answer = (value: unknown) =>
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => value),
  );

describe("fetchTree", () => {
  it.each([
    [401, { status: "visitor" }],
    [403, { status: "visitor" }],
    [503, { status: "error", busy: true }],
    [502, { status: "error", busy: false }],
  ])("reads %i as %o", async (status, expected) => {
    answer(respond(status));
    expect(await fetchTree()).toEqual(expected);
  });

  it("reads configured: false as not connected", async () => {
    answer(respond(200, { configured: false }));
    expect(await fetchTree()).toEqual({ status: "unconfigured" });
  });

  it("reads an HTML shell as a visitor", async () => {
    answer({ ...respond(200), headers: { get: () => "text/html" } });
    expect(await fetchTree()).toEqual({ status: "visitor" });
  });

  it("reads a failed request as an error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new TypeError("offline");
      }),
    );
    expect(await fetchTree()).toEqual({ status: "error", busy: false });
  });
});

describe("fetchPage", () => {
  it("reads 404 as missing and asks by the page's own address", async () => {
    const fetchMock = vi.fn(async (_url: string) => respond(404));
    vi.stubGlobal("fetch", fetchMock);
    expect(await fetchPage("a b")).toEqual({ status: "missing" });
    expect(fetchMock.mock.calls[0][0]).toBe("/api/playbook/pages/a%20b");
  });
});

describe("findPage and fileUrl", () => {
  it("finds a page under a database, never the database itself", () => {
    const root = {
      id: "r",
      title: "R",
      kind: "page" as const,
      children: [
        {
          id: "d",
          title: "D",
          kind: "database" as const,
          children: [
            { id: "p", title: "P", kind: "page" as const, children: [] },
          ],
        },
      ],
    };
    expect(findPage(root, "p")?.title).toBe("P");
    expect(findPage(root, "d")).toBeUndefined();
    expect(fileUrl("f/x")).toBe("/api/playbook/files/f%2Fx");
  });
});
