// @vitest-environment jsdom
/**
 * The graduation panel explains which courses the clock spans. A word set
 * in bold beside plain text must keep the space between them.
 */

import { cleanup, render } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it } from "vitest";
import AppTheme from "../../../../theme/AppTheme";
import PlannerBoard from "../PlannerBoard";

beforeAll(() => {
  // jsdom has no matchMedia.
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false,
    }) as MediaQueryList;
});

afterEach(cleanup);

describe("the graduation panel", () => {
  it("keeps the space after the bold word", () => {
    const { container } = render(
      <AppTheme>
        <PlannerBoard />
      </AppTheme>,
    );

    expect(container.textContent).toContain(
      "the courses you apply rather than",
    );
  }, 30_000);
});
