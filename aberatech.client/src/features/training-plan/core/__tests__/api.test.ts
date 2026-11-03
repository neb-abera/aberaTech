// @vitest-environment jsdom
/**
 * What the page makes of each answer. A refusal or the shell is a visitor.
 * A deployment without the plan page passes on its message. A 503 is
 * Notion being busy.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import { fetchPlan, planFileUrl } from "../api";

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

  it("reads the owner's plan", async () => {
    const plan = {
      id: "p",
      title: "Plan",
      sections: [],
      files: [],
      workouts: [],
      notes: [],
    };
    answer(respond(200, { configured: true, plan }));
    expect(await fetchPlan()).toEqual({ status: "owner", plan });
  });

  it("passes on the not-connected message", async () => {
    answer(respond(200, { configured: false, message: "Set it." }));
    expect(await fetchPlan()).toEqual({
      status: "unconfigured",
      message: "Set it.",
    });
  });

  it("reads the shell a deployment without sign-in answers as a visitor", async () => {
    answer({ ...respond(200), headers: { get: () => "text/html" } });
    expect(await fetchPlan()).toEqual({ status: "visitor" });
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

it("escapes a note attachment's id in its address", () => {
  expect(planFileUrl("abc:0")).toBe("/api/training-plan/files/abc%3A0");
});
