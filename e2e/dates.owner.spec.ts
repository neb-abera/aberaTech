import { expect, type Page, test } from "@playwright/test";

// /dates as the owner, against the compose app and its database: sign in
// by the real button, add a countdown, watch it tick, edit it, delete it,
// and find each change in the status the phone reads.

test.describe.configure({ mode: "serial" });

async function signIn(page: Page) {
  await page.goto("/dates");
  const settled =
    'a:has-text("Sign in with Google"), form[aria-label="Add countdown"]';
  await page.locator(settled).first().waitFor({ timeout: 15_000 });
  const button = page.getByRole("link", { name: "Sign in with Google" });
  if (await button.count()) {
    await button.click();
    await page.waitForURL("**/dates");
  }
  await expect(page.getByRole("form", { name: "Add countdown" })).toBeVisible();
}

async function clear(page: Page) {
  const status = await (await page.request.get("/api/alerts/status")).json();
  for (const countdown of (status.countdowns ?? []) as { id: string }[])
    await page.request.delete(`/api/alerts/countdowns/${countdown.id}`);
}

async function phoneSees(page: Page) {
  const status = await (await page.request.get("/api/alerts/status")).json();
  return status.countdowns as {
    label: string;
    targetAt: string;
    timeZone: string;
  }[];
}

test("the owner adds a countdown, sees it tick, edits it and deletes it, and the phone's status follows", async ({
  page,
}) => {
  await signIn(page);
  await clear(page);
  await page.reload();

  const add = page.getByRole("form", { name: "Add countdown" });
  await add.getByLabel("Label").fill("Home");
  await add.getByLabel("Date").fill("2030-03-01");
  await add.getByLabel(/^Time\s*\*?$/).fill("09:00");
  await add.getByLabel("Time zone").fill("Asia/Amman");
  await add.getByRole("button", { name: "Add countdown" }).click();

  const item = page.getByRole("listitem", { name: "Home" });
  await expect(item).toContainText("2030");
  await expect(item).toContainText("(Asia/Amman)");
  const clock = item.getByTestId("countdown-clock");
  await expect(clock).toHaveText(/^\d[\d,]* days \d\d:\d\d:\d\d left$/);
  const first = await clock.textContent();
  await expect(clock).not.toHaveText(first ?? "", { timeout: 5_000 });

  expect(await phoneSees(page)).toEqual([
    expect.objectContaining({
      label: "Home",
      targetAt: expect.stringMatching(/^2030-03-01T06:00:00(\+00:00|Z)$/),
      timeZone: "Asia/Amman",
    }),
  ]);

  await page.reload();
  await page.getByRole("button", { name: "Edit Home" }).click();
  const edit = page.getByRole("form", { name: "Edit countdown" });
  await expect(edit.getByLabel("Date")).toHaveValue("2030-03-01");
  await expect(edit.getByLabel(/^Time\s*\*?$/)).toHaveValue("09:00");
  await edit.getByLabel("Label").fill("Leave");
  await edit.getByLabel("Date").fill("2020-01-10");
  await edit.getByRole("button", { name: "Save countdown" }).click();

  const passed = page.getByRole("listitem", { name: "Leave" });
  await expect(passed.getByTestId("countdown-clock")).toHaveText(/ ago$/);
  expect((await phoneSees(page)).map((c) => c.label)).toEqual(["Leave"]);

  await passed.getByRole("button", { name: "Delete Leave" }).click();
  await passed.getByRole("button", { name: "Yes, delete Leave" }).click();
  await expect(page.getByText("No countdowns.")).toBeVisible();
  expect(await phoneSees(page)).toEqual([]);
});
