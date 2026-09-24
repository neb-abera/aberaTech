// @vitest-environment jsdom
/**
 * The chrome is in one place now, so a page cannot lose the footer by
 * forgetting to render it. Where the footer sits on a short page is layout,
 * which jsdom does not do: e2e/footer.spec.ts measures that.
 */

import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it } from "vitest";
import SiteFrame from "../SiteFrame";

afterEach(cleanup);

function mount() {
  render(
    <MemoryRouter>
      <SiteFrame>
        <main>the page</main>
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
