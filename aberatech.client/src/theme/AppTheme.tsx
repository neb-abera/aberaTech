import type { ThemeOptions } from "@mui/material/styles";
import { createTheme, ThemeProvider } from "@mui/material/styles";
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
    <ThemeProvider theme={theme} defaultMode="dark" disableTransitionOnChange>
      {children}
    </ThemeProvider>
  );
}
