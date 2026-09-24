/**
 * Render every route to HTML for the prose gate to read.
 *
 * The gate lints the pages the build ships prerendered. The app pages are not
 * among them on purpose (prerenderedRoutes.ts says why: live queue state, an
 * interactive tool, the owner's data), so their copy was never linted and the
 * /fitness intro carried an em dash for weeks with the gate green.
 *
 * Nothing here ships. dist-copy/ exists for the length of the build, for the
 * `clientprose` stage to lint and throw away. The renderer is the same one
 * prerender.mjs uses, so what is linted is what a reader is shown.
 *
 * A route that renders its signed-out state here still hides whatever only
 * the owner sees. That copy is still outside the gate.
 */
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { render, routes } from "../dist-server/entry-server.js";

const OUT = "dist-copy";

// The 404 page has no route of its own: App.tsx catches everything else.
const pages = [...routes.map((route) => route.path), "/not-a-page"];

// A page that renders its error boundary passes every prose rule there is,
// because it has no copy left to break one. Every page carries the footer
// (components/SiteFrame.tsx), so a render without one did not get as far as
// the page and is a hole in the gate rather than a clean page. /schedule
// reached that state through an unguarded localStorage read.
const MARK = "</footer>";

for (const route of pages) {
  const file = path.join(
    OUT,
    route === "/" ? "index.html" : `${route}/index.html`,
  );
  const html = await render(route);
  if (!html.includes(MARK)) {
    throw new Error(
      `${route} rendered ${html.length} characters with no ${MARK}: the page did not render, so the prose gate would read nothing`,
    );
  }
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, html);
  process.stdout.write(
    `copy ${route} -> ${file} (${html.length} characters)\n`,
  );
}

process.stdout.write(`${pages.length} pages rendered for the prose gate\n`);
