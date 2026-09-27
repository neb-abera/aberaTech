import type { Theme } from "@mui/material/styles";

/**
 * The blue glow at the top of every page, painted on the browser canvas.
 *
 * Pulling a page past either end (the rubber band on a Mac, an iPhone, or
 * Firefox) shows the browser canvas beyond the page. Firefox and Safari fill
 * that gap with the root element's background *colour* and nothing else: a
 * background image on the root stops at the page's edge. PR #191 extended
 * the gradient tile above the page, which Chrome drew and Firefox did not.
 * PR #197 then made the page's top edge the flat page colour so the gap
 * matched it, which is why pulling down showed black and the glow read as a
 * bar across the page rather than a wash off the top edge.
 *
 * So the canvas colour is the glow colour, and the page fades out of it and
 * back into it: blue at both edges, the page colour in between. The gap at
 * either end is more of the same blue, whatever the browser paints there.
 *
 * The colours and sizes match the glow before PR #191, which sat on the hero
 * box: `ellipse 80% 50% at 50% -20%` of hsl(210, 100%, 16%) over a 647px box
 * on the home page. Its top edge measured rgb(2, 27, 53) at the centre and
 * the page colour 100px down the gutters. hsl(210, 100%, 10%) is rgb(0, 26,
 * 51), and the wash reaches the page colour by 96px. The mean luminance of
 * the top 90px is 0.0151 against the old 0.0149. At 16% across the whole
 * edge and 320px deep it was 0.0229.
 *
 * Three parts on the root element: the colour, an opaque wash that carries
 * the page colour through the middle, and the bloom over the top of it.
 * e2e/canvas.spec.ts samples the pixels.
 *
 * The body must not paint its own colour, or it covers all of this.
 *
 * Dark is the site's default and the no-attribute case; light is the
 * override, so a page that has not yet run the colour-scheme script paints
 * as dark.
 */
const bloom = (colour: string) =>
  `radial-gradient(ellipse 80% 325px at 50% -130px, ${colour}, transparent)`;

/** Blue at the top edge, page colour by 96px, blue again over the last 120px. */
const wash = (colour: string, page: string) =>
  `linear-gradient(${colour}, ${page} 96px, ${page} calc(100% - 120px), ${colour})`;

const glow = { dark: "hsl(210, 100%, 10%)", light: "hsl(210, 100%, 94%)" };

export const canvasBackground = (theme: Theme) => {
  const page = theme.vars?.palette.background.default ?? "transparent";
  const paint = (colour: string) => ({
    backgroundColor: colour,
    backgroundImage: `${bloom(colour)}, ${wash(colour, page)}`,
    backgroundRepeat: "no-repeat",
  });
  return {
    html: paint(glow.dark),
    'html[data-mui-color-scheme="light"]': paint(glow.light),
    body: {
      backgroundColor: "transparent",
    },
  };
};
