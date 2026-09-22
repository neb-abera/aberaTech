// @vitest-environment jsdom
/**
 * The blue wash is the canvas background, not a box inside the page. Pulling
 * the page down past its top on a Mac or an iPhone shows the canvas, and on
 * 2026-09-22 that was a near-black band above a blue page.
 */

import CssBaseline from "@mui/material/CssBaseline";
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import AppTheme from "../AppTheme";

afterEach(cleanup);

describe("canvas background", () => {
  it("paints the wash on the root element, continuing above the page", () => {
    render(
      <AppTheme>
        <CssBaseline enableColorScheme />
      </AppTheme>,
    );

    const html = getComputedStyle(document.documentElement);
    expect(html.backgroundImage).toContain("radial-gradient");
    expect(html.backgroundSize).toBe("100% 200vh");
    expect(html.backgroundPosition).toBe("0px -100vh");
    expect(html.backgroundColor).toBe(
      "var(--template-palette-background-default)",
    );
  });

  it("leaves the body transparent so it does not cover the wash", () => {
    render(
      <AppTheme>
        <CssBaseline enableColorScheme />
      </AppTheme>,
    );

    expect(getComputedStyle(document.body).backgroundColor).toBe(
      "rgba(0, 0, 0, 0)",
    );
  });

  it("switches the wash with the colour scheme", () => {
    render(
      <AppTheme>
        <CssBaseline enableColorScheme />
      </AppTheme>,
    );

    const dark = getComputedStyle(document.documentElement).backgroundImage;
    document.documentElement.setAttribute("data-mui-color-scheme", "light");
    const light = getComputedStyle(document.documentElement).backgroundImage;
    document.documentElement.removeAttribute("data-mui-color-scheme");

    // jsdom reports hsl(210, 100%, 16%) and hsl(210, 100%, 90%) as rgb.
    expect(dark).toContain("rgb(0, 41, 82)");
    expect(light).toContain("rgb(204, 230, 255)");
  });
});
