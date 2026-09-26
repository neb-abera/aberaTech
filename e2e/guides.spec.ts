import { expect, type Request, test } from "@playwright/test";

// The guides' sections, as a reader meets them. /transition used to load 22
// embedded viewers (Google Docs and YouTube) on every visit, all of them
// inside sections nobody had opened. A frame now mounts with its section.
// Sections open from the address, so one can be linked, and more than one
// can be open at once.

const embedHosts = /(^|\.)(docs\.google\.com|youtube\.com)$/;
const isEmbed = (request: Request) =>
  embedHosts.test(new URL(request.url()).hostname);

const summary = (page: import("@playwright/test").Page, name: string) =>
  page.getByRole("button", { name, exact: true });

/**
 * Open a section and wait until it reports open. A click that lands before
 * the page hydrates reaches the prerendered button, which has no handler yet,
 * and is lost: 2 runs in 24 of the documents test failed that way on
 * 2026-09-26. So the click repeats until the section says it is open, and
 * never once it has.
 */
async function openSection(
  page: import("@playwright/test").Page,
  name: string,
) {
  const button = summary(page, name);
  await expect(async () => {
    if ((await button.getAttribute("aria-expanded")) !== "true")
      await button.click();
    await expect(button).toHaveAttribute("aria-expanded", "true", {
      timeout: 1_000,
    });
  }).toPass();
}

test("/transition requests no embedded document until a section opens", async ({
  page,
}) => {
  const embeds: string[] = [];
  page.on("request", (request) => {
    if (isEmbed(request)) embeds.push(request.url());
  });

  await page.goto("/transition");
  // A section with no frames opening proves the page has hydrated, so any
  // frame the script would mount has had its chance to.
  await openSection(page, "Terminal leave");

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

  await openSection(page, "12 to 18 Months before ETS");
  const frame = page.locator('iframe[title="CSP Checklist"]');
  await frame.scrollIntoViewIfNeeded();

  await expect(frame).toHaveAttribute("src", /docs\.google\.com\/viewer/);
  await requested;
});

test("/transition opens the section the address names", async ({ page }) => {
  await page.goto("/transition#9-to-12-months");

  const section = page.locator('[id="9-to-12-months"]');
  await expect(summary(page, "9 to 12 Months before ETS")).toHaveAttribute(
    "aria-expanded",
    "true",
  );
  await expect(section).toBeInViewport();
  await expect(summary(page, "6 to 9 Months before ETS")).toHaveAttribute(
    "aria-expanded",
    "false",
  );

  // A link within the page opens its section too, without a reload.
  await page.evaluate(() => {
    window.location.hash = "#terminal-leave";
  });
  await expect(summary(page, "Terminal leave")).toHaveAttribute(
    "aria-expanded",
    "true",
  );
});

test("/transition keeps more than one section open", async ({ page }) => {
  await page.goto("/transition");

  await openSection(page, "Terminal leave");
  await openSection(page, "Long after ETS");

  await expect(summary(page, "Terminal leave")).toHaveAttribute(
    "aria-expanded",
    "true",
  );
  await expect(summary(page, "Long after ETS")).toHaveAttribute(
    "aria-expanded",
    "true",
  );
});

test("/transition titles every section smaller number first", async ({
  page,
}) => {
  await page.goto("/transition");

  // "18 to 24 Months", "6 to 9 Months", "90 to 180 days": every title
  // reads from the smaller number to the larger. "12 to 9 Months" did not.
  await expect(summary(page, "9 to 12 Months before ETS")).toBeVisible();
  await expect(page.getByText("12 to 9 Months")).toHaveCount(0);
});

test("/technical opens the section the address names, and keeps others open", async ({
  page,
}) => {
  await page.goto("/technical#programming-languages");

  const languages = summary(
    page,
    "What programming languages should you learn",
  );
  await expect(languages).toHaveAttribute("aria-expanded", "true");
  await expect(page.locator('[id="programming-languages"]')).toBeInViewport();

  await openSection(page, "How can kids learn to program");
  await expect(languages).toHaveAttribute("aria-expanded", "true");
  await expect(summary(page, "How can kids learn to program")).toHaveAttribute(
    "aria-expanded",
    "true",
  );
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
  // A section with no frames opening proves the page has hydrated.
  await openSection(page, "How can kids learn to program");

  await expect(page.locator("iframe")).toHaveCount(0);
  expect(requests).toEqual([]);
});

test("/technical loads the video when its section opens", async ({ page }) => {
  await page.goto("/technical");

  await openSection(
    page,
    "Video: Why 95% of Self-Taught Programmers Fail, by Andy Sterkowitz",
  );
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
  await openSection(page, "90 to 180 days before ETS");

  const photo = page.getByRole("img", { name: "Hiring Events" });
  await photo.scrollIntoViewIfNeeded();

  await expect
    .poll(() => photo.evaluate((img: HTMLImageElement) => img.currentSrc))
    .toMatch(/-(768x512|1024x683)\.jpg$/);
});
