import { expect, type Page, type TestInfo } from "@playwright/test";

/** The widths a desktop bar has to fit, from the smallest laptop up. */
export const desktopWidths = [1024, 1280, 1440] as const;

interface Item {
  text: string;
  height: number;
  lines: number;
}

/**
 * Measure every labeled entry in the app bar's toolbar: the entry's own box
 * (the link or button, not its label span) and the number of lines its text
 * occupies, read from the text's client rectangles. Home never wraps, so its
 * height is the one-line height every other entry is held to.
 */
export async function measureBar(page: Page): Promise<Item[]> {
  return page.evaluate(() => {
    const toolbar = document.querySelector("header .MuiToolbar-root");
    if (!toolbar) return [];
    const items = [...toolbar.querySelectorAll("a, button")].filter(
      (el) =>
        (el as HTMLElement).offsetParent !== null &&
        (el.textContent ?? "").trim() !== "",
    );
    return items.map((el) => {
      // Lines are read from the label's text nodes alone: an icon beside the
      // label (the menu's arrow) is a box of its own at another top.
      const tops = new Set<number>();
      const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
      for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        if ((node.textContent ?? "").trim() === "") continue;
        const range = document.createRange();
        range.selectNodeContents(node);
        for (const rect of range.getClientRects())
          if (rect.width > 0 && rect.height > 0) tops.add(Math.round(rect.top));
      }
      return {
        text: (el.textContent ?? "").trim(),
        height: Math.round(el.getBoundingClientRect().height * 10) / 10,
        lines: tops.size,
      };
    });
  });
}

/** Assert every entry is one line, at every desktop width, and record the numbers. */
export async function expectOneLineBar(
  page: Page,
  testInfo: TestInfo,
  who: "visitor" | "owner",
) {
  // The bar settles once the account probe answers: Sign in for a visitor,
  // Sign out and the owner's entries for the owner. Measure after that.
  const settled = who === "owner" ? "Sign out" : "Sign in";
  for (const width of desktopWidths) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto("/guides");
    await expect(
      page.getByRole("banner").getByRole(who === "owner" ? "button" : "link", {
        name: settled,
      }),
    ).toBeVisible();
    const items = await measureBar(page);
    const home = items.find((item) => item.text === "Home");
    expect(home, "Home is in the bar").toBeTruthy();
    const description = items
      .map((item) => `${item.text}=${item.height}px/${item.lines}`)
      .join(", ");
    testInfo.annotations.push({ type: `${who} ${width}px`, description });
    console.log(`${testInfo.project.name} ${who} ${width}px: ${description}`);
    for (const item of items) {
      expect.soft(item.lines, `"${item.text}" lines at ${width}px`).toBe(1);
      expect
        .soft(item.height, `"${item.text}" height at ${width}px`)
        .toBeLessThanOrEqual((home?.height ?? 0) + 1);
    }
  }
}
