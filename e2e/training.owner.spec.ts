import { expect, type Page, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /training as the owner, against the compose app: the owner document in
// its database and the judges in memory (FakeJudges.cs, which says
// Nebikinizer solved Codeforces 2263B and was wrong on 2254B). The clock is
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

test("the owner reaches /training from the app bar and sees Today", async ({
  page,
}) => {
  await open(page, "/training");
  await page.goto("/guides");
  await page
    .getByRole("banner")
    .getByRole("link", { name: "Training", exact: true })
    .click();
  await expect(page).toHaveURL(/\/training$/);
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

test("the owner ticks an item and writes a note, and both survive a reload", async ({
  page,
}) => {
  await open(page, "/training?lesson=cp-3");
  const box = page.getByRole("checkbox", {
    name: /Done: Introduction to Counting & Probability, chapter 3/,
  });
  const note = page.getByRole("textbox", { name: "Lesson notes" });
  const text = `e2e ${test.info().project.name}`;

  let put = saved(page);
  await box.check();
  expect((await put).ok()).toBe(true);
  put = saved(page);
  await note.fill(text);
  expect((await put).ok()).toBe(true);

  await page.reload();
  await expect(box).toBeChecked({ timeout: 15_000 });
  await expect(note).toHaveValue(text);

  put = saved(page);
  await box.uncheck();
  await note.fill("");
  expect((await put).ok()).toBe(true);
  await page.reload();
  await expect(box).not.toBeChecked({ timeout: 15_000 });
  await expect(note).toHaveValue("");
});

test("a problem Codeforces reports solved shows solved, and an unsolved one stays open", async ({
  page,
}) => {
  await open(page, "/training?lesson=band-900-1");
  const solved = page.getByRole("article", { name: /Codeforces 2263B/ });
  await expect(solved.getByTestId("item-status")).toContainText(
    "(Codeforces API)",
    { timeout: 15_000 },
  );
  await expect(solved.getByRole("checkbox")).toHaveCount(0);
  const open2254 = page.getByRole("article", { name: /Codeforces 2254B/ });
  await expect(open2254.getByTestId("item-status")).toContainText(
    "counts when the Codeforces API sees it solved",
  );
  await expect(open2254.getByRole("checkbox")).toHaveCount(0);
});
