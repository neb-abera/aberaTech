import {
  type BrowserContextOptions,
  expect,
  type Page,
  type TestInfo,
  test,
} from "@playwright/test";

// /planner arrives as the board a first-time visitor sees, then hydrates.
// Until 2026-09-28 its HTML was an empty root: 1.3 KB, and nothing painted
// until 376 KB of script had run (FCP 6.6 s on a phone).

/** The project's device, for a context opened by hand. */
function device(testInfo: TestInfo): BrowserContextOptions {
  const {
    viewport,
    userAgent,
    deviceScaleFactor,
    isMobile,
    hasTouch,
    baseURL,
  } = testInfo.project.use;
  return {
    viewport,
    userAgent,
    deviceScaleFactor,
    isMobile,
    hasTouch,
    baseURL,
  };
}

/** What a visitor reads first: the title, the rules, the plan, the courses. */
async function expectBoard(page: Page) {
  await expect(
    page.getByRole("heading", { level: 1, name: "Graduate course planner" }),
  ).toBeVisible();
  await expect(page.getByText(/^Plan a Johns Hopkins/)).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Degree rules", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "The plan", exact: true }),
  ).toBeVisible();
  await expect(page.locator("[data-code]").first()).toBeVisible();
}

test("the board is readable before any JavaScript runs", async ({
  browser,
}, testInfo) => {
  const context = await browser.newContext({
    ...device(testInfo),
    javaScriptEnabled: false,
  });
  const page = await context.newPage();
  const response = await page.goto("/planner");

  expect(response?.status()).toBe(200);
  await expectBoard(page);
  await context.close();
});

/** Where the board's landmarks sit, and the first course's colour key. */
async function layout(page: Page) {
  const box = async (name: string) => {
    const b = await page
      .getByRole("heading", { name, exact: true })
      .boundingBox();
    if (!b) throw new Error(`${name} has no box`);
    return { x: Math.round(b.x), y: Math.round(b.y), w: Math.round(b.width) };
  };
  const key = page.locator("[data-code] > span[aria-hidden]").first();
  return {
    rules: await box("Degree rules"),
    plan: await box("The plan"),
    key: await key.evaluate((e) => getComputedStyle(e).backgroundColor),
  };
}

test("hydration keeps the prerendered board, with no error and no shift", async ({
  browser,
}, testInfo) => {
  // The same viewport with and without script. A board that rendered
  // differently on the server (a rail sized by a media query, a colour read
  // from the theme in script) moves when React takes over, or fails to
  // hydrate and is rebuilt.
  const still = await browser.newContext({
    ...device(testInfo),
    javaScriptEnabled: false,
  });
  const stillPage = await still.newPage();
  await stillPage.goto("/planner");
  await expectBoard(stillPage);
  const before = await layout(stillPage);
  await still.close();

  const live = await browser.newContext(device(testInfo));
  const page = await live.newPage();
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/planner");
  // Hydrated: the bar's account control appears once the account probe has
  // answered, which is after React owns the page. On a phone it sits in the
  // closed menu, so it is attached rather than visible.
  await expect(
    page
      .locator("header")
      .getByText(/^(Sign in|Sign out)$/)
      .first(),
  ).toBeAttached();
  await expectBoard(page);

  // React names a hydration failure in words in development and by error
  // number in production: 418 (content), 423 (recovered by a client render),
  // 425 (text).
  expect(
    errors.filter((text) => /hydrat|#41[89]|#42[1-5]/i.test(text)),
  ).toEqual([]);
  expect(await layout(page)).toEqual(before);
  await live.close();
});

test("a visitor's board asks for no saved plan and logs no error", async ({
  browser,
}, testInfo) => {
  // A fresh context carries no cookie. The owner's plan is behind sign-in,
  // so asking for it answers 401 in production, and every browser logs a
  // 4xx as a console error (Lighthouse did, on every run). The page asks
  // who is signed in first, and a visitor never asks for the plan.
  const context = await browser.newContext(device(testInfo));
  const page = await context.newPage();
  const errors: string[] = [];
  const refused: string[] = [];
  const asked: string[] = [];
  page.on("console", (message) => {
    // Chromium ignores COOP on the suite's plain http origin and says so.
    // Production is https. Lighthouse marks the origin secure instead
    // (tools/lighthouse/lighthouserc.cjs).
    const text = message.text();
    if (/^The Cross-Origin-Opener-Policy header has been ignored/.test(text))
      return;
    if (message.type() === "error") errors.push(text);
  });
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith("/api/progress/")) asked.push(path);
  });
  page.on("response", (response) => {
    const path = new URL(response.url()).pathname;
    if (path.startsWith("/api/") && response.status() >= 400)
      refused.push(`${response.status()} ${path}`);
  });

  await page.goto("/planner");
  await expect(
    page.getByText("Changes stay in this tab and are not saved."),
  ).toBeAttached();
  await expectBoard(page);

  expect(refused).toEqual([]);
  expect(asked).toEqual([]);
  expect(errors).toEqual([]);
  await context.close();
});
