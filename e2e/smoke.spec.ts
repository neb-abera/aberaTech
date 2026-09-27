import { expect, test } from "@playwright/test";

// End-to-end smoke against the production image and its database, as `make
// up` runs them: the process is up, its dependencies are reachable, the
// client is served, and the API behind it answers.

test("liveness and readiness both answer", async ({ request }) => {
  const health = await request.get("/healthz");
  expect(health.status()).toBe(200);
  expect(await health.text()).toBe("ok");

  // Readiness opens a connection to each configured database. Under `make
  // up` both are configured, so a 503 here means the app came up without
  // its Postgres and every page depending on it is broken.
  const ready = await request.get("/readyz");
  expect(ready.status()).toBe(200);
});

test("security headers are served to browsers", async ({ request }) => {
  const response = await request.get("/");
  const headers = response.headers();

  expect(headers["content-security-policy"]).toContain("default-src 'self'");
  expect(headers["content-security-policy"]).toContain("object-src 'none'");
  expect(headers["x-content-type-options"]).toBe("nosniff");
  expect(headers["referrer-policy"]).toBe("strict-origin-when-cross-origin");
  expect(headers["cross-origin-opener-policy"]).toBe("same-origin");
  expect(headers["permissions-policy"]).toContain("camera=()");
});

test("serves the home page, with the API wired behind it", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { level: 1 })).toContainText("Abera");

  // The one public API the site cannot do without: the booking page's state.
  const state = await page.request.get("/api/scheduling/state");
  expect(state.ok()).toBeTruthy();
  expect(["slots", "queue"]).toContain((await state.json()).mode);
});

test("an app page that is not prerendered still boots", async ({ page }) => {
  // /schedule shows live queue state, so it is served as the empty shell and
  // rendered in the browser; the shell must still carry the app.
  await page.goto("/schedule");

  await expect(
    page.getByRole("heading", { level: 1, name: "Schedule time with me" }),
  ).toBeVisible();
});

test("an address the site does not have is a 404 with a page", async ({
  page,
}) => {
  const response = await page.goto("/definitely-not-a-page");

  expect(response?.status()).toBe(404);
  await expect(
    page.getByRole("heading", { level: 1, name: "No page at that address" }),
  ).toBeVisible();
});

test("the stock Vite logo is not served", async ({ request }) => {
  // create-vite ships public/vite.svg and nothing here uses it. The favicon
  // beside it is the control: a static file that ships answers 200.
  expect((await request.get("/favicon.svg")).status()).toBe(200);
  expect((await request.get("/vite.svg")).status()).toBe(404);
});

test("the favicon is transparent and follows the browser theme", async ({
  request,
}) => {
  // A background shape hides the tab behind the icon. A single fill vanishes
  // on one of the two tab strips.
  const svg = await (await request.get("/favicon.svg")).text();
  expect(svg).not.toContain("<rect");
  expect(svg).toContain("prefers-color-scheme: dark");
  expect((await request.get("/favicon-96-dark.png")).status()).toBe(200);
});

test("the home screen icon and /favicon.ico are served", async ({
  page,
  request,
}) => {
  await page.goto("/");
  await expect(page.locator('link[rel="apple-touch-icon"]')).toHaveAttribute(
    "href",
    "/apple-touch-icon.png",
  );
  const touch = await request.get("/apple-touch-icon.png");
  expect(touch.status()).toBe(200);
  expect(touch.headers()["content-type"]).toBe("image/png");
  const ico = await request.get("/favicon.ico");
  expect(ico.status()).toBe(200);
  expect(ico.headers()["content-type"]).toBe("image/x-icon");
});
