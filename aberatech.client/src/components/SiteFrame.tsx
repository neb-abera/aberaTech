import Box from "@mui/material/Box";
import CssBaseline from "@mui/material/CssBaseline";
import type { MouseEvent, ReactNode } from "react";
import AppTheme from "../theme/AppTheme";
import AppAppBar from "./AppAppBar";
import Footer from "./Footer";

export interface SiteFrameProps {
  children: ReactNode;
  disableCustomTheme?: boolean;
}

/** The id the skip link points at. One per document, like the landmark. */
export const mainId = "main-content";

/**
 * Focus the page itself rather than follow the fragment. Following it would
 * put `#main-content` in the address bar, and not every engine moves focus
 * to a fragment's target. The href stays for a browser without the script.
 */
function skipToMain(event: MouseEvent<HTMLAnchorElement>) {
  const main = document.getElementById(mainId);
  if (!main) return;
  event.preventDefault();
  main.focus();
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
 *
 * The page sits in the one `<main>` landmark, and the first thing a
 * keyboard reaches is a link past the bar to it. It is off screen until it
 * has focus. e2e/a11y.spec.ts presses Tab in every engine to prove both.
 */
export default function SiteFrame({ children, ...props }: SiteFrameProps) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <Box
        sx={{ minHeight: "100dvh", display: "flex", flexDirection: "column" }}
      >
        <Box
          component="a"
          href={`#${mainId}`}
          onClick={skipToMain}
          sx={(theme) => ({
            position: "absolute",
            top: 8,
            left: 8,
            zIndex: theme.zIndex.tooltip + 1,
            px: 2,
            py: 1,
            borderRadius: 1,
            bgcolor: "background.paper",
            color: "text.primary",
            border: "2px solid",
            borderColor: "text.primary",
            transform: "translateY(-200%)",
            "&:focus": { transform: "none" },
          })}
        >
          Skip to content
        </Box>
        <AppAppBar />
        <Box
          component="main"
          id={mainId}
          tabIndex={-1}
          sx={{ "&:focus": { outline: "none" } }}
        >
          {children}
        </Box>
        <Footer />
      </Box>
    </AppTheme>
  );
}
