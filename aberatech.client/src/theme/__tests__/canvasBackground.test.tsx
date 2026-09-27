// @vitest-environment jsdom
/**
 * The blue glow is the canvas background, not a box inside the page, and the
 * canvas colour is the glow colour, so pulling the page past the top shows
 * blue. The page ends in the page colour, with no band at the bottom. On 2026-09-22 that gap was a near-black band above a blue page, and
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
const dark = /hsl\(210, 100%, 10%\)|rgb\(0, 2[56], 51\)/;
const light = /hsl\(210, 100%, 94%\)|rgb\(224, 240, 255\)/;

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

  it("starts the wash on the glow colour and ends it on the page colour", () => {
    mount();

    const page = "var(--template-palette-background-default)";
    const image = root().backgroundImage;
    // The bloom is the first layer, the wash under it. The wash is opaque:
    // the canvas colour shows only in the gap past the page's edges.
    expect(
      image.startsWith("radial-gradient(ellipse 80% 325px at 50% -130px,"),
    ).toBe(true);
    const wash = image.slice(image.indexOf("linear-gradient("));
    expect(wash.slice("linear-gradient(".length)).toMatch(dark);
    expect(wash.endsWith(`, ${page} 96px)`)).toBe(true);
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
