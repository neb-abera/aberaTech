import { expect, test } from "@playwright/test";
import { expectOneLineBar } from "./app-bar-lines";

// The visitor's app bar at desktop widths: every entry on one line. The
// owner's bar is held to the same in app-bar.owner.spec.ts.

test("a visitor's app bar keeps every entry on one line from 1024px up", async ({
  page,
}, testInfo) => {
  await page.goto("/guides");
  await expect(
    page.getByRole("banner").getByRole("link", { name: "Sign in" }),
  ).toBeVisible();
  await expectOneLineBar(page, testInfo, "visitor");
  await expect(
    page.getByRole("banner").getByRole("button", { name: "My pages" }),
  ).toHaveCount(0);
});
