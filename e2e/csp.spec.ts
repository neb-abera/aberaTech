import { expect, type Page, test } from "@playwright/test";

// The page runs under its own Content-Security-Policy without a single
// violation, in every engine. The policy allows no inline <style> by
// default: the prerendered pages carry their styles in elements the server
// allows by hash, and MUI's runtime styles go into empty elements (allowed
// by the empty string's hash) that emotion fills through the CSSOM. A
// violation anywhere here is a page that renders unstyled for a visitor.

const visitorRoutes = [
  "/",
  "/guides",
  "/projects",
  "/transition",
  "/technical",
  "/rf-training",
  "/signal-processing",
  "/quantum-cryptography",
  "/planner",
  "/schedule",
  "/fitness",
  "/links",
  "/plan",
  "/devbox",
  "/schedule/admin",
  "/definitely-not-a-page",
];

const ownerRoutes = ["/devbox", "/schedule/admin", "/links", "/plan"];

interface Violation {
  directive: string;
  blocked: string;
  sample: string;
  element: string;
  source: string;
}

/** Every violation the page reports, from before its first byte runs. */
async function listen(page: Page) {
  // Only the site under test answers. An embedded document or video that
  // keeps talking to its own host would hold networkidle open, and before
  // #217 mounted them lazily that held /transition past 30 seconds. The
  // policy is the page's, so nothing outside it needs to load.
  const origin = new URL(test.info().project.use.baseURL ?? "").origin;
  await page.context().route(
    (url) => url.origin !== origin,
    (route) => route.abort(),
  );
  await page.addInitScript(() => {
    const seen: unknown[] = [];
    (window as unknown as { __csp: unknown[] }).__csp = seen;
    document.addEventListener("securitypolicyviolation", (event) => {
      seen.push({
        directive: event.violatedDirective,
        blocked: event.blockedURI,
        sample: event.sample,
        // Which element: the sample is empty without 'report-sample'.
        element:
          event.target instanceof Element
            ? event.target.outerHTML.slice(0, 200)
            : String(event.target),
        source: `${event.sourceFile}:${event.lineNumber}`,
      });
    });
  });
}

async function visit(page: Page, path: string): Promise<Violation[]> {
  await page.goto(path);
  await page.waitForLoadState("networkidle");
  // Open whatever opens, so the styles of collapsed sections are inserted
  // too. Clicks only on the page's own disclosure buttons, never a link.
  for (const summary of await page.locator('[aria-expanded="false"]').all()) {
    if (!(await summary.isVisible())) continue;
    await summary.click().catch(() => undefined);
    if (new URL(page.url()).pathname !== path) await page.goBack();
    break;
  }
  await page.waitForLoadState("networkidle");
  return page.evaluate(
    () => (window as unknown as { __csp: Violation[] }).__csp,
  );
}

/** The styles emotion wrote are in effect: every one of its elements has a sheet with rules. */
async function emotionStylesApply(page: Page) {
  return page.evaluate(() => {
    const elements = Array.from(
      document.querySelectorAll<HTMLStyleElement>("style[data-emotion]"),
    );
    const rules = elements.reduce(
      (total, element) => total + (element.sheet?.cssRules.length ?? 0),
      0,
    );
    return {
      elements: elements.length,
      withoutSheet: elements.filter((element) => element.sheet === null).length,
      rules,
    };
  });
}

test("the style policy allows no inline style element by default", async ({
  request,
}) => {
  const policy = (await request.get("/")).headers()["content-security-policy"];
  const styleSrc = /(?:^|;\s*)style-src ([^;]*)/.exec(policy)?.[1] ?? "";

  expect(styleSrc).toContain("'self'");
  expect(styleSrc).not.toContain("'unsafe-inline'");
  expect(policy).toMatch(/(?:^|;\s*)style-src-attr 'unsafe-inline'(?:;|$)/);
});

for (const path of visitorRoutes) {
  test(`a visitor on ${path} meets no policy violation`, async ({ page }) => {
    await listen(page);

    const violations = await visit(page, path);

    expect(violations, JSON.stringify(violations, null, 2)).toEqual([]);
    const styles = await emotionStylesApply(page);
    expect(styles.elements).toBeGreaterThan(0);
    expect(styles.withoutSheet).toBe(0);
    expect(styles.rules).toBeGreaterThan(0);
  });
}

test("the owner's pages meet no policy violation", async ({ page }) => {
  await listen(page);
  // The Development sign-in the compose app maps: the same cookie the
  // Google sign-in issues.
  await page.goto("/api/scheduling/admin/sign-in?returnUrl=/devbox");
  await page.waitForURL("**/devbox");
  const me = await (await page.request.get("/api/scheduling/admin/me")).json();
  expect(me.signedIn).toBe(true);

  for (const path of ownerRoutes) {
    const violations = await visit(page, path);
    expect(violations, `${path}: ${JSON.stringify(violations)}`).toEqual([]);
  }
});

test("a prerendered page is styled before its script runs", async ({
  browser,
}) => {
  // With JavaScript off, only the server's HTML and its hashed style
  // elements can style the page.
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  const violations: string[] = [];
  page.on("console", (message) => {
    if (/Content.Security.Policy|Content Security Policy/i.test(message.text()))
      violations.push(message.text());
  });

  await page.goto("/transition");

  const styled = await page.evaluate(() => {
    const sheets = Array.from(
      document.querySelectorAll<HTMLStyleElement>("style[data-emotion]"),
    );
    // A class rule from the head that sets display, and an element it
    // matches: the value the browser computed is the rule's only when the
    // element was allowed to apply.
    let applied: { selector: string; want: string; got: string } | null = null;
    for (const sheet of sheets) {
      for (const rule of Array.from(sheet.sheet?.cssRules ?? [])) {
        if (!(rule instanceof CSSStyleRule)) continue;
        if (!/^\.css-[\w-]+$/.test(rule.selectorText)) continue;
        if (!rule.style.display || rule.style.display === "block") continue;
        const element = document.querySelector(rule.selectorText);
        if (!element) continue;
        applied = {
          selector: rule.selectorText,
          want: rule.style.display,
          got: getComputedStyle(element).display,
        };
        break;
      }
      if (applied) break;
    }
    return {
      elements: sheets.length,
      rules: sheets.reduce((n, s) => n + (s.sheet?.cssRules.length ?? 0), 0),
      applied,
    };
  });
  await context.close();

  expect(violations).toEqual([]);
  expect(styled.elements).toBeGreaterThan(0);
  expect(styled.rules).toBeGreaterThan(0);
  expect(
    styled.applied,
    "a display rule and an element it matches",
  ).not.toBeNull();
  expect(styled.applied?.got).toBe(styled.applied?.want);
});
