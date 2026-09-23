import type { Theme } from "@mui/material/styles";

/**
 * The blue glow at the top of every page, painted on the browser canvas.
 *
 * Pulling a page down past its top (the rubber band on a Mac, an iPhone, or
 * Firefox) shows the browser canvas above the page. Firefox and Safari paint
 * that gap in the root element's colour and nothing else: a background image
 * on the root stops at the page's top edge. So the page's top edge has to be
 * that flat colour, or the gap shows as a band. The first attempt (PR #191)
 * extended the gradient tile above the page and looked right in Chrome,
 * which never rubber-bands, and wrong in Firefox.
 *
 * Two layers on the root element: a short fade from the page colour at the
 * top edge, over the glow. The top row of pixels is the page colour, the
 * glow appears under the bar, and the gap above the page is more of the
 * same colour. e2e/canvas.spec.ts samples the pixels.
 *
 * The body must not paint its own colour, or it covers the glow. The colour
 * moves to the root too, so a page shorter than the viewport ends on the
 * scheme's background rather than the browser's white.
 *
 * Dark is the site's default and the no-attribute case; light is the
 * override, so a page that has not yet run the colour-scheme script paints
 * as dark.
 */
const glow = (colour: string) =>
  `radial-gradient(ellipse 80% 40vh at 50% -15vh, ${colour}, transparent)`;

export const canvasBackground = (theme: Theme) => {
  const page = theme.vars?.palette.background.default ?? "transparent";
  const edge = `linear-gradient(${page}, transparent 72px)`;
  return {
    html: {
      backgroundColor: page,
      backgroundImage: `${edge}, ${glow("hsl(210, 100%, 16%)")}`,
      backgroundRepeat: "no-repeat",
    },
    'html[data-mui-color-scheme="light"]': {
      backgroundImage: `${edge}, ${glow("hsl(210, 100%, 90%)")}`,
    },
    body: {
      backgroundColor: "transparent",
    },
  };
};
