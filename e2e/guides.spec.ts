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
  await summary(page, "Terminal leave").click();
  await expect(summary(page, "Terminal leave")).toHaveAttribute(
    "aria-expanded",
    "true",
  );

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

  await summary(page, "12 to 18 Months before ETS").click();
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

  await summary(page, "Terminal leave").click();
  await summary(page, "Long after ETS").click();

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

  await summary(page, "How can kids learn to program").click();
  await expect(languages).toHaveAttribute("aria-expanded", "true");
  await expect(summary(page, "How can kids learn to program")).toHaveAttribute(
    "aria-expanded",
    "true",
  );
});
