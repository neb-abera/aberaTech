// @vitest-environment jsdom
/**
 * What the page makes of each answer, and how the log is laid out.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import {
  columnsOf,
  fetchPlan,
  type LogRow,
  newestFirst,
  notionUrl,
  show,
} from "../api";

afterEach(() => vi.unstubAllGlobals());

const answer = (value: unknown) =>
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => value),
  );

describe("fetchPlan", () => {
  it.each([
    [401, { status: "visitor" }],
    [403, { status: "visitor" }],
    [503, { status: "error", busy: true }],
    [502, { status: "error", busy: false }],
  ])("reads %i as %o", async (status, expected) => {
    answer(respond(status));
    expect(await fetchPlan()).toEqual(expected);
  });

  it("reads configured: false as not connected, with the server's words", async () => {
    answer(respond(200, { configured: false, message: "Set the page." }));
    expect(await fetchPlan()).toEqual({
      status: "unconfigured",
      message: "Set the page.",
    });
  });

  it("reads an HTML shell as a visitor", async () => {
    answer({ ...respond(200), headers: { get: () => "text/html" } });
    expect(await fetchPlan()).toEqual({ status: "visitor" });
  });

  it("reads the page and its tables", async () => {
    const page = { id: "p", title: "Plan", blocks: [] };
    const tables = [{ id: "t", title: "Log", rows: [] }];
    answer(respond(200, { configured: true, page, tables }));
    expect(await fetchPlan()).toEqual({ status: "owner", page, tables });
  });

  it("reads a network failure as an error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new TypeError("offline");
      }),
    );
    expect(await fetchPlan()).toEqual({ status: "error", busy: false });
  });
});

describe("the log", () => {
  const rows: LogRow[] = [
    { id: "a", values: { Name: "Old", Date: "2026-11-20", Notes: "n" } },
    { id: "b", values: { Name: "Undated", Extra: true } },
    { id: "c", values: { Name: "New", Date: "2026-12-01", Total: 315 } },
  ];

  it("puts the newest row first and an undated row last", () => {
    expect(newestFirst(rows).map((row) => row.id)).toEqual(["c", "a", "b"]);
  });

  it("lists only filled columns, known ones in plan order, then the rest", () => {
    expect(columnsOf(rows)).toEqual([
      "Date",
      "Name",
      "Total",
      "Notes",
      "Extra",
    ]);
  });

  it("shows a value as text", () => {
    expect(show(undefined)).toBe("");
    expect(show(true)).toBe("Yes");
    expect(show(false)).toBe("No");
    expect(show(342)).toBe("342");
  });
});

describe("notionUrl", () => {
  it("points at the page by its id without dashes", () => {
    expect(notionUrl("40d3731a-4cfa-813a-91be-c961d3875ebc")).toBe(
      "https://www.notion.so/40d3731a4cfa813a91bec961d3875ebc",
    );
  });
});
