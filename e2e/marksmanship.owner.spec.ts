import { expect, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /marksmanship as the owner, against the compose app and its Notion in
// memory (FakeNotion.cs, made-up content): reach it from the app bar, read
// the training log with its numbers, and read the plan below it.

const ready = 'table[aria-labelledby^="log-"]';

test("the owner reaches /marksmanship from the app bar", async ({ page }) => {
  await openAsOwner(page, "/marksmanship", ready);
  await page.goto("/guides");
  await page
    .getByRole("banner")
    .getByRole("link", { name: "Marksmanship", exact: true })
    .click();
  await expect(page).toHaveURL(/\/marksmanship$/);
  await expect(page.locator(ready)).toBeVisible();
});

test("the owner reads the training log and the plan", async ({ page }) => {
  await openAsOwner(page, "/marksmanship", ready);
  const log = page.getByRole("table", { name: "Pistol training log" });

  await expect(
    log.getByRole("columnheader", { name: "Slow fire" }),
  ).toBeVisible();
  const row = log.getByRole("row", { name: /Baseline President's course/ });
  await expect(row.getByRole("cell", { name: "2026-11-21" })).toBeVisible();
  await expect(
    row.getByRole("cell", { name: "150", exact: true }),
  ).toBeVisible();
  await expect(
    row.getByRole("cell", { name: "315", exact: true }),
  ).toBeVisible();
  await expect(row.getByText("Slow fire lost the most.")).toBeVisible();

  const plan = page.getByRole("article", { name: "Plan" });
  await expect(
    plan.getByRole("heading", { name: "President's Hundred pistol plan" }),
  ).toBeVisible();
  await expect(plan.getByText("The goal is 342 at Camp Perry.")).toBeVisible();

  await expect(
    plan.getByRole("link", {
      name: "Open President's Hundred pistol plan in Notion",
    }),
  ).toHaveAttribute(
    "href",
    "https://www.notion.so/0000a3c0000000000000000000000000",
  );
  await expect(
    page.getByRole("link", { name: "Open Pistol training log in Notion" }),
  ).toHaveAttribute("href", /^https:\/\/www\.notion\.so\/[0-9a-f]{32}$/);
});
