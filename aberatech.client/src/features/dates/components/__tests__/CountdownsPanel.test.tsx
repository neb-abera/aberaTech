// @vitest-environment jsdom
/**
 * Who sees the countdowns: a visitor's browser never asks for them, and
 * the owner gets the list from the alerts state.
 */

import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { bounds, settings } from "../../../../test/alertsFixtures";
import type { AlertsView } from "../../../alerts/core/api";
import CountdownsPanel from "../CountdownsPanel";

afterEach(() => cleanup());

const owner: AlertsView = {
  status: "owner",
  state: {
    timeZone: "UTC",
    pollMinutes: 5,
    defaultLeadMinutes: 10,
    settings,
    bounds,
    mutedUntil: null,
    lastFetchAt: null,
    lastFetchError: null,
    lastSuccessAt: null,
    lastSend: null,
    alerts: [],
    countdowns: [
      {
        id: "3d2c1b0a-9f8e-4d7c-8b6a-5f4e3d2c1b0a",
        label: "Home",
        targetAt: "2026-11-11T12:00:00+00:00",
        timeZone: "UTC",
        updatedAt: "2026-10-01T06:00:00+00:00",
      },
    ],
  },
};

async function mount(signedIn: boolean, view: AlertsView = owner) {
  const load = vi.fn(async () => view);
  await act(async () => {
    render(
      <CountdownsPanel
        load={load}
        signedIn={async () => signedIn}
        now={Date.parse("2026-10-01T06:47:27Z")}
      />,
    );
  });
  return load;
}

describe("CountdownsPanel", () => {
  it("gives a visitor the sign-in button and never asks for the countdowns", async () => {
    const load = await mount(false);

    expect(
      screen.getByRole("link", { name: "Sign in with Google" }),
    ).toBeTruthy();
    expect(
      screen.getByText("Countdowns are the owner's. Sign in to see them."),
    ).toBeTruthy();
    expect(load).not.toHaveBeenCalled();
  });

  it("shows the owner's countdowns from the alerts state", async () => {
    await mount(true);

    expect(screen.getByText("Home")).toBeTruthy();
    expect(screen.getByText("41 days 05:12:33 left")).toBeTruthy();
  });

  it("says when the alerts are not set up, and when the server does not answer", async () => {
    await mount(true, {
      status: "unconfigured",
      missing: ["Alerts__CalendarIcsUrl"],
    });
    expect(screen.getByText(/has not\s+set up/)).toBeTruthy();
    cleanup();

    await mount(true, { status: "error" });
    expect(screen.getByText("The server did not answer.")).toBeTruthy();
  });
});
