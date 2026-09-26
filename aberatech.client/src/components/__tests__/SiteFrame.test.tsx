// @vitest-environment jsdom
/**
 * The chrome is in one place now, so a page cannot lose the footer by
 * forgetting to render it. Where the footer sits on a short page is layout,
 * which jsdom does not do: e2e/footer.spec.ts measures that. Keyboard focus
 * in a real browser is e2e/a11y.spec.ts.
 */

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it } from "vitest";
import SiteFrame from "../SiteFrame";

afterEach(cleanup);

function mount() {
  render(
    <MemoryRouter>
      <SiteFrame>
        <h1>the page</h1>
      </SiteFrame>
    </MemoryRouter>,
  );
}

describe("SiteFrame", () => {
  it("carries the bar, the page and the footer", () => {
    mount();

    expect(screen.getByRole("banner")).toBeTruthy();
    expect(screen.getByText("the page")).toBeTruthy();
    expect(screen.getByRole("contentinfo")).toBeTruthy();
  });

  it("puts the page, and only the page, in the main landmark", () => {
    mount();

    const main = screen.getByRole("main");
    expect(main.contains(screen.getByText("the page"))).toBe(true);
    expect(main.contains(screen.getByRole("banner"))).toBe(false);
    expect(main.contains(screen.getByRole("contentinfo"))).toBe(false);
  });

  it("starts with a skip link that moves focus to the page", () => {
    mount();

    const skip = screen.getByRole("link", { name: "Skip to content" });
    const focusable = document.querySelectorAll("a[href], button");
    expect(focusable[0]).toBe(skip);

    const main = screen.getByRole("main");
    expect(skip.getAttribute("href")).toBe(`#${main.id}`);
    fireEvent.click(skip);
    expect(document.activeElement).toBe(main);
  });

  it("is a column at least a screen tall, so the footer has slack to take", () => {
    mount();

    const column = screen.getByRole("contentinfo").parentElement;
    const style = getComputedStyle(column as Element);
    expect(style.display).toBe("flex");
    expect(style.flexDirection).toBe("column");
    expect(style.minHeight).toBe("100dvh");
    expect(getComputedStyle(screen.getByRole("contentinfo")).marginTop).toBe(
      "auto",
    );
  });
});
