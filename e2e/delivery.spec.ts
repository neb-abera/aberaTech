import { expect, test } from "@playwright/test";

// How the site reaches the browser. Each assertion pins a delivery regression
// that is invisible to unit tests and easy to ship: an uncompressed bundle, a
// hashed asset served with max-age=0 (every return visit re-validates it), a
// cached document (deploys stop reaching returning visitors), a cookie on a
// static response (the CDN stops caching it), or a public page that is blank
// until its script runs (a crawler indexes the blank).

type Page = import("@playwright/test").Page;
type Response = import("@playwright/test").Response;

/**
 * The public pages baked to HTML at build time, with the heading each one
 * carries. A copy of aberatech.client/src/site/prerenderedRoutes.ts and the
 * titles in sections.ts, kept honest by the cross-check against
 * /app-routes.json below: a page renamed or removed there fails here rather
 * than quietly leaving nothing under test.
 */
const prerendered: Record<string, string> = {
  "/": "Neb Abera",
  "/guides": "Guides",
  "/projects": "Projects",
  "/transition": "The Military Transition Guide I Wish I Had",
  "/technical": "Learning Software Development",
  "/rf-training": "Tactically Relevant RF Training",
  "/signal-processing": "Learning Signal Processing",
  "/quantum-cryptography": "Learning Quantum and Post-Quantum Cryptography",
};

async function loadHome(page: Page) {
  const responses: Response[] = [];
  page.on("response", (res) => responses.push(res));
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Abera");
  return responses;
}

const pathOf = (res: Response) => new URL(res.url()).pathname;
const isAsset = (res: Response) => pathOf(res).startsWith("/assets/");

test("the bundle and stylesheet are served compressed", async ({ page }) => {
  const responses = await loadHome(page);

  const compressible = responses.filter(
    (res) => isAsset(res) && /\.(js|css)$/.test(pathOf(res)),
  );
  expect(compressible.length).toBeGreaterThan(0);

  for (const res of compressible) {
    const encoding = (await res.headerValue("content-encoding")) ?? "identity";
    expect(encoding, `${res.url()} left the server uncompressed`).toMatch(
      /gzip|br|zstd/,
    );
  }
});

test("hashed assets are cacheable, the document is not", async ({ page }) => {
  const responses = await loadHome(page);

  // Vite content-hashes everything under /assets, so a change produces a new
  // URL and the old one can be cached forever.
  const assets = responses.filter(isAsset);
  expect(assets.length).toBeGreaterThan(0);
  for (const res of assets) {
    const cache = (await res.headerValue("cache-control")) ?? "";
    expect(cache, `${res.url()} is not cacheable`).toContain("immutable");
  }

  // The document is the one URL that must stay fresh: it is where the hashed
  // names live, so caching it means deploys stop reaching returning visitors.
  const doc = responses.find((res) => pathOf(res) === "/");
  expect(doc).toBeDefined();
  const docCache = (await doc?.headerValue("cache-control")) ?? "";
  expect(docCache).toContain("no-cache");
});

test("the document and hashed assets set no cookies", async ({ page }) => {
  // A Set-Cookie on a static response makes it per-visitor: the CDN will not
  // cache it (or worse, caches one visitor's cookie for everyone), and every
  // asset request starts carrying the cookie back. The only cookie this site
  // issues is the host's admin session, and only on sign-in.
  const responses = await loadHome(page);

  const doc = responses.find((res) => pathOf(res) === "/");
  const assets = responses.filter(isAsset);
  expect(doc).toBeDefined();
  expect(assets.length).toBeGreaterThan(0);

  for (const res of [doc, ...assets]) {
    const cookies = (await res?.headersArray())?.filter(
      (header) => header.name.toLowerCase() === "set-cookie",
    );
    expect(cookies, `${res?.url()} sets a cookie`).toEqual([]);
  }
});

test("every prerendered page is one the app can render", async ({
  request,
}) => {
  // The build writes the router's own list of pages beside the bundle, and
  // the server answers 404 to anything outside it. The list above is a copy
  // of a subset; this is what keeps the copy from going stale.
  const response = await request.get("/app-routes.json");
  expect(response.status()).toBe(200);
  const routes: string[] = await response.json();

  for (const path of Object.keys(prerendered)) {
    expect(routes, `${path} is no longer a page of the app`).toContain(path);
  }
});

for (const [path, heading] of Object.entries(prerendered)) {
  test(`${path} is readable before any JavaScript runs`, async ({
    browser,
  }) => {
    // Prerendering's whole promise: first paint is the page, not a blank
    // shell waiting on the bundle, and a crawler reading the HTML sees the
    // words. A browser with JavaScript disabled is the strictest proof.
    const context = await browser.newContext({ javaScriptEnabled: false });
    const page = await context.newPage();
    const response = await page.goto(path);

    expect(response?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: heading }),
    ).toBeVisible();

    await context.close();
  });
}

test("a page that is not prerendered ships the empty shell, not another page", async ({
  request,
}) => {
  // The shell for client-rendered routes must leave the root empty. Serving
  // the home page's baked markup there would flash the wrong page and then
  // hydrate against DOM that contradicts it.
  const response = await request.get("/schedule");

  expect(response.status()).toBe(200);
  expect(await response.text()).toMatch(/<div id="root"><\/div>/);
});
