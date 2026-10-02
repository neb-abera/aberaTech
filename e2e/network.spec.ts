import { expect, test } from "@playwright/test";

// /network as a visitor, in every engine: the page is the owner's, so a
// visitor gets the sign-in button and the document is never requested.

test("a visitor is asked to sign in and the network is never requested", async ({
  page,
}) => {
  const asked: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/progress"))
      asked.push(request.url());
  });
  await page.goto("/network");
  await expect(
    page.getByRole("heading", { level: 1, name: "Network" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Sign in with Google" }),
  ).toBeVisible();
  expect(asked).toEqual([]);
});
