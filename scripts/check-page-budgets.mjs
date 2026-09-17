#!/usr/bin/env node
/**
 * check-page-budgets.mjs — fail when the production client build has grown
 * past the byte budgets committed in scripts/page-budgets.json.
 *
 *   node scripts/check-page-budgets.mjs <dist directory> <budgets file>
 *
 * Bytes, never milliseconds: the same build measures the same on a laptop and
 * on a shared runner, which a timing never does. Text is measured gzipped at
 * level 9, because that is near what travels and it is what a careless
 * dependency or an inlined blob actually moves; files that are already
 * compressed (images, fonts) are counted as they are.
 *
 * What is measured, all of it read out of the build rather than named by
 * hand, since every asset's name carries a hash that changes with its bytes:
 *
 *   entry-js       the module script dist/index.html starts
 *   entry-css      the stylesheet it links
 *   home-initial   index.html plus everything its head makes the browser
 *                  fetch before anything else: the two above, every
 *                  modulepreload, every preload
 *   html:<route>   each prerendered page, one budget per page
 *
 * A prerendered page with no budget fails, and so does a budget for a page
 * that is no longer built, so the list cannot quietly fall out of step with
 * the site. A file the head names that is not in the build fails too: that
 * is a preload pointing at nothing.
 *
 * Run by the clientbudget stage of aberaTech.Server/Dockerfile, after
 * check-page-budgets.selftest.mjs has shown this script failing on a fixture
 * that is over budget.
 */
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import process from "node:process";
import { gzipSync } from "node:zlib";

const [distArg, budgetsArg] = process.argv.slice(2);
if (!distArg || !budgetsArg) {
  process.stderr.write(
    "usage: check-page-budgets.mjs <dist directory> <budgets file>\n",
  );
  process.exit(2);
}

const dist = path.resolve(distArg);
const { budgets } = JSON.parse(readFileSync(budgetsArg, "utf8"));

const COMPRESSIBLE = new Set([".html", ".js", ".css", ".json", ".svg"]);

/** The bytes a file costs on the wire, near enough. */
function weigh(file) {
  const bytes = readFileSync(file);
  return COMPRESSIBLE.has(path.extname(file))
    ? gzipSync(bytes, { level: 9 }).length
    : bytes.length;
}

const failures = [];
const rows = [];

function check(name, files) {
  const missing = files.filter((file) => !existsSync(file));
  for (const file of missing) {
    failures.push(
      `${name}: ${path.relative(dist, file)} is named by index.html but is not in the build`,
    );
  }
  if (missing.length > 0) return;

  const actual = files.reduce((sum, file) => sum + weigh(file), 0);
  const budget = budgets[name];
  const named = files.map((file) => path.relative(dist, file)).join(", ");

  if (typeof budget !== "number") {
    failures.push(
      `${name}: no budget in ${budgetsArg} (${named} is ${actual} bytes)`,
    );
    return;
  }

  rows.push({ name, actual, budget });
  if (actual > budget) {
    failures.push(
      `${name}: ${named} is ${actual} bytes, over its budget of ${budget} by ${actual - budget}`,
    );
  }
}

/** Same-origin URLs of the tags in the head that match, in document order. */
function headUrls(html, tag, test) {
  const head = html.slice(0, html.indexOf("</head>"));
  const urls = [];
  for (const [element] of head.matchAll(new RegExp(`<${tag}\\b[^>]*>`, "g"))) {
    if (!test(element)) continue;
    const url = element.match(/\b(?:src|href)="(\/[^"/][^"]*)"/)?.[1];
    if (url) urls.push(path.join(dist, url));
  }
  return urls;
}

const indexFile = path.join(dist, "index.html");
if (!existsSync(indexFile)) {
  process.stderr.write(`${indexFile} does not exist; was the client built?\n`);
  process.exit(2);
}
const index = readFileSync(indexFile, "utf8");

const entryJs = headUrls(index, "script", (tag) => /type="module"/.test(tag));
const entryCss = headUrls(index, "link", (tag) => /rel="stylesheet"/.test(tag));
const preloads = headUrls(index, "link", (tag) =>
  /rel="(?:modulepreload|preload)"/.test(tag),
);

if (entryJs.length !== 1 || entryCss.length !== 1) {
  process.stderr.write(
    `expected one module script and one stylesheet in index.html, found ${entryJs.length} and ${entryCss.length}; did the shell change?\n`,
  );
  process.exit(2);
}

check("entry-js", entryJs);
check("entry-css", entryCss);
check("home-initial", [indexFile, ...entryJs, ...entryCss, ...preloads]);

// Every prerendered page: dist/index.html and dist/<route>/index.html.
function pages(directory, route = "") {
  const found = [];
  for (const entry of readdirSync(directory)) {
    const full = path.join(directory, entry);
    if (entry === "assets") continue;
    if (statSync(full).isDirectory()) {
      found.push(...pages(full, `${route}/${entry}`));
    } else if (entry === "index.html") {
      found.push({ route: route === "" ? "/" : route, file: full });
    }
  }
  return found;
}

const built = pages(dist).sort((a, b) => a.route.localeCompare(b.route));
for (const page of built) {
  check(`html:${page.route}`, [page.file]);
}

for (const name of Object.keys(budgets)) {
  if (
    name.startsWith("html:") &&
    !built.some((page) => `html:${page.route}` === name)
  ) {
    failures.push(
      `${name}: has a budget but is not in the build; remove it from ${budgetsArg}`,
    );
  }
}

const width = Math.max(...rows.map((row) => row.name.length));
for (const row of rows) {
  const used = ((row.actual / row.budget) * 100).toFixed(0).padStart(3);
  process.stdout.write(
    `${row.name.padEnd(width)}  ${String(row.actual).padStart(8)} / ${String(row.budget).padStart(8)} bytes  ${used}%\n`,
  );
}

if (failures.length > 0) {
  process.stderr.write(
    `\npage budgets: ${failures.length} over or unaccounted for\n`,
  );
  for (const failure of failures) process.stderr.write(`  ${failure}\n`);
  process.stderr.write(
    "\nIf the growth is deliberate, raise the number in scripts/page-budgets.json in the same pull request and say why there.\n",
  );
  process.exit(1);
}

process.stdout.write("page budgets: all within budget\n");
