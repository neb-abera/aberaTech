import type { SxProps, Theme } from "@mui/material/styles";

/**
 * The blue wash at the top of every page.
 *
 * It used to live inside Hero, which is why it appeared on the home page and
 * nowhere else. Defined once here so the home page and every other page are
 * literally the same gradient rather than two that drift apart.
 */
export const pageBackground: SxProps<Theme> = (theme) => ({
  width: "100%",
  backgroundRepeat: "no-repeat",
  backgroundImage:
    "radial-gradient(ellipse 80% 50% at 50% -20%, hsl(210, 100%, 90%), transparent)",
  ...theme.applyStyles("dark", {
    backgroundImage:
      "radial-gradient(ellipse 80% 50% at 50% -20%, hsl(210, 100%, 16%), transparent)",
  }),
});
