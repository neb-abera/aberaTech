import { readFile } from "node:fs/promises";
import { expect, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /training-plan as the owner, against the compose app and its Notion in
// memory (FakeNotion.cs, a made-up two-week plan): reach it from the app
// bar, move between weeks, open a workout and a note, and download a note's
// attachment and the plan's file with their own names.

const ready = 'nav[aria-label="Weeks"]';

test("the owner reaches /training-plan from the app bar", async ({ page }) => {
  await openAsOwner(page, "/training-plan", ready);
  await page.goto("/guides");
  await page
    .getByRole("banner")
    .getByRole("link", { name: "Training plan", exact: true })
    .click();
  await expect(page).toHaveURL(/\/training-plan$/);
  await expect(page.locator(ready)).toBeVisible();
});

test("the owner reads a week, moves to the next, and downloads files", async ({
  page,
}) => {
  await openAsOwner(page, "/training-plan", ready);
  await page.goto("/training-plan?week=1");
  const week = page.getByRole("region", { name: "Week 1" });

  await expect(
    week.getByRole("heading", { name: "Week 1 of 2" }),
  ).toBeVisible();
  await expect(week.getByText("Block 1. Block 1 Week 1 Notes")).toBeVisible();
  // 5 + 45 + 60 minutes, 33 TSS, by sport largest first.
  await expect(
    week.getByText(
      "1 h 50 min, 33 TSS. Strength 1 h, Run 45 min, Other 5 min.",
    ),
  ).toBeVisible();
  const monday = week.getByRole("region", { name: "Mon 2 Nov 2026" });
  await expect(monday.getByText("Aerobic base run")).toBeVisible();

  await monday.getByText("Aerobic base run").click();
  await expect(monday.getByText("Nose breathe the whole run.")).toBeVisible();
  await expect(
    monday.getByRole("link", { name: "https://example.com/maf" }),
  ).toHaveAttribute("target", "_blank");

  await monday.getByText("Intro to block one").click();
  const [intro] = await Promise.all([
    page.waitForEvent("download"),
    monday.getByRole("link", { name: "Download intro.pdf" }).click(),
  ]);
  expect(intro.suggestedFilename()).toBe("intro.pdf");
  expect(await readFile(await intro.path(), "utf8")).toContain("%intro");

  await page.getByRole("button", { name: "Next week" }).click();
  await expect(page).toHaveURL(/\/training-plan\?week=2$/);
  const second = page.getByRole("region", { name: "Week 2" });
  await second.getByText("Heart rate drift test").click();
  await expect(second.getByText("Active: 1:00:00 at 3-4 RPE")).toBeVisible();
  await expect(page.getByRole("button", { name: "Next week" })).toBeDisabled();

  await page.reload();
  await expect(page.getByRole("region", { name: "Week 2" })).toBeVisible();
  await page.getByRole("button", { name: "Week 1", exact: true }).click();
  await expect(page.getByRole("region", { name: "Week 1" })).toBeVisible();

  const about = page.getByRole("region", { name: "About the plan" });
  const [guide] = await Promise.all([
    page.waitForEvent("download"),
    about.getByRole("link", { name: "Download plan-guide.pdf" }).click(),
  ]);
  expect(guide.suggestedFilename()).toBe("plan-guide.pdf");
  await about.getByText("About the plan", { exact: true }).last().click();
  await expect(
    about.getByText("Two made-up weeks of base training."),
  ).toBeVisible();
});

test("the owner opens a page the plan links and comes back", async ({
  page,
}) => {
  await openAsOwner(page, "/training-plan", ready);
  const related = page.getByRole("navigation", { name: "Related pages" });
  await related.getByRole("link", { name: "Sample guide" }).click();
  await expect(page).toHaveURL(/\/training-plan\?page=/);
  const article = page.getByRole("article", { name: "Linked page" });
  await expect(
    article.getByRole("heading", { level: 2, name: "Sample guide" }),
  ).toBeVisible();
  await expect(
    article.getByText("Start with four easy runs a week."),
  ).toBeVisible();

  await page.reload();
  await expect(
    article.getByText("Start with four easy runs a week."),
  ).toBeVisible();

  await article.getByRole("link", { name: "Back to the plan" }).click();
  await expect(page.locator(ready)).toBeVisible();
  await page
    .getByRole("navigation", { name: "Related pages" })
    .getByRole("link", { name: "Sample analysis" })
    .click();
  await expect(
    page.getByText("Three build weeks, then a deload."),
  ).toBeVisible();
});
