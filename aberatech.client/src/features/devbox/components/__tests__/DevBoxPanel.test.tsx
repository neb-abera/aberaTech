// @vitest-environment jsdom
/**
 * The dev box page from two chairs. A visitor gets a sign-in button that
 * brings them back here. The owner sees the power state; on a parked box the
 * Start button posts once, the page reports "starting", keeps asking, and
 * says where to go once Azure answers "running". A refused start says why,
 * and a deployment without a subscription says so instead of offering a
 * button.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import DevBoxPanel from "../DevBoxPanel";

let fetchMock: ReturnType<typeof vi.fn>;

const status = (power: string, agent: unknown = null) =>
  respond(200, { configured: true, power, agent });

const reported = (over: Record<string, unknown> = {}) => ({
  seen: true,
  seenSecondsAgo: 20,
  remoteControl: "active",
  sessions: 2,
  load: 1.25,
  uptimeSeconds: 4200,
  holdUntil: null,
  environmentUrl: "https://claude.ai/code?environment=env_abc",
  pending: { holdMinutes: null, park: false },
  ...over,
});

const settle = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
};

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

function mount(...answers: ReturnType<typeof respond>[]) {
  fetchMock = vi.fn();
  for (const answer of answers) fetchMock.mockResolvedValueOnce(answer);
  fetchMock.mockResolvedValue(answers[answers.length - 1]);
  vi.stubGlobal("fetch", fetchMock);
  render(<DevBoxPanel />);
}

const posts = () =>
  fetchMock.mock.calls.filter(([, options]) => options?.method === "POST");

describe("a visitor", () => {
  it("is offered sign-in that comes back here, and nothing is started", async () => {
    mount(respond(401));
    await settle();

    const signIn = screen.getByRole("link", { name: "Sign in with Google" });
    expect(signIn.getAttribute("href")).toBe(
      "/api/scheduling/admin/sign-in?returnUrl=/devbox",
    );
    expect(screen.queryByRole("button", { name: "Start dev box" })).toBeNull();
    expect(posts()).toHaveLength(0);
  });
});

describe("a deployment without a subscription", () => {
  it("says so instead of offering a button", async () => {
    mount(respond(200, { configured: false, power: null }));
    await settle();

    expect(screen.getByText(/no dev box configured/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Start dev box" })).toBeNull();
  });
});

describe("the owner", () => {
  it("starts a parked box and is told where to go once it runs", async () => {
    mount(
      status("deallocated"),
      respond(202, { configured: true, power: "starting" }),
      status("starting"),
      status("running"),
    );
    await settle();

    expect(screen.getByLabelText("Power state: deallocated")).toBeTruthy();
    const start = screen.getByRole("button", { name: "Start dev box" });
    expect(start.hasAttribute("disabled")).toBe(false);

    fireEvent.click(start);
    await settle();

    expect(posts()).toHaveLength(1);
    expect(posts()[0][0]).toBe("/api/devbox/start");
    expect(screen.getByText(/Azure is starting the box/)).toBeTruthy();
    expect(
      screen
        .getByRole("button", { name: "Start dev box" })
        .hasAttribute("disabled"),
    ).toBe(true);

    // First poll: still starting.
    await act(async () => {
      vi.advanceTimersByTime(5000);
    });
    await settle();
    expect(screen.getByLabelText("Power state: starting")).toBeTruthy();

    // Second poll: running, and the page says where to pick it up.
    await act(async () => {
      vi.advanceTimersByTime(5000);
    });
    await settle();
    expect(screen.getByLabelText("Power state: running")).toBeTruthy();
    expect(screen.getByText(/It registers about a minute/)).toBeTruthy();
    expect(posts()).toHaveLength(1);
  });

  it("cannot press Start on a box that is already running", async () => {
    mount(status("running"));
    await settle();

    expect(
      screen
        .getByRole("button", { name: "Start dev box" })
        .hasAttribute("disabled"),
    ).toBe(true);
    expect(screen.getByText(/It registers about a minute/)).toBeTruthy();
  });

  it("is told why a start was refused, and may try again", async () => {
    mount(status("deallocated"), respond(429));
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Start dev box" }));
    await settle();

    expect(screen.getByText("Too many presses. Wait a minute.")).toBeTruthy();
    expect(
      screen
        .getByRole("button", { name: "Start dev box" })
        .hasAttribute("disabled"),
    ).toBe(false);
  });

  it("still sees the runbook when Azure does not answer", async () => {
    mount(respond(502));
    await settle();

    expect(screen.getByText(/Azure did not answer/)).toBeTruthy();
    expect(screen.getByText("If this page is down")).toBeTruthy();
  });

  it("sees the agent's report and the link into Claude", async () => {
    mount(status("running", reported()));
    await settle();

    expect(
      screen.getByText(/Remote Control is up with 2 sessions/),
    ).toBeTruthy();
    const open = screen.getByRole("link", { name: "Open devbox in Claude" });
    expect(open.getAttribute("href")).toBe(
      "https://claude.ai/code?environment=env_abc",
    );
    expect(screen.getByText(/Up 1 h 10 min, load 1.25/)).toBeTruthy();
  });

  it("is warned when the box has gone quiet", async () => {
    mount(status("running", reported({ seenSecondsAgo: 600 })));
    await settle();

    expect(screen.getByText(/has not reported for 10 minutes/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Hold 2 h" })).toBeNull();
  });

  it("queues a hold and a park for the box", async () => {
    // GET status, POST hold, the refresh GET, POST park, the refresh GET.
    mount(
      status("running", reported()),
      respond(202, {}),
      status("running", reported()),
      respond(202, {}),
      status("running", reported()),
    );
    await settle();
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));

    fireEvent.click(screen.getByRole("button", { name: "Hold 2 h" }));
    await settle();
    expect(posts()[0][0]).toBe("/api/devbox/hold");
    expect(JSON.parse(String(posts()[0][1].body))).toEqual({ minutes: 120 });
    expect(screen.getByText(/Hold for 2 hours queued/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Park now" }));
    await settle();
    expect(posts()[1][0]).toBe("/api/devbox/park");
    expect(screen.getByText(/Park queued/)).toBeTruthy();
  });

  it("does not park without a yes", async () => {
    mount(status("running", reported()));
    await settle();
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(false));

    fireEvent.click(screen.getByRole("button", { name: "Park now" }));
    await settle();

    expect(posts()).toHaveLength(0);
  });
});
