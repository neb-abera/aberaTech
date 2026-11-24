import { expect, test } from "@playwright/test";
import { openAsOwner, openFromBar } from "./owner-session";

// /plan as the owner, against the compose app and its Notion in memory
// (FakeNotion.cs, made-up content): reach it from the app bar, see the
// long-term plan's slide drawn in the page, follow the link to Network,
// and find the document edited here under it.

const ready = 'article[aria-label="Plan"]';

test("the owner reaches the career plan from the app bar", async ({ page }) => {
  await openAsOwner(page, "/plan", ready);
  await page.goto("/guides");
  await openFromBar(page, "Career plan");
  await expect(page).toHaveURL(/\/plan$/);
  await expect(page.locator(ready)).toBeVisible();
});

test("the owner sees the slide drawn, the goals, and the document edited here", async ({
  page,
}) => {
  await openAsOwner(page, "/plan", ready);
  const plan = page.getByRole("article", { name: "Plan" });

  await expect(
    plan.getByRole("heading", { name: "Career plan 2026 to 2028" }),
  ).toBeVisible();
  const slide = plan.getByRole("img", { name: "long-term-plan.png" });
  await expect(slide).toBeVisible();
  // The image came through this site and decoded.
  await expect
    .poll(() =>
      slide.evaluate((img: HTMLImageElement) =>
        img.complete ? img.naturalWidth : 0,
      ),
    )
    .toBeGreaterThan(0);
  await expect(slide).toHaveAttribute(
    "src",
    /^\/api\/career-plan\/files\/[0-9a-f-]{36}$/,
  );
  // The slide fills the column and never spills past it.
  const width = await plan.evaluate((el) => el.getBoundingClientRect().width);
  const drawn = await slide.evaluate((el) => el.getBoundingClientRect().width);
  expect(drawn).toBeGreaterThan(width * 0.9);
  expect(drawn).toBeLessThanOrEqual(width + 1);

  await expect(plan.getByText("Finish the common core by 2028.")).toBeVisible();
  await expect(
    plan.getByRole("link", { name: "Download long-term-plan.pptx" }),
  ).toHaveAttribute("href", /^\/api\/career-plan\/files\/[0-9a-f-]{36}$/);
  await expect(
    plan.getByRole("link", { name: "Open Career plan 2026 to 2028 in Notion" }),
  ).toHaveAttribute(
    "href",
    "https://www.notion.so/0000ca2e000000000000000000000000",
  );

  await expect(
    page.getByRole("heading", { name: "Edited on this site" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit" })).toBeVisible();

  await page.getByRole("link", { name: "Network", exact: true }).click();
  await expect(page).toHaveURL(/\/network$/);
});
