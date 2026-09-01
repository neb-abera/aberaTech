/**
 * Bake the static public routes to HTML, after `vite build` has produced both
 * the client bundle (dist/) and the server renderer (dist-server/).
 *
 * Each route's markup is injected into the built index.html — the file that
 * already names the hashed assets — and written where the route's URL maps in
 * wwwroot: /transition becomes dist/transition/index.html, and / fills
 * dist/index.html itself.
 *
 * The untouched template is kept as dist/spa.html for the server's fallback.
 * It cannot share dist/index.html: that file now carries the home page's
 * markup, and a client-rendered route like /schedule served over it would
 * flash the wrong page and then hydrate against DOM that contradicts it.
 */
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import {
  prerenderedRoutes,
  render,
  routes,
} from "../dist-server/entry-server.js";

const MARK = '<div id="root"></div>';

const template = await readFile("dist/index.html", "utf8");
if (!template.includes(MARK)) {
  throw new Error(
    `dist/index.html has no ${MARK} to fill; did the shell change?`,
  );
}

await writeFile("dist/spa.html", template);
process.stdout.write("kept empty shell -> dist/spa.html\n");

// The routes the app can actually render, for the server to answer 404 on
// anything else. Derived from site/routes.ts, so a page added there is served
// by being listed once rather than in two places that can disagree.
const manifest = routes.map((route) => route.path);
await writeFile("dist/app-routes.json", JSON.stringify(manifest, null, 2));
process.stdout.write(
  `route manifest -> dist/app-routes.json (${manifest.length})\n`,
);

for (const route of prerenderedRoutes) {
  const html = await render(route);
  if (html.trim() === "") {
    throw new Error(`route ${route} rendered to nothing`);
  }

  const file =
    route === "/" ? "dist/index.html" : path.join("dist", route, "index.html");
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, template.replace(MARK, `<div id="root">${html}</div>`));
  process.stdout.write(`prerendered ${route} -> ${file}\n`);
}
