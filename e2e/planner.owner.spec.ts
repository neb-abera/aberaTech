import { expect, test } from "@playwright/test";

// /planner as its owner uses it: pick a track, watch the board and the
// rules follow, and find the choice still there after a reload because it
// was saved to the account. Under `make e2e` the document routes answer as
// the owner without a cookie (compose.yaml, Fitness__DevelopmentOwner),
// so the plan saved here is the one plan on the server. The test puts the
// default track back before it ends.

const initial = "Signal processing and RF, balanced";
const other = "Receiver systems, in ten";

test("choosing a track rebuilds the plan and is still chosen after a reload", async ({
  page,
}) => {
  await page.goto("/planner");
  const saved = page.getByText("Saved to your account");
  await expect(saved).toBeVisible({ timeout: 15_000 });

  const track = (name: string) =>
    page.getByRole("button", { name: new RegExp(`^${name}`) });

  // Whatever an earlier run left, start from the default.
  await track(initial).click();

  const pill = (label: string) =>
    page
      .locator(".MuiPaper-root")
      .filter({ has: page.getByText(label, { exact: true }) })
      .locator("h6");
  const before = await pill("Courses planned").textContent();

  // The save is a beat behind the change. Wait for the request itself
  // rather than the caption, which reads "Saved" again before it is sent.
  const put = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/progress/planner") &&
      response.request().method() === "PUT",
  );
  await track(other).click();
  expect((await put).ok()).toBe(true);
  // The banner above the board names the track in play, with its trade-off.
  const trackBanner = page
    .locator(".MuiPaper-root")
    .filter({ hasText: "Trade-off." });
  await expect(trackBanner).toContainText(other);
  await expect(
    page.getByRole("heading", { name: "Degree rules" }),
  ).toBeVisible();
  expect(Number(await pill("Courses planned").textContent())).toBeGreaterThan(
    0,
  );

  await page.reload();
  await expect(saved).toBeVisible({ timeout: 15_000 });
  await expect(trackBanner).toContainText(other);

  const restored = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/progress/planner") &&
      response.request().method() === "PUT",
  );
  await track(initial).click();
  expect((await restored).ok()).toBe(true);
  await expect(trackBanner).toContainText(initial);
  expect(await pill("Courses planned").textContent()).toBe(before);
});
