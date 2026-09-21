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
  headFor,
  prerenderedRoutes,
  render,
  routes,
  sitemapXml,
} from "../dist-server/entry-server.js";

const MARK = '<div id="root"></div>';
// The shell's one title, which each prerendered page replaces with its own
// head: title, description, canonical URL and preview card. spa.html keeps
// the plain title, and App.tsx sets the right one once the page is running.
const TITLE = /<title>[^<]*<\/title>/;

const template = await readFile("dist/index.html", "utf8");
if (!template.includes(MARK)) {
  throw new Error(
    `dist/index.html has no ${MARK} to fill; did the shell change?`,
  );
}
if (!TITLE.test(template)) {
  throw new Error(
    "dist/index.html has no <title> to replace; did the shell change?",
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

await writeFile("dist/sitemap.xml", sitemapXml());
process.stdout.write("sitemap -> dist/sitemap.xml\n");

// The client-rendered pages get the shell with their own head. They cannot
// be prerendered (live queue state, an interactive tool, the owner's data)
// but a link to /schedule pasted into a message used to render a card
// carrying the home page's title, because spa.html has one head for all.
// The root stays empty, so main.tsx renders rather than hydrates.
for (const route of routes.map((entry) => entry.path)) {
  if (prerenderedRoutes.includes(route)) continue;
  const file = path.join("dist", route, "index.html");
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, template.replace(TITLE, headFor(route)));
  process.stdout.write(`shell with its own head ${route} -> ${file}\n`);
}

for (const route of prerenderedRoutes) {
  const html = await render(route);
  if (html.trim() === "") {
    throw new Error(`route ${route} rendered to nothing`);
  }

  const file =
    route === "/" ? "dist/index.html" : path.join("dist", route, "index.html");
  await mkdir(path.dirname(file), { recursive: true });
  const page = template
    .replace(TITLE, headFor(route))
    .replace(MARK, `<div id="root">${html}</div>`);
  await writeFile(file, page);
  process.stdout.write(`prerendered ${route} -> ${file}\n`);
}
