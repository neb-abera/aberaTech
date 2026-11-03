import { expect, test } from "@playwright/test";

// /marksmanship as a visitor, in every engine: the page is the owner's, so
// a visitor gets the sign-in button and the plan request is refused.

test("a visitor is asked to sign in and the plan is refused", async ({
  page,
}) => {
  const plan = page.waitForResponse(
    (response) => new URL(response.url()).pathname === "/api/marksmanship",
  );
  await page.goto("/marksmanship");
  expect((await plan).status()).toBe(401);
  await expect(
    page.getByRole("heading", { level: 1, name: "Marksmanship" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Sign in with Google" }),
  ).toBeVisible();
  await expect(page.getByRole("table")).toHaveCount(0);
});
