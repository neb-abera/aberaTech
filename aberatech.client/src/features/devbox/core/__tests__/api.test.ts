/**
 * The dev box API from the page's side: a visitor is a status and not an
 * error, a deployment without a subscription says so, the owner gets the
 * power state, and a start reports why it did not happen.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import {
  fetchDevBoxStatus,
  isInTransit,
  isParked,
  isRunning,
  startDevBox,
} from "../api";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchDevBoxStatus", () => {
  it("reads the power state for the owner", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          respond(200, { configured: true, power: "running" }),
        ),
    );

    expect(await fetchDevBoxStatus()).toEqual({
      status: "owner",
      power: "running",
    });
  });

  it("treats a 401 and a 403 as a visitor", async () => {
    for (const status of [401, 403]) {
      vi.stubGlobal("fetch", vi.fn().mockResolvedValue(respond(status)));
      expect(await fetchDevBoxStatus()).toEqual({ status: "visitor" });
    }
  });

  it("says when the deployment has no subscription", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(respond(200, { configured: false })),
    );

    expect(await fetchDevBoxStatus()).toEqual({ status: "unconfigured" });
  });

  it("is an error when Azure could not be asked, or nothing answered", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(respond(502)));
    expect(await fetchDevBoxStatus()).toEqual({ status: "error" });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
    expect(await fetchDevBoxStatus()).toEqual({ status: "error" });
  });
});

describe("startDevBox", () => {
  it("posts once and reports the outcome", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(respond(202, { power: "starting" }));
    vi.stubGlobal("fetch", fetchMock);

    expect(await startDevBox()).toEqual({ ok: true });
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/devbox/start");
    expect(options.method).toBe("POST");
  });

  it("names why a start was refused", async () => {
    const cases: Array<[number, string]> = [
      [401, "visitor"],
      [429, "throttled"],
      [502, "azure"],
    ];
    for (const [status, reason] of cases) {
      vi.stubGlobal("fetch", vi.fn().mockResolvedValue(respond(status)));
      expect(await startDevBox()).toEqual({ ok: false, reason });
    }

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
    expect(await startDevBox()).toEqual({ ok: false, reason: "network" });
  });
});

describe("power states", () => {
  it("knows which states are which", () => {
    expect(isRunning("running")).toBe(true);
    expect(isParked("deallocated")).toBe(true);
    expect(isParked("stopped")).toBe(true);
    expect(isInTransit("starting")).toBe(true);
    expect(isInTransit("deallocating")).toBe(true);
    expect(isParked("running")).toBe(false);
    expect(isInTransit("running")).toBe(false);
  });
});
