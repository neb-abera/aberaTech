import { expect, test } from "@playwright/test";
import { expectOneLineBar } from "./app-bar-lines";
import { openAsOwner } from "./owner-session";

// The signed-in owner's app bar at desktop widths. On 11 Nov 2026, at
// 1300px, "Dev box", "Training plan" and "Schedule time with me" each broke
// across two lines, with ten owner pages laid out flat in the bar.

// The playbook reads the in-memory Notion and writes nothing, so signing in
// there leaves the state the other owner specs hold alone.
const ready = 'nav[aria-label="Playbook pages"]';

test("the owner's app bar keeps every entry on one line from 1024px up", async ({
  page,
}, testInfo) => {
  await openAsOwner(page, "/playbook", ready);
  await expectOneLineBar(page, testInfo, "owner");
});

test("the owner's menu works from the keyboard and names itself to a screen reader", async ({
  page,
}) => {
  await openAsOwner(page, "/playbook", ready);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/guides");
  const button = page
    .getByRole("banner")
    .getByRole("button", { name: "My pages" });
  await expect(button).toHaveAttribute("aria-haspopup", "menu");
  await expect(button).toHaveAttribute("aria-expanded", "false");

  await button.focus();
  await page.keyboard.press("Enter");
  const menu = page.getByRole("menu", { name: "My pages" });
  await expect(menu).toBeVisible();
  // An open menu is modal: MUI hides the rest of the page from the
  // accessibility tree, the button with it, so read the button by its id.
  const byId = page.locator("#owner-menu-button");
  await expect(byId).toHaveAttribute("aria-expanded", "true");
  await expect(menu.getByRole("menuitem").first()).toBeFocused();
  await page.keyboard.press("ArrowDown");
  await expect(menu.getByRole("menuitem").nth(1)).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(menu).toBeHidden();
  // Gone from the page once its closing transition ends, which is when focus
  // goes back to the button.
  await expect(page.locator("#owner-menu")).toHaveCount(0);
  await expect(button).toBeFocused();
  await expect(button).toHaveAttribute("aria-expanded", "false");

  await page.keyboard.press("Enter");
  await expect(menu.getByRole("menuitem").first()).toBeFocused();
  await page.keyboard.press("End");
  await expect(
    menu.getByRole("menuitem", { name: "Training", exact: true }),
  ).toBeFocused();
  await page.keyboard.press("ArrowUp");
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/training-plan$/);
  await expect(
    page.getByRole("heading", { level: 1, name: "Fitness plan" }),
  ).toBeVisible();

  // The page you are on is marked in the menu.
  await button.click();
  await expect(byId).toHaveAttribute("aria-expanded", "true");
  await expect(
    menu.getByRole("menuitem", { name: "Fitness plan", exact: true }),
  ).toHaveAttribute("aria-current", "page");
});
