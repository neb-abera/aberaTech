// @vitest-environment jsdom
/**
 * The blue glow is the canvas background, not a box inside the page, and the
 * page's top edge is the flat page colour. Pulling the page down past its top
 * shows the canvas in that colour alone (Firefox, Safari), and on 2026-09-22
 * that was a near-black band above a blue page. e2e/canvas.spec.ts proves the
 * pixels; this locks in the styles that produce them.
 */

import CssBaseline from "@mui/material/CssBaseline";
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import AppTheme from "../AppTheme";

afterEach(cleanup);

function mount() {
  render(
    <AppTheme>
      <CssBaseline enableColorScheme />
    </AppTheme>,
  );
}

describe("canvas background", () => {
  it("paints the glow on the root element under a fade from the page colour", () => {
    mount();

    const html = getComputedStyle(document.documentElement);
    expect(html.backgroundColor).toBe(
      "var(--template-palette-background-default)",
    );
    // The fade is the first layer, so the top edge is the page colour.
    expect(html.backgroundImage).toMatch(
      /^linear-gradient\(var\(--template-palette-background-default\), (transparent|rgba\(0, 0, 0, 0\)) 72px\),\s*radial-gradient/,
    );
  });

  it("leaves the body transparent so it does not cover the glow", () => {
    mount();

    expect(getComputedStyle(document.body).backgroundColor).toBe(
      "rgba(0, 0, 0, 0)",
    );
  });

  it("switches the glow with the colour scheme", () => {
    mount();

    const dark = getComputedStyle(document.documentElement).backgroundImage;
    document.documentElement.setAttribute("data-mui-color-scheme", "light");
    const light = getComputedStyle(document.documentElement).backgroundImage;
    document.documentElement.removeAttribute("data-mui-color-scheme");

    // jsdom sometimes reports the hsl colours as rgb.
    expect(dark).toMatch(/hsl\(210, 100%, 16%\)|rgb\(0, 41, 82\)/);
    expect(light).toMatch(/hsl\(210, 100%, 90%\)|rgb\(204, 230, 255\)/);
  });
});
