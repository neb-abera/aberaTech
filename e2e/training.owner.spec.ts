import { expect, type Page, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /training as the owner, against the compose app and the owner document
// in its database. The clock is
// fixed at Tue 10 Nov 2026, a programming day in Phase 0. Every change is
// undone before the test ends, since the three engines share one server.

const ready = '[aria-label="Overall progress"]';

async function open(page: Page, path: string) {
  await page.clock.setFixedTime(new Date("2026-11-10T10:00:00"));
  await openAsOwner(page, path, ready);
}

function saved(page: Page) {
  return page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === "/api/progress/training" &&
      response.request().method() === "PUT",
  );
}

test("the owner opens /training on Today", async ({ page }) => {
  await open(page, "/training");
  const today = page.getByRole("region", { name: "Today" });
  await expect(
    today.getByRole("heading", {
      name: /Tue 10 Nov 2026 · Phase 0 · week 1 of 8/,
    }),
  ).toBeVisible();
  await expect(today.getByText("Programming: 1 h")).toBeVisible();
  await expect(today.getByRole("listitem").first()).toContainText(
    "C++ for contests: the language",
  );
});

test("the owner ticks an item and it survives a reload", async ({ page }) => {
  await open(page, "/training?lesson=cp-3");
  const box = page.getByRole("checkbox", {
    name: /Done: Introduction to Counting & Probability, chapter 3/,
  });

  let put = saved(page);
  await box.check();
  expect((await put).ok()).toBe(true);

  await page.reload();
  await expect(box).toBeChecked({ timeout: 15_000 });

  put = saved(page);
  await box.uncheck();
  expect((await put).ok()).toBe(true);
  await page.reload();
  await expect(box).not.toBeChecked({ timeout: 15_000 });
});
