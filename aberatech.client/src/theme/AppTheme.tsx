import type { ThemeOptions } from "@mui/material/styles";
import {
  createTheme,
  ThemeProvider,
  useColorScheme,
} from "@mui/material/styles";
import * as React from "react";
import { canvasBackground } from "./canvasBackground";
import { dataDisplayCustomizations } from "./customizations/dataDisplay";
import { feedbackCustomizations } from "./customizations/feedback";
import { inputsCustomizations } from "./customizations/inputs";
import { navigationCustomizations } from "./customizations/navigation";
import { surfacesCustomizations } from "./customizations/surfaces";
import { colorSchemes, shadows, shape, typography } from "./themePrimitives";

interface AppThemeProps {
  children: React.ReactNode;
  /**
   * This is for the docs site. You can ignore it or remove it.
   */
  disableCustomTheme?: boolean;
  themeComponents?: ThemeOptions["components"];
}

/** On the root for one tick while the scheme changes; index.css holds the rule. */
export const SWITCHING_CLASS = "scheme-switching";

/**
 * What MUI's disableTransitionOnChange does, without its inline <style>: the
 * CSP allows no style element with text it has not hashed, and that one is
 * written at run time. A class on the root for one tick, and a rule in the
 * page's own stylesheet, turn every transition off while the colours change,
 * so the switch repaints at once instead of animating each surface.
 */
function SchemeSwitchWithoutTransitions() {
  const { colorScheme } = useColorScheme();
  const previous = React.useRef(colorScheme);
  React.useEffect(() => {
    if (previous.current === colorScheme) return;
    const first = previous.current === undefined;
    previous.current = colorScheme;
    if (first) return;
    const root = document.documentElement;
    root.classList.add(SWITCHING_CLASS);
    // Style is recalculated with transitions off before the class goes.
    window.getComputedStyle(document.body);
    const timer = window.setTimeout(
      () => root.classList.remove(SWITCHING_CLASS),
      1,
    );
    return () => {
      window.clearTimeout(timer);
      root.classList.remove(SWITCHING_CLASS);
    };
  }, [colorScheme]);
  return null;
}

export default function AppTheme(props: AppThemeProps) {
  const { children, disableCustomTheme, themeComponents } = props;
  const theme = React.useMemo(() => {
    return disableCustomTheme
      ? {}
      : createTheme({
          // For more details about CSS variables configuration, see https://mui.com/material-ui/customization/css-theme-variables/configuration/
          cssVariables: {
            colorSchemeSelector: "data-mui-color-scheme",
            cssVarPrefix: "template",
          },
          colorSchemes, // Recently added in v6 for building light & dark mode app, see https://mui.com/material-ui/customization/palette/#color-schemes
          // Dark unless somebody chooses otherwise. Without this the provider
          // follows the operating system, so half of all first visits would
          // arrive light — and the site is designed dark first.
          defaultColorScheme: "dark",
          typography,
          shadows,
          shape,
          components: {
            MuiCssBaseline: { styleOverrides: canvasBackground },
            ...inputsCustomizations,
            ...dataDisplayCustomizations,
            ...feedbackCustomizations,
            ...navigationCustomizations,
            ...surfacesCustomizations,
            ...themeComponents,
          },
        });
  }, [disableCustomTheme, themeComponents]);
  if (disableCustomTheme) {
    return <React.Fragment>{children}</React.Fragment>;
  }
  return (
    // defaultMode matches Shell's InitColorSchemeScript. Without it the
    // provider starts from "system" on a first visit, paints the light scheme
    // on a light OS, and the dropdown's correction paints dark again a frame
    // later: a flash of light on every first visit, seen in all three engines
    // by e2e/canvas.spec.ts on 2026-09-23.
    <ThemeProvider theme={theme} defaultMode="dark">
      <SchemeSwitchWithoutTransitions />
      {children}
    </ThemeProvider>
  );
}
