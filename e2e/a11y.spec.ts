import { readFileSync } from "node:fs";
import AxeBuilder from "@axe-core/playwright";
import { expect, type Page, test } from "@playwright/test";

// axe over every page the app serves, in every engine. The list comes from
// /app-routes.json, which the build writes from site/routes.ts, so a page
// added there is scanned here without anyone editing this file. The 404
// page is added by hand: it belongs to the router, not to that list.
//
// A serious or critical violation fails the page. One that needs a design
// decision rather than a fix is listed in a11y-allowlist.json with the
// reason, per page and per rule, and an entry that no longer matches
// anything fails too, so the list cannot outlive what it excuses.

interface Allowed {
  route: string;
  rule: string;
  reason: string;
}

const allowlist: Allowed[] = JSON.parse(
  readFileSync(new URL("./a11y-allowlist.json", import.meta.url), "utf8"),
);

const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:8080";
const listed = await fetch(`${baseURL}/app-routes.json`)
  .then((response) => {
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json() as Promise<string[]>;
  })
  .catch((error: unknown) => {
    throw new Error(
      `a11y.spec.ts reads the route list from ${baseURL}/app-routes.json before it can name its tests: ${error}`,
    );
  });

const routes = [...listed, "/no-such-page"];

// The WCAG 2.2 A and AA rules. axe's best-practice rules stay out: they are
// advice, and a gate that fails on advice gets switched off.
const tags = [
  "wcag2a",
  "wcag2aa",
  "wcag21a",
  "wcag21aa",
  "wcag22a",
  "wcag22aa",
];
const failing = new Set(["serious", "critical"]);

async function settle(page: Page) {
  // Client-rendered pages fetch before they draw. Every page has an h1 once
  // it has drawn, and no spinner once its data is in.
  await expect(page.locator("h1").first()).toBeVisible();
  await expect(page.locator(".MuiCircularProgress-root")).toHaveCount(0, {
    timeout: 15_000,
  });
}

// Both schemes: the site opens dark and a visitor can pick light, and a
// colour that clears 4.5:1 on one ground can fail on the other.
const schemes = ["dark", "light"] as const;

async function violations(page: Page) {
  const results = await new AxeBuilder({ page })
    // A YouTube embed's own markup is not this site's to fix. The iframe
    // element itself is still checked, for its title.
    .exclude('iframe[src*="youtube"]')
    .withTags(tags)
    .analyze();
  return results.violations.filter((v) => failing.has(v.impact ?? ""));
}

for (const route of routes) {
  test(`${route} has no serious or critical axe violations`, async ({
    page,
  }) => {
    const found: { scheme: string; id: string; text: string }[] = [];
    for (const scheme of schemes) {
      // MUI keeps the choice in localStorage. The last script added wins.
      await page.addInitScript((mode) => {
        localStorage.setItem("mui-mode", mode);
      }, scheme);
      if (scheme === schemes[0]) await page.goto(route);
      else await page.reload();
      await settle(page);
      await expect(page.locator("html")).toHaveAttribute(
        "data-mui-color-scheme",
        scheme,
      );

      for (const v of await violations(page)) {
        const nodes = v.nodes
          .slice(0, 5)
          .map(
            (n) =>
              `    ${n.target.join(" ")}  ${n.failureSummary?.split("\n")[1] ?? ""}`,
          )
          .join("\n");
        found.push({
          scheme,
          id: v.id,
          text: `${scheme}: ${v.impact} ${v.id}: ${v.help}\n${nodes}`,
        });
      }
    }

    const excused = allowlist.filter((entry) => entry.route === route);
    const unexcused = found.filter(
      (v) => !excused.some((entry) => entry.rule === v.id),
    );
    const stale = excused.filter(
      (entry) => !found.some((v) => v.id === entry.rule),
    );

    expect(
      unexcused.map((v) => v.text),
      `axe on ${route}`,
    ).toEqual([]);
    expect(
      stale.map((entry) => `${entry.rule}: ${entry.reason}`),
      `a11y-allowlist.json entries for ${route} that no longer fire`,
    ).toEqual([]);
    // One main landmark, which the skip link below lands on. axe files this
    // under best practice, so it is asserted here rather than left to axe.
    await expect(page.getByRole("main")).toHaveCount(1);
  });
}

test("the first Tab reaches a skip link, and it moves focus past the bar", async ({
  page,
}) => {
  await page.goto("/guides");
  await settle(page);

  await page.keyboard.press("Tab");
  const skip = page.getByRole("link", { name: "Skip to content" });
  await expect(skip).toBeFocused();
  // Hidden until it has focus, then on screen where a keyboard user sees it.
  const box = await skip.boundingBox();
  expect(box, "the focused skip link has no box").not.toBeNull();
  expect(box?.y ?? -1).toBeGreaterThanOrEqual(0);

  await page.keyboard.press("Enter");
  const main = page.getByRole("main");
  await expect(main).toBeFocused();
  await expect(main.getByRole("heading", { level: 1 })).toHaveText("Guides");
});

test("the phone menu passes axe while open, closes, and navigates", async ({
  page,
  isMobile,
}) => {
  test.skip(!isMobile, "the menu button is the phone layout's");
  await page.goto("/");
  await settle(page);

  const menu = page.getByRole("button", { name: "Menu button" });
  const close = page.getByRole("button", { name: "Close menu" });
  await menu.click();
  await expect(close).toBeVisible();
  await expect(
    page.getByRole("menuitem", { name: "Military Transition Guide" }),
  ).toBeVisible();

  // The drawer slides in and the backdrop fades. Measured mid-way, a
  // half-faded label fails contrast that the settled menu passes.
  await page.evaluate(() =>
    // An animation the drawer replaces is cancelled, and a cancelled
    // animation's promise rejects. Settled either way is what counts.
    Promise.all(
      document.getAnimations().map((a) => a.finished.catch(() => undefined)),
    ),
  );
  const found = (await violations(page)).map(
    (v) =>
      `${v.impact} ${v.id}: ${v.nodes.map((n) => `${n.target.join(" ")} ${n.failureSummary?.split("\n")[1] ?? ""}`).join("; ")}`,
  );
  expect(found, "axe with the menu open").toEqual([]);

  await close.click();
  await expect(close).toBeHidden();

  await menu.click();
  await page.getByRole("menuitem", { name: "Course planner" }).click();
  await expect(page).toHaveURL(/\/planner$/);
  await expect(close).toBeHidden();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});
