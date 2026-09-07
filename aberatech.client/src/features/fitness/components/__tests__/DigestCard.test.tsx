// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import DigestCard from "../DigestCard";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("DigestCard", () => {
  it("shows the digest text the endpoint serves", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({
          date: "2026-09-07",
          weekStart: "2026-09-07",
          text: "abera.tech/fitness — week of 2026-09-07\n\nTRAINING\n  Endurance: 85 min this week so far.",
          lines: [],
        }),
        { headers: { "Content-Type": "application/json" } },
      ),
    );

    render(<DigestCard />);

    expect(await screen.findByText(/Endurance: 85 min this week/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Copy" })).toBeTruthy();
  });

  it("stands when the digest fails", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("boom"));

    render(<DigestCard />);

    expect(await screen.findByText(/did not answer/)).toBeTruthy();
  });
});
