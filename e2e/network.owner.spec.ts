import { expect, type Page, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /network as the owner, against the compose app and its database: sign in
// by the real button, paste a document, see it drawn and listed, select a
// node, narrow the picture, reload and find it kept. Whatever the document
// held before is put back at the end.

test.describe.configure({ mode: "serial" });

const ready = 'button:has-text("Edit data")';

const document = {
  version: 1,
  updated: "2026-10-02",
  sectors: [
    { id: "radio", label: "Radio" },
    { id: "capital", label: "Capital" },
  ],
  organizations: [
    {
      id: "acme",
      name: "Acme Radio",
      sector: "radio",
      kind: "company",
      url: "https://example.com/acme",
    },
    { id: "fund", name: "North Fund", sector: "capital", kind: "fund" },
  ],
  people: [
    {
      id: "ada",
      name: "Ada Example",
      role: "Chief Engineer, Acme Radio",
      sector: "radio",
      orgs: ["acme"],
      tier: 5,
      status: "pending",
      url: "https://example.com/ada",
      why: "Runs the team.",
      mutuals: 3,
    },
    {
      id: "bo",
      name: "Bo Example",
      role: "Partner, North Fund",
      sector: "capital",
      orgs: ["fund", "acme"],
      tier: 3,
      status: "connected",
    },
    {
      id: "cy",
      name: "Cy Example",
      role: "Founder",
      sector: "capital",
      orgs: [],
      tier: 2,
      status: "followed",
    },
  ],
};

async function current(page: Page): Promise<string | null> {
  const response = await page.request.get("/api/progress/network");
  return response.status() === 200 ? await response.text() : null;
}

test("the owner reaches /network from the app bar on any page, and the bar fits", async ({
  page,
}) => {
  await openAsOwner(page, "/network", ready);
  for (const [width, menu] of [
    [900, false],
    [1280, false],
    [390, true],
  ] as const) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto("/guides");
    if (menu) {
      await page.getByRole("button", { name: "Menu button" }).click();
      await page
        .getByRole("menuitem", { name: "Network", exact: true })
        .click();
    } else {
      const bar = page.getByRole("banner");
      await bar.getByRole("link", { name: "Network", exact: true }).click();
      await page.goto("/guides");
      const overflow = await page.evaluate(() => {
        const toolbar = document.querySelector("header .MuiToolbar-root");
        return toolbar ? toolbar.scrollWidth - toolbar.clientWidth : -1;
      });
      expect(overflow, `toolbar overflow at ${width}px`).toBe(0);
      await bar.getByRole("link", { name: "Network", exact: true }).click();
    }
    await expect(page).toHaveURL(/\/network$/);
    await expect(
      page.getByRole("heading", { level: 1, name: "Network" }),
    ).toBeVisible();
  }
});

test("the owner applies a document, sees it drawn, selects and narrows, and finds it kept after a reload", async ({
  page,
}) => {
  await openAsOwner(page, "/network", ready);
  const before = await current(page);
  try {
    await page.getByRole("button", { name: /Edit data|Close editor/ }).click();
    await page.getByLabel("Document JSON").fill(JSON.stringify(document));
    const saved = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/api/progress/network" &&
        response.request().method() === "PUT",
    );
    await page.getByRole("button", { name: "Apply" }).click();
    expect((await saved).ok()).toBe(true);

    const graph = page.getByTestId("network-graph");
    await expect(graph).toHaveAttribute(
      "aria-label",
      "Network map: 3 people and 2 organizations",
    );
    await expect(graph.getByRole("button")).toHaveCount(5);
    await expect(
      page.getByRole("table", { name: "People" }).getByRole("row"),
    ).toHaveCount(4);

    // The picture is drawn: every node has a position inside the box.
    const boxes = await graph.getByRole("button").evaluateAll((nodes) =>
      nodes.map((node) => {
        const rect = (node as SVGGElement).getBoundingClientRect();
        return { w: rect.width, h: rect.height };
      }),
    );
    for (const box of boxes) {
      expect(box.w).toBeGreaterThan(8);
      expect(box.h).toBeGreaterThan(8);
    }

    await graph.getByRole("button", { name: "Ada Example, pending" }).click();
    const detail = page.getByRole("complementary", { name: "Selected" });
    await expect(
      detail.getByRole("heading", { name: "Ada Example" }),
    ).toBeVisible();
    await expect(detail.getByText("Runs the team.")).toBeVisible();
    await expect(
      detail.getByRole("link", { name: "Open profile" }),
    ).toHaveAttribute("href", "https://example.com/ada");

    await page.getByRole("button", { name: "Radio", exact: true }).click();
    await expect(
      graph.getByRole("button", { name: "Ada Example, pending" }),
    ).toHaveAttribute("data-dimmed", "true");
    await expect(
      graph.getByRole("button", { name: "Bo Example, connected" }),
    ).not.toHaveAttribute("data-dimmed", "true");
    await expect(
      page.getByRole("table", { name: "People" }).getByRole("row"),
    ).toHaveCount(3);

    await page.reload();
    await expect(page.getByTestId("network-graph")).toHaveAttribute(
      "aria-label",
      "Network map: 3 people and 2 organizations",
    );
  } finally {
    await page.request.put("/api/progress/network", {
      data:
        before ??
        JSON.stringify({
          version: 1,
          updated: "",
          sectors: [],
          organizations: [],
          people: [],
        }),
      headers: { "Content-Type": "application/json" },
    });
  }
});
