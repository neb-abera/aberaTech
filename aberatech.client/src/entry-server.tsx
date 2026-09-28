import type { ReactNode } from "react";
import { renderToString } from "react-dom/server";
import { prerenderToNodeStream } from "react-dom/static";
import { StaticRouter } from "react-router";
import Shell from "./Shell.tsx";
import { checkErrors } from "./site/prerenderCheck";

// The render's style elements, gathered into the head for the CSP to hash.
export { hoistStyles } from "./site/emotionStyles";
// The head each prerendered page carries: its own title, description and
// preview card, in place of the shell's one title for every page.
export { headFor } from "./site/meta";
// What a page must hold before the build writes it.
export { checkPage } from "./site/prerenderCheck";
export { prerenderedRoutes } from "./site/prerenderedRoutes";
// The server needs the app's own list of pages to tell a real page from a
// typo; without it every unknown path was answered with the shell and a 200.
export { routes } from "./site/routes";
// The sitemap, written beside the pages it lists.
export { sitemapXml } from "./site/sitemap";

/**
 * One route rendered to the HTML the browser entry will hydrate. Runs in Node
 * at build time, never in production.
 *
 * prerenderToNodeStream (rather than renderToString) is what makes the lazy
 * routes work: it waits for every suspended chunk to resolve, so the output is
 * the page, not the Suspense fallback. Emotion inlines each component's styles
 * beside it during the render, which is what makes the first paint styled.
 * tools/prerender.mjs then gathers them into the head (hoistStyles), where
 * emotion's client cache would have moved them anyway.
 */
function page(url: string) {
  return (
    <StaticRouter location={url}>
      <Shell />
    </StaticRouter>
  );
}

export function render(url: string): Promise<string> {
  return renderTree(url, () => page(url));
}

/**
 * render, for any tree. Throws, naming the route, when React reports an
 * error. A component that throws inside a Suspense boundary does not fail a
 * server render: React writes the boundary for the browser to render again,
 * and renderToString's own error handler does nothing. On 2026-09-28 that
 * shipped 16 pages with no content and a green build. The prerender pass
 * reports every such error, so its handler collects them.
 */
export async function renderTree(
  url: string,
  tree: () => ReactNode,
): Promise<string> {
  // Two passes with two APIs, each covering the other's blind spot.
  //
  // The first pass exists only to load the route's React.lazy chunk:
  // prerenderToNodeStream waits for suspended components, but emits the
  // Suspense boundary as its fallback plus a hidden deferred segment and a
  // swap script — streaming-shaped output that shows "Loading..." to anything
  // that reads the file without running the script.
  //
  // renderToString emits the markup inline, but cannot wait: a chunk still
  // loading would come out as the fallback. With the lazy cache warmed by the
  // first pass, nothing suspends, and the output is the plain HTML a static
  // file should be.
  const errors: unknown[] = [];
  try {
    const warmup = await prerenderToNodeStream(tree(), {
      onError: (error) => {
        errors.push(error);
      },
    });
    // The prelude is a Node Readable at run time; react-dom types it as a
    // web ReadableStream, which has no resume().
    (warmup.prelude as unknown as { resume(): void }).resume(); // Drain; unused.
  } catch (error) {
    // An error outside every boundary rejects the render. It has reached
    // onError already, unless React failed before rendering at all.
    if (!errors.includes(error)) errors.push(error);
  }
  checkErrors(url, errors);

  return renderToString(tree());
}
