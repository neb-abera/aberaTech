import {
  type CssVarsTheme,
  shouldSkipGeneratingVar as muiSkips,
  type Theme,
  type ThemeVars,
} from "@mui/material/styles";

/*
 * Which theme values become CSS variables.
 *
 * Every prerendered page carries the theme's variables inline, for both
 * schemes. On 2026-09-28 that was 233 variables and 3,628 bytes gzipped on
 * every page. Two kinds are dropped here.
 *
 * The same in both schemes. Shadows, font shorthands and z-indexes do not
 * change with the scheme, so a variable for each buys nothing. Their styles
 * get the value itself.
 *
 * Palettes of MUI components no page ships. Using one of these components
 * means taking it off this list.
 */
const SCHEME_FREE = new Set(["shadows", "font", "zIndex"]);

const UNSHIPPED_COMPONENTS = new Set([
  "SnackbarContent",
  "Skeleton",
  "SpeedDialAction",
  "StepConnector",
  "StepContent",
]);

export function skipThemeVar(keys: string[]): boolean {
  return (
    muiSkips(keys) ||
    SCHEME_FREE.has(keys[0]) ||
    (keys[0] === "palette" && UNSHIPPED_COMPONENTS.has(keys[1]))
  );
}

/** What createTheme returns when cssVariables is set. */
export type VarsTheme = Theme & CssVarsTheme & { font: ThemeVars["font"] };

type Tree = { [key: string]: Tree | string | number | (() => unknown) };

/*
 * Styles read the theme through theme.vars, and MUI leaves a skipped key out
 * of theme.vars altogether. A style that reads one gets undefined, and its
 * declaration vanishes without an error, or the page throws. So every key
 * skipThemeVar drops gets a value back here. A scheme-free key gets the value
 * itself. A key that differs by scheme gets var(--template-<key>), which the
 * root leaves undefined, and e2e/theme-vars.spec.ts fails on any page that
 * names one.
 */
function putBack(vars: Tree, source: Tree, prefix: string, keys: string[]) {
  for (const [key, value] of Object.entries(source)) {
    const path = [...keys, key];
    if (typeof value === "object" && value !== null) {
      if (!(key in vars) || typeof vars[key] !== "object") vars[key] = {};
      putBack(vars[key] as Tree, value, prefix, path);
    } else if (
      (typeof value === "string" || typeof value === "number") &&
      skipThemeVar(path) &&
      !muiSkips(path) &&
      !(key in vars)
    ) {
      vars[key] = `var(--${prefix}-${path.join("-")})`;
    }
  }
}

/** Puts a value back into theme.vars for every key skipThemeVar drops. */
export function withSkippedVars(created: Theme): VarsTheme {
  const theme = created as VarsTheme;
  const fill = (generated: ThemeVars): ThemeVars => {
    const vars = {
      ...structuredClone(generated),
      shadows: theme.shadows,
      font: theme.font,
      zIndex: theme.zIndex,
    } as unknown as Tree;
    for (const scheme of Object.values(theme.colorSchemes)) {
      putBack(vars, scheme as unknown as Tree, theme.cssVarPrefix, []);
    }
    return vars as unknown as ThemeVars;
  };
  const generate = theme.generateThemeVars;
  theme.vars = fill(theme.vars);
  theme.generateThemeVars = () => fill(generate());
  return theme;
}
