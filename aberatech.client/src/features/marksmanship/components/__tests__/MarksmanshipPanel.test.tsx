// @vitest-environment jsdom
import { cleanup, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import MarksmanshipPanel from "../MarksmanshipPanel";

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
      <MarksmanshipPanel />
    </MemoryRouter>,
  );

describe("MarksmanshipPanel", () => {
  it("draws the log newest first and the plan's blocks", async () => {
    serve(200, {
      configured: true,
      page: {
        id: "p",
        title: "President's Hundred pistol plan",
        blocks: [
          {
            id: "1",
            type: "heading",
            level: 2,
            text: [{ text: "Bottom line" }],
          },
        ],
      },
      tables: [
        {
          id: "t",
          title: "Pistol training log",
          rows: [
            {
              id: "a",
              values: { Name: "Older", Date: "2026-11-20", Total: 300 },
            },
            {
              id: "b",
              values: { Name: "Newer", Date: "2026-12-01", Total: 315 },
            },
          ],
        },
      ],
    });
    draw();

    const log = await screen.findByRole("table", {
      name: "Pistol training log",
    });
    const rows = within(log).getAllByRole("row");
    expect(
      within(rows[0])
        .getAllByRole("columnheader")
        .map((c) => c.textContent),
    ).toEqual(["Date", "Name", "Total"]);
    expect(within(rows[1]).getByText("Newer")).toBeTruthy();
    expect(within(rows[2]).getByText("Older")).toBeTruthy();
    expect(screen.getByText("Bottom line")).toBeTruthy();
  });

  it("says when nothing is logged yet", async () => {
    serve(200, {
      configured: true,
      page: { id: "p", title: "Plan", blocks: [] },
      tables: [{ id: "t", title: "Pistol training log", rows: [] }],
    });
    draw();
    expect(await screen.findByText("No sessions logged yet.")).toBeTruthy();
  });

  it("says what to set when the page is not connected", async () => {
    serve(200, {
      configured: false,
      message: "Set Notion__MarksmanshipPlanPageId.",
    });
    draw();
    expect(
      await screen.findByText("Set Notion__MarksmanshipPlanPageId."),
    ).toBeTruthy();
  });

  it("asks a visitor to sign in", async () => {
    serve(401, {});
    draw();
    expect(
      await screen.findByText("This page is the owner's. Sign in to see it."),
    ).toBeTruthy();
  });

  it("says Notion is busy on a 503", async () => {
    serve(503, {});
    draw();
    expect(
      await screen.findByText("Notion is busy. Reload in a minute."),
    ).toBeTruthy();
  });
});
