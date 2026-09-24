import Box from "@mui/material/Box";
import CssBaseline from "@mui/material/CssBaseline";
import type { ReactNode } from "react";
import AppTheme from "../theme/AppTheme";
import AppAppBar from "./AppAppBar";
import Footer from "./Footer";

export interface SiteFrameProps {
  children: ReactNode;
  disableCustomTheme?: boolean;
}

/**
 * The chrome every page carries: the theme, the bar, the page, the footer.
 *
 * The column is at least a screen tall and the footer takes the slack, so a
 * page with little on it (/links with no links, /schedule with no slots,
 * /fitness signed out, 404) ends on the footer at the bottom of the window
 * rather than halfway up a tall monitor. Footer.tsx sets the `mt: auto` that
 * pushes it down; this is the column that gives it room to move.
 *
 * The bar is `position: fixed`, so it is out of the flow and the column's
 * height is the page plus the footer.
 */
export default function SiteFrame({ children, ...props }: SiteFrameProps) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <Box
        sx={{ minHeight: "100dvh", display: "flex", flexDirection: "column" }}
      >
        <AppAppBar />
        {children}
        <Footer />
      </Box>
    </AppTheme>
  );
}
