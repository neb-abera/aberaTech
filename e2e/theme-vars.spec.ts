import { expect, type Page, test } from "@playwright/test";

// Every theme variable a page names is defined on the root, in both schemes.
//
// The theme writes its colours, shadows and sizes as CSS custom properties
// (--template-*) and generates only the ones AppTheme's skipThemeVar lets
// through. A style that names a variable the theme stopped generating loses
// its value without an error: the colour, border or shadow just goes. This
// reads every var(--template-...) in the page's stylesheets and in inline
// style attributes, and fails on each one the root does not define.
//
// A reference written with a fallback, var(--x, value), counts the same. MUI
// writes the default scheme's value as the fallback, so on the other scheme
// it is the wrong colour. The two names meant to be undefined are listed
// below with their reasons.
//
// themeVars.ts gives every skipped key that differs by scheme a reference
// like this in theme.vars, so a style that still uses one fails here.
//
// The routes come from /app-routes.json, as in a11y.spec.ts. Styles for a
// menu, a drawer or the other scheme are inserted when they first render, so
// those states are opened too.

const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:8080";
const routes = await fetch(`${baseURL}/app-routes.json`)
  .then((response) => {
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json() as Promise<string[]>;
  })
  .catch((error: unknown) => {
    throw new Error(
      `theme-vars.spec.ts reads the route list from ${baseURL}/app-routes.json before it can name its tests: ${error}`,
    );
  });

const schemes = ["dark", "light"] as const;

/** Named with no definition on purpose. Each says why. */
function meantToBeUndefined(name: string, scheme: string | null) {
  // AppAppBar and PlannerBoard write var(--template-frame-height, 0px), from
  // the MUI template this site began as. Nothing defines it, and the 0px
  // fallback is the value meant, in both schemes.
  if (name === "--template-frame-height") return true;
  // MUI defines the Paper overlays for the dark scheme only. In light, a
  // Paper's var(--template-overlays-N) is undefined, and no overlay is what
  // light is meant to get.
  return scheme === "light" && name.startsWith("--template-overlays-");
}

async function settle(page: Page) {
  await expect(page.locator("h1").first()).toBeVisible();
  await expect(page.locator(".MuiCircularProgress-root")).toHaveCount(0, {
    timeout: 15_000,
  });
}

/** Each --template-* variable the page names and the root leaves empty. */
async function undefinedVars(page: Page, state: string) {
  const missing = await page.evaluate(() => {
    const texts: string[] = [];
    const walk = (rules: CSSRuleList) => {
      for (const rule of Array.from(rules)) texts.push(rule.cssText);
    };
    for (const sheet of Array.from(document.styleSheets)) walk(sheet.cssRules);
    for (const element of Array.from(document.querySelectorAll("[style]"))) {
      texts.push(element.getAttribute("style") ?? "");
    }
    const named = new Set<string>();
    for (const text of texts) {
      for (const match of text.matchAll(/var\(\s*(--template-[\w-]+)/g)) {
        named.add(match[1]);
      }
    }
    const root = getComputedStyle(document.documentElement);
    return {
      scheme: document.documentElement.getAttribute("data-mui-color-scheme"),
      named: named.size,
      missing: [...named].filter(
        (name) => root.getPropertyValue(name).trim() === "",
      ),
    };
  });
  // A page that names no variable at all means the reading broke, not that
  // the page is clean.
  expect(missing.named, `${state}: variables named`).toBeGreaterThan(0);
  return missing.missing
    .filter((name) => !meantToBeUndefined(name, missing.scheme))
    .map((name) => `${state}: ${name}`);
}

async function openIn(page: Page, route: string, scheme: string) {
  await page.addInitScript((mode) => {
    localStorage.setItem("mui-mode", mode);
  }, scheme);
  await page.goto(route);
  await settle(page);
  await expect(page.locator("html")).toHaveAttribute(
    "data-mui-color-scheme",
    scheme,
  );
}

for (const route of routes) {
  test(`${route} names no theme variable the root leaves undefined`, async ({
    page,
  }) => {
    const found: string[] = [];
    for (const scheme of schemes) {
      await openIn(page, route, scheme);
      found.push(...(await undefinedVars(page, scheme)));
    }
    expect(found).toEqual([]);
  });
}

test("the scheme menu, a scheme switch and the phone drawer name only defined theme variables", async ({
  page,
}) => {
  const found: string[] = [];
  for (const scheme of schemes) {
    await page.setViewportSize({ width: 1280, height: 800 });
    await openIn(page, "/", scheme);

    await page.getByRole("button", { name: "Colour scheme" }).first().click();
    await expect(page.getByRole("menu")).toBeVisible();
    found.push(...(await undefinedVars(page, `${scheme}, scheme menu open`)));

    const other = scheme === "dark" ? "Light" : "Dark";
    await page.getByRole("menuitem", { name: other }).click();
    await expect(page.locator("html")).toHaveAttribute(
      "data-mui-color-scheme",
      other.toLowerCase(),
    );
    found.push(
      ...(await undefinedVars(page, `${scheme}, switched to ${other}`)),
    );

    await page.getByRole("button", { name: "Colour scheme" }).first().click();
    await page
      .getByRole("menuitem", { name: scheme === "dark" ? "Dark" : "Light" })
      .click();
    await expect(page.locator("html")).toHaveAttribute(
      "data-mui-color-scheme",
      scheme,
    );

    await page.getByRole("button").first().hover();

    await page.setViewportSize({ width: 390, height: 844 });
    await page.getByRole("button", { name: "Menu button" }).click();
    await expect(
      page.getByRole("button", { name: "Close menu" }),
    ).toBeVisible();
    found.push(...(await undefinedVars(page, `${scheme}, phone drawer open`)));
    await page.getByRole("button", { name: "Close menu" }).click();
  }
  expect(found).toEqual([]);
});
