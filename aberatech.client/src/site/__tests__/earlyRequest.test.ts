/**
 * The first API request of a page, started from its head and handed to the
 * page's own fetch once.
 */
import { afterEach, describe, expect, it, vi } from "vitest";
import { earlyRequestScript, requestJson } from "../earlyRequest";

const init = {
  credentials: "same-origin",
  headers: { Accept: "application/json" },
};

afterEach(() => {
  globalThis.__earlyRequest = undefined;
  vi.unstubAllGlobals();
});

describe("earlyRequestScript", () => {
  it("starts the request with the options the page's fetch uses", () => {
    const fetch = vi.fn(() => Promise.resolve(new Response("{}")));
    const scope: { __earlyRequest?: unknown } = {};

    new Function("window", "fetch", earlyRequestScript("/api/progress/links"))(
      scope,
      fetch,
    );

    expect(fetch).toHaveBeenCalledWith("/api/progress/links", init);
    expect(scope.__earlyRequest).toMatchObject({ url: "/api/progress/links" });
  });

  it("does not report a failed early request as unhandled", async () => {
    // Nothing awaits the promise until the bundle runs. A rejection before
    // then would be logged as uncaught without the catch the script adds.
    const failure = Promise.reject(new TypeError("offline"));
    const catchSpy = vi.spyOn(failure, "catch");
    const scope: { __earlyRequest?: unknown } = {};

    new Function("window", "fetch", earlyRequestScript("/x"))(
      scope,
      () => failure,
    );

    expect(catchSpy).toHaveBeenCalled();
    await expect(failure).rejects.toThrow("offline");
  });

  it("adds the viewer's zone the way the schedule page writes it", () => {
    // useSchedule asks /api/scheduling/state?zone=… through URLSearchParams.
    // The head script must build the same address, or the page asks twice.
    for (const zone of [
      "America/New_York",
      "Asia/Amman",
      "America/Argentina/Buenos_Aires",
      "Etc/GMT+5",
      "America/Port-au-Prince",
    ]) {
      const fetch = vi.fn(() => Promise.resolve(new Response("{}")));
      const scope: { __earlyRequest?: { url: string } } = {};
      const fakeIntl = {
        DateTimeFormat: () => ({ resolvedOptions: () => ({ timeZone: zone }) }),
      };

      new Function(
        "window",
        "fetch",
        "Intl",
        earlyRequestScript("/api/scheduling/state", { zone: true }),
      )(scope, fetch, fakeIntl);

      const expected = `/api/scheduling/state?${new URLSearchParams({ zone })}`;
      expect(scope.__earlyRequest?.url).toBe(expected);
      expect(fetch).toHaveBeenCalledWith(expected, init);
    }
  });

  it("cannot close its own script element", () => {
    expect(earlyRequestScript("/a</script><b>")).not.toContain("</script>");
  });
});

describe("requestJson", () => {
  it("takes the head's request once, then asks the network", async () => {
    const early = Promise.resolve(new Response("early"));
    globalThis.__earlyRequest = { url: "/api/progress/links", response: early };
    const fetch = vi.fn(() => Promise.resolve(new Response("late")));
    vi.stubGlobal("fetch", fetch);

    expect(await (await requestJson("/api/progress/links")).text()).toBe(
      "early",
    );
    expect(fetch).not.toHaveBeenCalled();

    // A second load (the page shown again) is a new question.
    await requestJson("/api/progress/links");
    expect(fetch).toHaveBeenCalledWith("/api/progress/links", init);
  });

  it("passes a signal on to the network", async () => {
    const fetch = vi.fn(() => Promise.resolve(new Response("late")));
    vi.stubGlobal("fetch", fetch);
    const { signal } = new AbortController();

    await requestJson("/api/scheduling/state?zone=UTC", signal);

    expect(fetch).toHaveBeenCalledWith("/api/scheduling/state?zone=UTC", {
      ...init,
      signal,
    });
  });

  it("leaves a head request for another address alone", async () => {
    const early = Promise.resolve(new Response("early"));
    globalThis.__earlyRequest = { url: "/api/devbox/status", response: early };
    const fetch = vi.fn(() => Promise.resolve(new Response("late")));
    vi.stubGlobal("fetch", fetch);

    await requestJson("/api/progress/links");

    expect(fetch).toHaveBeenCalledWith("/api/progress/links", init);
    expect(globalThis.__earlyRequest?.url).toBe("/api/devbox/status");
  });
});
