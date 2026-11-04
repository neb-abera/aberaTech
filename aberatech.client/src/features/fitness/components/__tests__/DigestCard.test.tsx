// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import DigestCard from "../DigestCard";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("DigestCard", () => {
  it("starts closed and shows the digest text when opened", async () => {
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

    const copy = screen.getByRole("button", { name: "Copy" });
    await waitFor(() =>
      expect((copy as HTMLButtonElement).disabled).toBe(false),
    );
    expect(screen.queryByText(/Endurance: 85 min this week/)).toBeNull();

    const show = screen.getByRole("button", { name: "Show" });
    expect(show.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(show);
    expect(screen.getByText(/Endurance: 85 min this week/)).toBeTruthy();
    expect(
      screen
        .getByRole("button", { name: "Hide" })
        .getAttribute("aria-expanded"),
    ).toBe("true");
  });

  it("stands when the digest fails", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("boom"));

    render(<DigestCard />);

    expect(await screen.findByText(/did not answer/)).toBeTruthy();
  });
});
