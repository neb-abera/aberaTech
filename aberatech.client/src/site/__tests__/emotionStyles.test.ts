import { describe, expect, it } from "vitest";
import { hoistStyles } from "../emotionStyles";

describe("hoistStyles", () => {
  it("takes every style element out of the markup", () => {
    const { markup } = hoistStyles(
      '<style data-emotion="css a">.css-a{color:red}</style><div class="css-a">x</div><style data-emotion="css b">.css-b{margin:0}</style><p class="css-b">y</p>',
    );

    expect(markup).toBe('<div class="css-a">x</div><p class="css-b">y</p>');
  });

  it("writes one element per cache, every id named, rules in render order", () => {
    const { styles } = hoistStyles(
      '<style data-emotion="css a">.css-a{color:red}</style><i></i><style data-emotion="css b c">.css-b{margin:0}@keyframes c{}</style>',
    );

    expect(styles).toBe(
      '<style data-emotion="css a b c">.css-a{color:red}.css-b{margin:0}@keyframes c{}</style>',
    );
  });

  it("keeps each global element whole, ahead of the class rules", () => {
    const { styles } = hoistStyles(
      '<style data-emotion="css-global g1">html{margin:0}</style><style data-emotion="css a">.css-a{}</style><style data-emotion="css-global g2">:root{--x:1}</style>',
    );

    expect(styles).toBe(
      '<style data-emotion="css-global g1">html{margin:0}</style><style data-emotion="css-global g2">:root{--x:1}</style><style data-emotion="css a">.css-a{}</style>',
    );
  });

  it("writes a rule once when the render wrote it twice", () => {
    const twice = '<style data-emotion="css a">.css-a{color:red}</style>';

    expect(hoistStyles(twice + twice).styles).toBe(
      '<style data-emotion="css a">.css-a{color:red}</style>',
    );
  });

  it("leaves a render with no styles as it was", () => {
    expect(hoistStyles("<main>plain</main>")).toEqual({
      markup: "<main>plain</main>",
      styles: "",
    });
  });
});
