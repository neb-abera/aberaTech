import { expect, test } from "@playwright/test";

// A page with little on it used to end wherever its content ran out, leaving
// the footer halfway up a tall window and a blank screen below it. The
// column is a screen tall now and the footer takes the slack
// (components/SiteFrame.tsx).
//
// Measured on the footer element itself, not on a wrapper: a wrapper that
// stretches proves nothing about where the footer sits inside it.

// 1600 is taller than these pages are long, which is the case that broke.
test.use({ viewport: { width: 1280, height: 1600 } });

const short = ["/no-such-page", "/links", "/devbox"];

for (const path of short) {
  test(`${path}: the footer ends at the bottom of the window`, async ({
    page,
  }) => {
    await page.goto(path);
    const footer = page.locator("footer");
    await expect(footer).toBeVisible();

    const box = await footer.boundingBox();
    const height = page.viewportSize()?.height ?? 0;
    expect(box, "the footer has no box").not.toBeNull();
    // Its own bottom edge, against the window's. Nothing scrolls on these
    // pages, so the two are the same coordinate space.
    expect(
      Math.abs((box?.y ?? 0) + (box?.height ?? 0) - height),
      `footer bottom ${(box?.y ?? 0) + (box?.height ?? 0)}, window ${height}`,
    ).toBeLessThanOrEqual(1);
  });
}
