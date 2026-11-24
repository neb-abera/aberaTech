// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import LongTermPlan from "../LongTermPlan";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function serve(status: number, body: unknown) {
  vi.spyOn(globalThis, "fetch").mockResolvedValue(
    new Response(JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    }),
  );
}

const draw = () =>
  render(
    <MemoryRouter>
      <LongTermPlan />
    </MemoryRouter>,
  );

const page = {
  id: "0000ca2e-0000-0000-0000-000000000000",
  title: "Career plan 2026 to 2028",
  blocks: [
    { type: "heading", level: 2, text: [{ text: "Long-term plan" }] },
    {
      type: "file",
      id: "slide-1",
      kind: "image",
      name: "slide-1.png",
      caption: [{ text: "Career plan, 2025 to 2031" }],
    },
    { type: "file", id: "slide-2", kind: "image", name: "slide-2.png" },
    { type: "file", id: "deck", kind: "file", name: "plan.pptx" },
  ],
};

describe("LongTermPlan", () => {
  it("draws each slide in place, from this site, and links the page in Notion", async () => {
    serve(200, { configured: true, page });
    draw();

    const first = await screen.findByRole("img", {
      name: "Career plan, 2025 to 2031",
    });
    expect(first.getAttribute("src")).toBe("/api/career-plan/files/slide-1");
    expect(first.closest("a")?.getAttribute("href")).toBe(
      "/api/career-plan/files/slide-1",
    );
    // No caption: the file's name is what a screen reader hears.
    expect(
      screen.getByRole("img", { name: "slide-2.png" }).getAttribute("src"),
    ).toBe("/api/career-plan/files/slide-2");
    expect(screen.getAllByRole("img")).toHaveLength(2);

    expect(
      screen
        .getByRole("link", { name: "Download plan.pptx" })
        .getAttribute("href"),
    ).toBe("/api/career-plan/files/deck");
    expect(
      screen.getByRole("heading", { name: "Career plan 2026 to 2028" }),
    ).toBeTruthy();
    expect(
      screen
        .getByRole("link", { name: "Open Career plan 2026 to 2028 in Notion" })
        .getAttribute("href"),
    ).toBe("https://www.notion.so/0000ca2e000000000000000000000000");
  });

  it("asks a visitor to sign in, and comes back here", async () => {
    serve(401, {});
    draw();

    const button = await screen.findByRole("link", {
      name: /sign in with google/i,
    });
    expect(button.getAttribute("href")).toContain("returnUrl=/plan");
    expect(screen.queryByRole("img")).toBeNull();
  });

  it("says what to set when the page is not connected", async () => {
    serve(200, { configured: false, message: "Set Notion__CareerPlanPageId." });
    draw();

    expect(
      await screen.findByText("Set Notion__CareerPlanPageId."),
    ).toBeTruthy();
    expect(screen.queryByRole("img")).toBeNull();
  });

  it("says Notion is busy on a 503 and did not answer on any other failure", async () => {
    serve(503, {});
    draw();
    expect(await screen.findByText(/Notion is busy/)).toBeTruthy();
    cleanup();
    vi.restoreAllMocks();

    serve(502, {});
    draw();
    expect(await screen.findByText(/Notion did not answer/)).toBeTruthy();
  });
});
