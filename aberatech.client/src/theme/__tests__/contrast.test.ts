/**
 * Caption colours have to be readable in both schemes. On 2026-09-22 a design
 * audit measured the light scheme's `text.disabled` at 2.67:1 on the page
 * background: footer column headings, guide-card path labels, the copyright
 * line and PageShell's note were all below the 4.5:1 WCAG AA needs for text
 * under 18.66px. Dark measured 5.31:1 and was fine. This holds both.
 */

import { createTheme } from "@mui/material/styles";
import { describe, expect, it } from "vitest";
import { colorSchemes } from "../themePrimitives";

// The resolved palette, not the tokens: a role the tokens leave out still
// reaches the page, as MUI's own default. text.disabled was exactly that.
const theme = createTheme({ colorSchemes });

const AA_NORMAL_TEXT = 4.5;

/** `hsl(h, s%, l%)`, `hsla(...)` or `rgba(r, g, b, a)` to [r, g, b, a], 0..1. */
const parse = (colour: string): [number, number, number, number] => {
  const rgba = colour.match(
    /rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:[,/\s]+([\d.]+))?\s*\)/,
  );
  if (rgba) {
    const [r, g, b] = [rgba[1], rgba[2], rgba[3]].map((n) => Number(n) / 255);
    return [r, g, b, rgba[4] === undefined ? 1 : Number(rgba[4])];
  }
  const hsl = colour.match(
    /hsla?\(\s*([\d.]+)[,\s]+([\d.]+)%[,\s]+([\d.]+)%(?:[,/\s]+([\d.]+))?\s*\)/,
  );
  if (!hsl) throw new Error(`contrast test cannot read the colour ${colour}`);
  const [h, s, l] = [
    Number(hsl[1]) / 360,
    Number(hsl[2]) / 100,
    Number(hsl[3]) / 100,
  ];
  const alpha = hsl[4] === undefined ? 1 : Number(hsl[4]);
  if (s === 0) return [l, l, l, alpha];
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
  const p = 2 * l - q;
  const channel = (t: number) => {
    const u = (t + 1) % 1;
    if (u < 1 / 6) return p + (q - p) * 6 * u;
    if (u < 1 / 2) return q;
    if (u < 2 / 3) return p + (q - p) * (2 / 3 - u) * 6;
    return p;
  };
  return [channel(h + 1 / 3), channel(h), channel(h - 1 / 3), alpha];
};

/** A translucent foreground is what the eye sees: composite it on the page. */
const flatten = (fg: string, bg: string) => {
  const [fr, fg_, fb, a] = parse(fg);
  const [br, bg_, bb] = parse(bg);
  return [
    fr * a + br * (1 - a),
    fg_ * a + bg_ * (1 - a),
    fb * a + bb * (1 - a),
  ];
};

const luminance = ([r, g, b]: number[]) => {
  const lin = (c: number) =>
    c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
};

const contrast = (fg: string, bg: string) => {
  const a = luminance(flatten(fg, bg));
  const b = luminance(parse(bg).slice(0, 3));
  const [hi, lo] = a > b ? [a, b] : [b, a];
  return (hi + 0.05) / (lo + 0.05);
};

describe("scheme contrast", () => {
  for (const scheme of ["light", "dark"] as const) {
    const palette = theme.colorSchemes[scheme].palette;
    const surfaces = {
      "background.default": palette.background.default,
      "background.paper": palette.background.paper,
    };
    for (const [role, colour] of Object.entries({
      primary: palette.text.primary,
      secondary: palette.text.secondary,
      disabled: palette.text.disabled,
    })) {
      for (const [surface, background] of Object.entries(surfaces)) {
        it(`${scheme}: text.${role} on ${surface} meets WCAG AA`, () => {
          expect(
            contrast(colour as string, background as string),
          ).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
        });
      }
    }
  }
});
