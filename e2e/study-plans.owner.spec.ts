import { expect, type Page, test } from "@playwright/test";

// The three study plans as their owner works them: tick a task and see the
// count move, reload and find it kept, untick it; log a gate attempt and
// remove it. Under `make e2e` the document routes answer as the owner
// without a cookie (compose.yaml, Fitness__DevelopmentOwner). Every change
// made here is undone before the test ends.

const plans = ["/rf-training", "/signal-processing", "/quantum-cryptography"];

function saved(page: Page, path: string) {
  return page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname.startsWith("/api/progress/") &&
      response.request().method() === "PUT" &&
      page.url().endsWith(path),
  );
}

async function doneCount(page: Page) {
  const summary = page.getByText(/^\d+ of \d+ done/);
  await expect(summary).toBeVisible({ timeout: 15_000 });
  const [done, total] = ((await summary.textContent()) ?? "")
    .match(/\d+/g)
    ?.map(Number) ?? [NaN, NaN];
  return { done, total };
}

for (const path of plans) {
  test(`${path}: a ticked task is counted and kept`, async ({ page }) => {
    await page.goto(path);
    const start = await doneCount(page);
    expect(start.total).toBeGreaterThan(0);

    const task = page
      .locator(".MuiFormControlLabel-root")
      .filter({ has: page.locator("input[type=checkbox]:not(:checked)") })
      .first();
    const text = (await task.textContent()) ?? "";
    const box = page.getByRole("checkbox", { name: text, exact: true });

    let put = saved(page, path);
    await box.check();
    expect((await put).ok()).toBe(true);
    expect((await doneCount(page)).done).toBe(start.done + 1);

    await page.reload();
    expect((await doneCount(page)).done).toBe(start.done + 1);
    await expect(box).toBeChecked();

    put = saved(page, path);
    await box.uncheck();
    expect((await put).ok()).toBe(true);
    expect((await doneCount(page)).done).toBe(start.done);
  });
}

test("/rf-training: a gate attempt is logged, summarised and removed", async ({
  page,
}) => {
  await page.goto("/rf-training");
  await doneCount(page);
  const gate = page.getByRole("region", { name: /^Gate for / }).first();
  await expect(gate.getByRole("button", { name: "Log a pass" })).toBeVisible();

  const note = `e2e ${test.info().project.name}`;
  await gate.getByLabel("Minutes").fill("42");
  await gate.getByLabel("Note").fill(note);
  let put = saved(page, "/rf-training");
  await gate.getByRole("button", { name: "Log a pass" }).click();
  expect((await put).ok()).toBe(true);

  const attempt = gate.getByRole("listitem").filter({ hasText: note });
  await expect(attempt).toContainText("pass, 42 min");
  await expect(gate).toContainText(/Latest: pass on \d{4}-\d{2}-\d{2}\./);

  await page.reload();
  await expect(attempt).toBeVisible({ timeout: 15_000 });

  put = saved(page, "/rf-training");
  await attempt.getByRole("button", { name: /^Remove attempt on / }).click();
  expect((await put).ok()).toBe(true);
  await expect(attempt).toHaveCount(0);
});
