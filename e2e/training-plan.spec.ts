import { expect, test } from "@playwright/test";

// /training-plan as a visitor, in every engine: the plan is the owner's,
// so a visitor gets the sign-in button, the plan request is refused, and
// no file is ever asked for.

test("a visitor is asked to sign in and no plan file is requested", async ({
  page,
}) => {
  const asked: string[] = [];
  page.on("request", (request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith("/api/training-plan/")) asked.push(path);
  });
  const plan = page.waitForResponse(
    (response) => new URL(response.url()).pathname === "/api/training-plan",
  );
  await page.goto("/training-plan");
  expect((await plan).status()).toBe(401);
  await expect(
    page.getByRole("heading", { level: 1, name: "Fitness plan" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Sign in with Google" }),
  ).toBeVisible();
  expect(asked).toEqual([]);
});

test("a plan file asked for directly is refused without a session", async ({
  request,
}) => {
  const plan = "0000e70e-0000-0000-0000-000000000000";
  for (const path of [
    `/api/training-plan/files/${plan}`,
    `/api/training-plan/files/${plan}:0`,
  ]) {
    expect((await request.get(path)).status(), path).toBe(401);
  }
});
