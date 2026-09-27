/**
 * skipThemeVar drops variables from the stylesheet, and withSkippedVars puts
 * a value back into theme.vars for each one, so no style reads undefined.
 * e2e/theme-vars.spec.ts checks the pages in a browser.
 */
import {
  createTheme,
  shouldSkipGeneratingVar as muiSkips,
} from "@mui/material/styles";
import { describe, expect, it } from "vitest";
import { colorSchemes, shadows } from "../themePrimitives";
import { skipThemeVar, withSkippedVars } from "../themeVars";

const theme = withSkippedVars(
  createTheme({
    cssVariables: {
      colorSchemeSelector: "data-mui-color-scheme",
      cssVarPrefix: "template",
      shouldSkipGeneratingVar: skipThemeVar,
    },
    colorSchemes,
    defaultColorScheme: "dark",
    shadows,
  }),
);

const css = JSON.stringify(theme.generateStyleSheets());

type Tree = { [key: string]: unknown };

/** Every string or number in the tree, with its path. */
function leaves(tree: Tree, keys: string[] = []): [string[], unknown][] {
  return Object.entries(tree).flatMap(([key, value]) =>
    typeof value === "object" && value !== null
      ? leaves(value as Tree, [...keys, key])
      : typeof value === "string" || typeof value === "number"
        ? [[[...keys, key], value] as [string[], unknown]]
        : [],
  );
}

function at(tree: unknown, keys: string[]) {
  return keys.reduce<unknown>(
    (node, key) => (node as Tree | undefined)?.[key],
    tree,
  );
}

describe("theme variables", () => {
  it("generates no variable for values that are the same in both schemes", () => {
    expect(css).not.toMatch(/--template-(shadows|font|zIndex)-/);
  });

  it("generates the palette the pages use, in both schemes", () => {
    expect(css).toContain("--template-palette-primary-main");
    expect(css).toContain('[data-mui-color-scheme=\\"light\\"]');
  });

  it("generates no palette for components no page ships", () => {
    expect(css).not.toMatch(/--template-palette-(Skeleton|SnackbarContent)-/);
  });

  for (const [name, vars] of [
    ["theme.vars", theme.vars],
    ["generateThemeVars()", theme.generateThemeVars()],
  ] as const) {
    it(`leaves no key of either scheme out of ${name}`, () => {
      const missing = Object.values(theme.colorSchemes).flatMap((scheme) =>
        leaves(scheme as unknown as Tree)
          .filter(([keys]) => !muiSkips(keys))
          .filter(([keys]) => at(vars, keys) === undefined)
          .map(([keys]) => keys.join(".")),
      );
      expect(missing).toEqual([]);
    });

    it(`gives ${name} the value itself for each scheme-free key`, () => {
      expect(vars.shadows[8]).toBe(theme.shadows[8]);
      expect(vars.shadows[1]).toBe("var(--template-palette-baseShadow)");
      expect(vars.zIndex.drawer).toBe(theme.zIndex.drawer);
      expect(vars.font.body1).toBe(theme.font.body1);
    });

    it(`gives ${name} an undefined variable for an unshipped component's palette`, () => {
      expect(vars.palette.Skeleton.bg).toBe(
        "var(--template-palette-Skeleton-bg)",
      );
    });

    it(`leaves ${name} pointing at variables for the palette`, () => {
      expect(vars.palette.primary.main).toMatch(
        /^var\(--template-palette-primary-main[,)]/,
      );
    });
  }
});
