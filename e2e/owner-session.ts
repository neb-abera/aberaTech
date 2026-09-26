import { expect, type Page } from "@playwright/test";

/**
 * Open an owner page signed in, by pressing its own "Sign in with Google"
 * button. Under `make e2e` that button reaches the Development sign-in
 * (compose.yaml, Admin__DevelopmentSignIn), which issues the owner's cookie
 * without Google. `ready` is a locator that only the owner's view shows.
 */
export async function openAsOwner(page: Page, path: string, ready: string) {
  await page.goto(path);
  const button = page.getByRole("link", { name: "Sign in with Google" });
  await page.locator(ready).or(button).first().waitFor({ timeout: 15_000 });
  if (await button.isVisible()) {
    await button.click();
    await page.waitForURL(`**${path}`);
  }
  await expect(page.locator(ready).first()).toBeVisible({ timeout: 15_000 });
}
