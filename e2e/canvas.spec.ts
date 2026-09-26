import { inflateSync } from "node:zlib";
import { expect, test } from "@playwright/test";

// Pulling a page past either end (the rubber band on a Mac, an iPhone, or
// Firefox) shows the browser canvas beyond the page, painted in the root
// element's colour and nothing else. That colour is the glow's blue, so the
// gap is more blue; the page's first and last rows of pixels must be that
// same colour, edge to edge, or the seam shows as a band. On 2026-09-22 the
// gap was black above a blue page. On 2026-09-24 the page's edge matched a
// black gap, which is the same defect with the blue taken out. Sampled as
// pixels rather than read from the CSS: the CSS has said one thing and
// Firefox shown another.

type Page = import("@playwright/test").Page;
type Rgb = [number, number, number];

/** The first pixel of a PNG: signature, then chunks; IDAT inflates to a filter byte and the samples. */
function firstPixel(png: Buffer): Rgb {
  const idat: Buffer[] = [];
  for (let at = 8; at < png.length; ) {
    const length = png.readUInt32BE(at);
    if (png.toString("ascii", at + 4, at + 8) === "IDAT") {
      idat.push(png.subarray(at + 8, at + 8 + length));
    }
    at += 12 + length;
  }
  const raw = inflateSync(Buffer.concat(idat));
  return [raw[1], raw[2], raw[3]];
}

async function pixelAt(page: Page, x: number, y: number): Promise<Rgb> {
  return firstPixel(
    await page.screenshot({
      clip: { x, y, width: 1, height: 1 },
      scale: "css",
    }),
  );
}

async function canvasColour(page: Page): Promise<Rgb> {
  const css = await page.evaluate(
    () => getComputedStyle(document.documentElement).backgroundColor,
  );
  const [r, g, b] = css.match(/\d+/g)?.map(Number) ?? [];
  return [r, g, b];
}

/**
 * Two animation frames: the second callback runs only after the page has
 * produced a frame. Chromium answers "Unable to capture screenshot" to a
 * capture made before the first one, and the load event does not wait for
 * it: a capture at commit failed 30 of 120 times, and none after this.
 */
async function painted(page: Page) {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
      ),
  );
}

const near = (a: Rgb, b: Rgb) => a.every((c, i) => Math.abs(c - b[i]) <= 1);

test.afterEach(async ({ page }, info) => {
  if (info.status !== info.expectedStatus) {
    await info.attach("page", {
      body: await page.screenshot({ fullPage: true }),
      contentType: "image/png",
    });
  }
});

const pages = ["/", "/guides", "/transition", "/schedule"];

// Three engines on a four-core runner: /transition, the heaviest page, took
// Firefox past the default 30 s on 2026-09-23. The budget is not a timing
// gate; a slow page still passes, a wrong pixel never does.
test.describe.configure({ timeout: 60_000 });

// A first visit paints dark and stays dark. The provider used to start from
// "system" on a first visit and paint the light scheme on a light OS until a
// correction painted dark again: a flash, caught here as the root attribute
// changing after the first paint, in all three engines on 2026-09-23.
for (const path of pages) {
  test(`${path}: the scheme does not flash on a first visit`, async ({
    page,
  }) => {
    await page.addInitScript(() => {
      const seen: string[] = [];
      (window as unknown as { __schemes: string[] }).__schemes = seen;
      new MutationObserver(() => {
        seen.push(
          document.documentElement.getAttribute("data-mui-color-scheme") ?? "",
        );
      }).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["data-mui-color-scheme"],
      });
    });
    await page.goto(path);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    // Hydrated: the bar's sign-in control only appears once the account
    // probe has answered, which is after the provider has mounted. Then a
    // beat for any correction effect to run.
    await expect(
      page.getByRole("link", { name: "Sign in", exact: true }),
    ).toBeVisible();
    await page.waitForTimeout(300);

    const seen = await page.evaluate(
      () => (window as unknown as { __schemes: string[] }).__schemes,
    );
    expect(
      seen.filter((s) => s !== "dark"),
      `schemes seen: ${seen}`,
    ).toEqual([]);
  });
}

for (const scheme of ["dark", "light"] as const) {
  test.describe(`${scheme} scheme`, () => {
    for (const path of pages) {
      test(`${path}: both edges are the canvas colour, with the glow below the top`, async ({
        page,
      }) => {
        // The stored choice goes in before the first script runs, so the
        // light scheme needs no second load of the page.
        if (scheme === "light") {
          await page.addInitScript(() =>
            localStorage.setItem("mui-mode", "light"),
          );
        }
        await page.goto(path);
        await expect(page.locator("html")).toHaveAttribute(
          "data-mui-color-scheme",
          scheme,
        );
        await page.evaluate(() => document.fonts.ready);
        await painted(page);

        const canvas = await canvasColour(page);
        // Blue, not the page colour: the gap past either end is the point.
        expect(
          canvas[2] - canvas[0],
          `the canvas is not blue: ${canvas}`,
        ).toBeGreaterThan(20);

        const width = page.viewportSize()?.width ?? 1280;
        // Not the exact corners: WebKit paints the single pixel at (0,0) a
        // shade off, and nothing else along either edge.
        for (const x of [8, width / 4, width / 2, (3 * width) / 4, width - 8]) {
          const top = await pixelAt(page, Math.floor(x), 0);
          expect(
            near(top, canvas),
            `top edge at x=${x} is ${top}, canvas is ${canvas}`,
          ).toBe(true);
        }

        // The last row too: pulling up at the bottom shows the same gap.
        const height = page.viewportSize()?.height ?? 720;
        await page.evaluate(() =>
          window.scrollTo(0, document.documentElement.scrollHeight),
        );
        await page.waitForFunction(
          () =>
            Math.abs(
              window.scrollY +
                window.innerHeight -
                document.documentElement.scrollHeight,
            ) <= 1,
        );
        await painted(page);
        for (const x of [8, width / 2, width - 8]) {
          const bottom = await pixelAt(page, Math.floor(x), height - 1);
          expect(
            near(bottom, canvas),
            `bottom edge at x=${x} is ${bottom}, canvas is ${canvas}`,
          ).toBe(true);
        }
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.waitForFunction(() => window.scrollY === 0);
        await painted(page);

        // Under the bar, at the centre: the glow is there, so the page is
        // not simply flat. Dark tints the blue channel up; light tints red
        // down; either way one channel moves.
        const glow = await pixelAt(page, Math.floor(width / 2), 110);
        const moved = Math.max(...glow.map((c, i) => Math.abs(c - canvas[i])));
        expect(
          moved,
          `no glow under the bar: ${glow} against canvas ${canvas}`,
        ).toBeGreaterThan(8);
      });
    }
  });
}
