/**
 * What a prerendered page must hold before the build writes it.
 *
 * React does not fail a server render when a component throws inside a
 * Suspense boundary. It writes the boundary as a client render (`<!--$!-->`
 * and a template carrying the error), and renderToString reports nothing.
 * On 2026-09-28 a planted theme break did that to every page, and the build
 * shipped 16 pages whose root held no h1 and no text. So each render is
 * checked here, and tools/prerender.mjs stops the build on the first page
 * that fails, naming it.
 *
 * Every route in prerenderedRoutes.ts is checked. None is exempt: each has an
 * h1 and text. The client-rendered shells in prerender.mjs are not renders
 * and do not come through here.
 */

// The mark React writes where a Suspense boundary gave up on the server.
const CLIENT_RENDERED = "<!--$!-->";
// One token at a time: an invisible block (script, style, template or
// comment), a tag, or the text between tags. Only the text counts. The
// markup is read, never rewritten, so nothing here sanitises it.
const TOKEN =
  /<(script|style|template)\b[^>]*>[\s\S]*?<\/\1[^>]*>|<!--[\s\S]*?-->|<[^>]*>|[^<]+/gi;
const H1 = /<h1\b[^>]*>([\s\S]*?)<\/h1[^>]*>/gi;

function text(html: string): string {
  let found = "";
  for (const [token] of html.matchAll(TOKEN)) {
    if (!token.startsWith("<")) found += token;
  }
  return found.trim();
}

function message(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/**
 * Throws, naming the route and the errors, when React reported any while
 * rendering it. renderToString reports none of its own, so entry-server.tsx
 * collects them from the prerender pass and calls this.
 */
export function checkErrors(route: string, errors: readonly unknown[]): void {
  if (errors.length === 0) return;
  const count = `${errors.length} error${errors.length === 1 ? "" : "s"}`;
  throw new Error(
    `prerender ${route}: React reported ${count} while rendering: ${errors.map(message).join(" | ")}`,
  );
}

/**
 * Throws, naming the route and what is wrong, when the render is empty, a
 * boundary switched to client rendering, the root has no text, or the page
 * has no h1 with text.
 */
export function checkPage(route: string, html: string): void {
  const fail = (why: string) => {
    throw new Error(`prerender ${route}: ${why}`);
  };
  if (html.trim() === "") fail("the render is empty");
  if (html.includes(CLIENT_RENDERED)) {
    fail("a Suspense boundary switched to client rendering");
  }
  if (text(html) === "") fail("the root has no text");
  const headings = [...html.matchAll(H1)].map((match) => text(match[1]));
  if (!headings.some((heading) => heading !== "")) fail("the page has no h1");
}
