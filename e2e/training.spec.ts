import { expect, test } from "@playwright/test";

// /training as a visitor, in every engine: the syllabus is public, the
// owner's progress is not. The page asks only whether anyone is signed in
// and never for the document.

test("a visitor reads the syllabus and is never sent the progress", async ({
  page,
}) => {
  const asked: string[] = [];
  page.on("request", (request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith("/api/progress") || path.startsWith("/api/training"))
      asked.push(path);
  });
  await page.goto("/training");
  await expect(
    page.getByRole("heading", { level: 1, name: "Training" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Sign in with Google" }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Phase 0: DEMOB, return, and placement" }),
  ).toBeVisible();
  await expect(page.getByLabel("Overall progress")).toHaveCount(0);
  await expect(page.getByRole("region", { name: "Pace" })).toHaveCount(0);
  await expect(page.getByRole("tab", { name: "Next up" })).toHaveCount(0);
  await expect(page.getByRole("tab", { name: /^Review/ })).toHaveCount(0);
  await expect(page.getByRole("tab", { name: "Notes" })).toHaveCount(0);
  await expect(page.getByRole("tab", { name: "Log" })).toHaveCount(0);
  await expect(page.getByRole("region", { name: "Problem pool" })).toHaveCount(
    0,
  );

  await page
    .getByRole("link", { name: "Counting & Probability: counting" })
    .click();
  await page
    .getByRole("link", { name: "Chapter 3: Correcting for Overcounting" })
    .click();
  await expect(page).toHaveURL(/\/training\?lesson=cp-3$/);
  await expect(
    page.getByText(/sections 3\.1 to 3\.5, pp\. 49 to 62/),
  ).toBeVisible();
  await expect(page.getByRole("checkbox")).toHaveCount(0);

  // An owner view asked for by address shows the map instead.
  for (const view of ["review", "notes", "today", "log"]) {
    await page.goto(`/training?view=${view}`);
    await expect(
      page.getByRole("region", {
        name: "Phase 0: DEMOB, return, and placement",
      }),
    ).toBeVisible();
    await expect(page.getByRole("region", { name: "Review" })).toHaveCount(0);
    await expect(page.getByRole("region", { name: "Notes" })).toHaveCount(0);
  }
  expect(asked).toEqual([]);
});
