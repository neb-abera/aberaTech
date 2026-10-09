import { expect, test } from "@playwright/test";

// /playbook as a visitor, in every engine: the page is the owner's, so a
// visitor gets the sign-in button, the tree request is refused, and no
// page or file is ever asked for.

test("a visitor is asked to sign in and no playbook page is requested", async ({
  page,
}) => {
  const asked: string[] = [];
  page.on("request", (request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith("/api/playbook/")) asked.push(path);
  });
  const tree = page.waitForResponse(
    (response) => new URL(response.url()).pathname === "/api/playbook",
  );
  await page.goto("/playbook");
  expect((await tree).status()).toBe(401);
  await expect(
    page.getByRole("heading", { level: 1, name: "Playbook" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Sign in with Google" }),
  ).toBeVisible();
  expect(asked).toEqual([]);
});

test("a page or file asked for directly is refused without a session", async ({
  request,
}) => {
  const root = "00000001-0000-0000-0000-000000000000";
  for (const path of [
    `/api/playbook/pages/${root}`,
    `/api/playbook/files/${root}`,
  ]) {
    expect((await request.get(path)).status(), path).toBe(401);
  }
});
