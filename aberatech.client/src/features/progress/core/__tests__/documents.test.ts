import { afterEach, describe, expect, it, vi } from "vitest";
import { loadDocument, saveDocument } from "../documents";

const respond = (
  status: number,
  body: unknown = null,
  type = "application/json",
) =>
  vi.fn().mockResolvedValue({
    status,
    ok: status >= 200 && status < 300,
    headers: { get: (name: string) => (name === "content-type" ? type : null) },
    json: async () => body,
  });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("loadDocument", () => {
  it("calls a 401 a visitor, not an error", async () => {
    vi.stubGlobal("fetch", respond(401));
    expect(await loadDocument("rf-training")).toEqual({ status: "visitor" });
  });

  it("calls a 403 a visitor too", async () => {
    vi.stubGlobal("fetch", respond(403));
    expect(await loadDocument("rf-training")).toEqual({ status: "visitor" });
  });

  it("returns the owner's document, or null when none is saved yet", async () => {
    vi.stubGlobal("fetch", respond(200, { version: 1, done: ["a"] }));
    expect(await loadDocument("rf-training")).toEqual({
      status: "owner",
      value: { version: 1, done: ["a"] },
    });

    vi.stubGlobal("fetch", respond(404));
    expect(await loadDocument("rf-training")).toEqual({
      status: "owner",
      value: null,
    });
  });

  it("treats a shell answered in place of JSON as a visitor", async () => {
    // A deployment with no owner configured serves the page, not the API.
    vi.stubGlobal("fetch", respond(200, null, "text/html"));
    expect(await loadDocument("rf-training")).toEqual({ status: "visitor" });
  });

  it("reports a failure as an error rather than throwing", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
    expect(await loadDocument("rf-training")).toEqual({ status: "error" });
    vi.stubGlobal("fetch", respond(500));
    expect(await loadDocument("rf-training")).toEqual({ status: "error" });
  });

  it("asks with the session cookie and for JSON", async () => {
    const fetchMock = respond(404);
    vi.stubGlobal("fetch", fetchMock);
    await loadDocument("planner");
    expect(fetchMock).toHaveBeenCalledWith("/api/progress/planner", {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    });
  });
});

describe("saveDocument", () => {
  it("puts the document as JSON and says whether it was taken", async () => {
    const fetchMock = respond(204);
    vi.stubGlobal("fetch", fetchMock);

    expect(await saveDocument("planner", { a: 1 }, true)).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith("/api/progress/planner", {
      method: "PUT",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: '{"a":1}',
      keepalive: true,
    });
  });

  it("returns false on a refusal or a failure", async () => {
    vi.stubGlobal("fetch", respond(413));
    expect(await saveDocument("planner", {})).toBe(false);
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
    expect(await saveDocument("planner", {})).toBe(false);
  });
});
