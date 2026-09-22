import type { Theme } from "@mui/material/styles";

/**
 * The blue wash at the top of every page, painted on the browser canvas.
 *
 * It used to be the background of a box inside the page. The canvas behind
 * that box was the plain body colour, so pulling the page down past its top
 * (the rubber band on a Mac or an iPhone) showed a near-black band above the
 * wash. The root element's background is what the canvas paints, everywhere,
 * including the gap the rubber band opens. So the wash lives on `html`, in a
 * tile twice the viewport tall that starts one viewport above the page. The
 * ellipse is centred 15vh above the page top with a 40vh vertical radius,
 * which puts the same wash on the first screen as before and continues it
 * upward into the gap.
 *
 * The body must not paint its own colour, or it covers the wash. The colour
 * moves to `html` too, so a page shorter than the viewport still ends on the
 * scheme's background rather than the browser's white.
 *
 * Dark is the site's default and the no-attribute case; light is the override,
 * so a page that has not yet run the colour-scheme script paints as dark.
 */
const wash = (colour: string) =>
  `radial-gradient(ellipse 80% 20% at 50% 42.5%, ${colour}, transparent)`;

export const canvasBackground = (theme: Theme) => ({
  html: {
    backgroundColor: theme.vars?.palette.background.default,
    backgroundImage: wash("hsl(210, 100%, 16%)"),
    backgroundRepeat: "no-repeat",
    backgroundSize: "100% 200vh",
    backgroundPosition: "0 -100vh",
  },
  'html[data-mui-color-scheme="light"]': {
    backgroundImage: wash("hsl(210, 100%, 90%)"),
  },
  body: {
    backgroundColor: "transparent",
  },
});
