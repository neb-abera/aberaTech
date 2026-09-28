/**
 * The check every prerendered page passes before the build writes it.
 *
 * On 2026-09-28, during PR #261, a theme key skipped on purpose made every
 * page throw during the render. React switched each page to client
 * rendering, the root held a template and a script and no h1, and the build
 * and its checks passed. These are the shapes that render took.
 */
import { describe, expect, it } from "vitest";
import { checkErrors, checkPage } from "../prerenderCheck";

const healthy =
  '<script>(function(){})()</script><div><style data-emotion="css x">.x{}</style>' +
  '<main><h1 class="MuiTypography-root">Neb Abera</h1><p>Text</p></main></div>';

describe("checkErrors and checkPage", () => {
  it("passes a page with content and an h1", () => {
    expect(() => checkPage("/", healthy)).not.toThrow();
  });

  it("passes a render React reported no error for", () => {
    expect(() => checkErrors("/", [])).not.toThrow();
  });

  it("fails a render React reported an error for, naming the route and the error", () => {
    expect(() =>
      checkErrors("/guides", [
        new TypeError(
          "Cannot read properties of undefined (reading 'replace')",
        ),
      ]),
    ).toThrow(
      /\/guides: React reported 1 error while rendering: Cannot read properties of undefined \(reading 'replace'\)/,
    );
  });

  it("fails a page that rendered to nothing", () => {
    expect(() => checkPage("/links", "  \n")).toThrow(
      /\/links: the render is empty/,
    );
  });

  it("fails a root holding only scripts, styles and markup with no text", () => {
    const empty =
      '<script>(function(){})()</script><div><style data-emotion="css x">.x{}</style><span></span></div>';
    expect(() => checkPage("/plan", empty)).toThrow(
      /\/plan: the root has no text/,
    );
  });

  it("fails a page React switched to client rendering", () => {
    // The shape #261's planted break produced: an error template inside a
    // Suspense boundary marked for the browser to render again.
    const switched =
      '<script>(function(){})()</script><div><!--$!--><template data-msg="Switched to client rendering because the server rendering errored"></template><div>Loading...</div><!--/$--></div>';
    expect(() => checkPage("/", switched)).toThrow(
      /\/: a Suspense boundary switched to client rendering/,
    );
  });

  it("fails a page with text and no h1", () => {
    expect(() => checkPage("/projects", "<div><p>Some text</p></div>")).toThrow(
      /\/projects: the page has no h1/,
    );
  });

  it("fails an h1 with no text", () => {
    expect(() =>
      checkPage("/projects", "<div><h1 class='x'> </h1><p>Text</p></div>"),
    ).toThrow(/\/projects: the page has no h1/);
  });
});
