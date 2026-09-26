import { expect, type Page, type Request, test } from "@playwright/test";

// The guides' sections, as a reader meets them. /transition used to load 22
// embedded viewers (Google Docs and YouTube) on every visit, all of them
// inside sections nobody had opened. A frame now mounts with its section.
// Sections open from the address, so one can be linked, and more than one
// can be open at once.
//
// A section is a native <details>. One click on its title opens it, before
// the script has run or after, so no test here waits for the page before
// clicking. Until 2026-09-26 a click that landed before hydration reached a
// button with no handler and was lost, and these tests clicked until the
// section opened. That hid the same loss from every reader on a slow phone.

const embedHosts = /(^|\.)(docs\.google\.com|youtube\.com)$/;
const isEmbed = (request: Request) =>
  embedHosts.test(new URL(request.url()).hostname);

const section = (page: Page, id: string) => page.locator(`details[id="${id}"]`);
const title = (page: Page, id: string) =>
  section(page, id).locator(":scope > summary");

/** One click on the section's title, and it is open. */
async function openSection(page: Page, id: string) {
  await title(page, id).click();
  await expect(section(page, id)).toHaveJSProperty("open", true);
}

/**
 * Every section and everything inside it has hydrated. Each section marks
 * itself once the script behind its contents has run, so a frame that would
 * mount in a closed section has had its chance to.
 */
async function hydrated(page: Page) {
  const all = page.locator("details");
  await expect(all.first()).toBeAttached();
  await expect(page.locator("details:not([data-hydrated])")).toHaveCount(0);
}

test("/transition requests no embedded document until a section opens", async ({
  page,
}) => {
  const embeds: string[] = [];
  page.on("request", (request) => {
    if (isEmbed(request)) embeds.push(request.url());
  });

  await page.goto("/transition");
  await hydrated(page);
  await openSection(page, "terminal-leave");

  await expect(page.locator("iframe")).toHaveCount(0);
  expect(embeds).toEqual([]);
});

test("/transition loads a section's documents when it opens", async ({
  page,
}) => {
  await page.goto("/transition");
  const requested = page.waitForRequest(
    (request) => new URL(request.url()).hostname === "docs.google.com",
  );

  await openSection(page, "12-to-18-months");
  const frame = page.locator('iframe[title="CSP Checklist"]');
  await frame.scrollIntoViewIfNeeded();

  await expect(frame).toHaveAttribute("src", /docs\.google\.com\/viewer/);
  await requested;
});

// A reader sees the prerendered page before its script runs, and may click a
// section's title in that window. Every script is held until after the
// click, so the click lands on the HTML alone. The section must open at
// once, and its documents must load once the script arrives.
test("/transition opens a section clicked before its script runs", async ({
  page,
}) => {
  let release = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(/\.js(\?|$)/, async (route) => {
    await held;
    await route.continue();
  });

  await page.goto("/transition", { waitUntil: "commit" });
  const opened = section(page, "12-to-18-months");
  await title(page, "12-to-18-months").click();
  await expect(opened.getByText("12-18 months", { exact: true })).toBeVisible();
  release();

  await expect(opened.locator('iframe[title="CSP Checklist"]')).toHaveAttribute(
    "src",
    /docs\.google\.com\/viewer/,
  );
  await expect(opened).toHaveJSProperty("open", true);
});

// With scripts off, the words are in the page but a closed section hides
// them. A reader must still be able to open one.
test("/transition opens a section with JavaScript disabled", async ({
  browser,
}) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  await page.goto("/transition");

  const opened = section(page, "terminal-leave");
  await expect(
    opened.getByText("Terminal leave and ETS", { exact: true }),
  ).toBeHidden();
  await title(page, "terminal-leave").click();
  await expect(
    opened.getByText("Terminal leave and ETS", { exact: true }),
  ).toBeVisible();

  await context.close();
});

test("/transition opens the section the address names", async ({ page }) => {
  await page.goto("/transition#9-to-12-months");

  await expect(section(page, "9-to-12-months")).toHaveJSProperty("open", true);
  await expect(section(page, "9-to-12-months")).toBeInViewport();
  await expect(section(page, "6-to-9-months")).toHaveJSProperty("open", false);

  // A link within the page opens its section too, without a reload.
  await page.evaluate(() => {
    window.location.hash = "#terminal-leave";
  });
  await expect(section(page, "terminal-leave")).toHaveJSProperty("open", true);
});

test("/transition keeps more than one section open", async ({ page }) => {
  await page.goto("/transition");

  await openSection(page, "terminal-leave");
  await openSection(page, "long-after-ets");

  await expect(section(page, "terminal-leave")).toHaveJSProperty("open", true);
  await expect(section(page, "long-after-ets")).toHaveJSProperty("open", true);
});

test("/transition titles every section smaller number first", async ({
  page,
}) => {
  await page.goto("/transition");

  // "18 to 24 Months", "6 to 9 Months", "90 to 180 days": every title
  // reads from the smaller number to the larger. "12 to 9 Months" did not.
  await expect(title(page, "9-to-12-months")).toHaveText(
    "9 to 12 Months before ETS",
  );
  await expect(page.getByText("12 to 9 Months")).toHaveCount(0);
});

test("/technical opens the section the address names, and keeps others open", async ({
  page,
}) => {
  await page.goto("/technical#programming-languages");

  const languages = section(page, "programming-languages");
  await expect(languages).toHaveJSProperty("open", true);
  await expect(languages).toBeInViewport();

  await openSection(page, "kids");
  await expect(languages).toHaveJSProperty("open", true);
  await expect(section(page, "kids")).toHaveJSProperty("open", true);
});

// The opening video on /technical loaded on every visit. It now mounts with
// its section, and the thumbnails and player scripts come with it.
const youtubeHosts =
  /(^|\.)(youtube\.com|youtube-nocookie\.com|ytimg\.com|googlevideo\.com)$/;

test("/technical requests nothing from YouTube until its video section opens", async ({
  page,
}) => {
  const requests: string[] = [];
  page.on("request", (request) => {
    if (youtubeHosts.test(new URL(request.url()).hostname))
      requests.push(request.url());
  });

  await page.goto("/technical");
  await hydrated(page);
  await openSection(page, "kids");

  await expect(page.locator("iframe")).toHaveCount(0);
  expect(requests).toEqual([]);
});

test("/technical loads the video when its section opens", async ({ page }) => {
  await page.goto("/technical");

  await openSection(page, "andy-sterkowitz-video");
  const frame = page.locator(
    'iframe[title="Why 95% of Self-Taught Programmers Fail, by Andy Sterkowitz"]',
  );

  await expect(frame).toHaveAttribute(
    "src",
    "https://www.youtube.com/embed/ueXjGMrmn8k",
  );
});

// The Hiring Our Heroes photo is 1200 px and 303 KB at the owner's address,
// shown at 438 px at most. Every engine picks a smaller copy from its srcset.
test("/transition asks for the hiring events photo at its display size", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto("/transition");
  await openSection(page, "90-to-180-days");

  const photo = page.getByRole("img", { name: "Hiring Events" });
  await photo.scrollIntoViewIfNeeded();

  await expect
    .poll(() => photo.evaluate((img: HTMLImageElement) => img.currentSrc))
    .toMatch(/-(768x512|1024x683)\.jpg$/);
});

// A section's contents sit inside its border. The browser slots them through
// a shadow tree that dropped the page's border-box, and the full-width card
// ran 20 px past the section's right edge.
test("/transition keeps an open section's card inside the section", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1100, height: 900 });
  await page.goto("/transition");
  await openSection(page, "terminal-leave");

  const opened = section(page, "terminal-leave");
  const card = opened.getByRole("heading", { name: "Terminal leave and ETS" });
  const outer = await opened.boundingBox();
  const inner = await card.locator("..").boundingBox();
  expect(outer).not.toBeNull();
  expect(inner).not.toBeNull();
  if (!outer || !inner) return;
  expect(inner.x + inner.width).toBeLessThanOrEqual(outer.x + outer.width - 16);
});
