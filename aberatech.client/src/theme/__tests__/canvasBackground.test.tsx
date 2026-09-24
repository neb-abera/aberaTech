// @vitest-environment jsdom
/**
 * The blue glow is the canvas background, not a box inside the page, and the
 * canvas colour is the glow colour, so pulling the page past either end shows
 * blue. On 2026-09-22 that gap was a near-black band above a blue page, and
 * on 2026-09-24 it was black again with the glow cut into a bar across the
 * page. e2e/canvas.spec.ts proves the pixels; this locks in the styles that
 * produce them.
 */

import CssBaseline from "@mui/material/CssBaseline";
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import AppTheme from "../AppTheme";

afterEach(cleanup);

// jsdom sometimes reports the hsl colours as rgb.
const dark = /hsl\(210, 100%, 16%\)|rgb\(0, 41, 82\)/;
const light = /hsl\(210, 100%, 90%\)|rgb\(204, 230, 255\)/;

function mount() {
  render(
    <AppTheme>
      <CssBaseline enableColorScheme />
    </AppTheme>,
  );
}

function root() {
  return getComputedStyle(document.documentElement);
}

describe("canvas background", () => {
  it("fills the canvas with the glow colour, so the gap past either end is blue", () => {
    mount();

    expect(root().backgroundColor).toMatch(dark);
  });

  it("starts and ends the wash on the glow colour, with the page colour between", () => {
    mount();

    const page = "var(--template-palette-background-default)";
    const image = root().backgroundImage;
    // The bloom is the first layer, the wash under it. The wash is opaque:
    // the canvas colour shows only in the gap past the page's edges.
    expect(
      image.startsWith("radial-gradient(ellipse 80% 40vh at 50% -15vh,"),
    ).toBe(true);
    const wash = image.slice(image.indexOf("linear-gradient("));
    expect(wash.slice("linear-gradient(".length)).toMatch(dark);
    expect(wash).toContain(`${page} 320px, ${page} calc(100% - 180px)`);
    expect(wash).toMatch(
      /calc\(100% - 180px\), (hsl\(210, 100%, 16%\)|rgb\(0, 41, 82\))\)$/,
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

    const dim = root().backgroundImage;
    document.documentElement.setAttribute("data-mui-color-scheme", "light");
    const lit = root().backgroundImage;
    const litColour = root().backgroundColor;
    document.documentElement.removeAttribute("data-mui-color-scheme");

    expect(dim).toMatch(dark);
    expect(lit).toMatch(light);
    expect(litColour).toMatch(light);
  });
});
