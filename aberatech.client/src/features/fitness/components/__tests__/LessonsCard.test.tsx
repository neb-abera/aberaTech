// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import LessonsCard from "../LessonsCard";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function serve(body: unknown) {
  vi.spyOn(globalThis, "fetch").mockResolvedValue(
    new Response(JSON.stringify(body), {
      headers: { "Content-Type": "application/json" },
    }),
  );
}

describe("LessonsCard", () => {
  it("draws the Notion page's blocks, Running first", async () => {
    serve({
      configured: true,
      page: {
        id: "p",
        title: "Military athlete: fitness plan lessons learned",
        blocks: [
          { id: "1", type: "heading", level: 1, text: [{ text: "Running" }] },
          { id: "2", type: "paragraph", text: [{ text: "Set 1% incline." }] },
        ],
      },
    });
    render(<LessonsCard />);

    expect(await screen.findByText("Running")).toBeTruthy();
    expect(screen.getByText("Set 1% incline.")).toBeTruthy();
  });

  it("says what to set when the page is not connected", async () => {
    serve({ configured: false, message: "The lessons page is not connected." });
    render(<LessonsCard />);

    expect(
      await screen.findByText("The lessons page is not connected."),
    ).toBeTruthy();
  });
});
