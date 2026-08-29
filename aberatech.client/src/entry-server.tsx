import { renderToString } from "react-dom/server";
import { prerenderToNodeStream } from "react-dom/static";
import { StaticRouter } from "react-router";
import Shell from "./Shell.tsx";

export { prerenderedRoutes } from "./site/prerenderedRoutes";

/**
 * One route rendered to the HTML the browser entry will hydrate. Runs in Node
 * at build time, never in production.
 *
 * prerenderToNodeStream (rather than renderToString) is what makes the lazy
 * routes work: it waits for every suspended chunk to resolve, so the output is
 * the page, not the Suspense fallback. Emotion inlines each component's styles
 * beside it during the render, which is what makes the first paint styled; on
 * the client, emotion collects those tags into the head before React compares
 * markup, so hydration does not see them.
 */
function page(url: string) {
  return (
    <StaticRouter location={url}>
      <Shell />
    </StaticRouter>
  );
}

export async function render(url: string): Promise<string> {
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
  const warmup = await prerenderToNodeStream(page(url));
  // The prelude is a Node Readable at run time; react-dom types it as a web
  // ReadableStream, which has no resume().
  (warmup.prelude as unknown as { resume(): void }).resume(); // Drain; unused.

  return renderToString(page(url));
}
