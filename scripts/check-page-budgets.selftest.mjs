#!/usr/bin/env node
/**
 * check-page-budgets.selftest.mjs — show check-page-budgets.mjs failing.
 *
 * A gate that has only ever been seen to pass is not yet a gate. This builds
 * small fixture sites and runs the real script against them as the Dockerfile
 * does, asserting on the exit code and on the message: within budget passes;
 * an entry script over budget fails and names the file; so does a page over
 * budget; a page nobody budgeted for fails; a budget for a page that is gone
 * fails; and a preload that points at nothing fails. A budget more than 10%
 * above what it measures fails and prints the number to set, one exactly 10%
 * above passes, and a floor lets an entry sit at the floor and no higher.
 *
 * Exits non-zero, naming the case, if the checker gets any of them wrong.
 */
import { spawnSync } from "node:child_process";
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";
import { gzipSync } from "node:zlib";

const checker = path.join(
  path.dirname(fileURLToPath(import.meta.url)),
  "check-page-budgets.mjs",
);

/** Bytes gzip cannot shrink, the same ones every run. */
function noise(length) {
  let state = 0x2545f491;
  let out = "";
  while (out.length < length) {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    out += state.toString(36);
  }
  return out.slice(0, length);
}

function site({ scriptBytes = 200, guideBytes = 200, preload = true } = {}) {
  const root = mkdtempSync(path.join(os.tmpdir(), "page-budgets-"));
  const dist = path.join(root, "dist");
  mkdirSync(path.join(dist, "assets"), { recursive: true });
  mkdirSync(path.join(dist, "guide"), { recursive: true });

  const head = [
    '<script type="module" crossorigin src="/assets/index-fixture.js"></script>',
    '<link rel="stylesheet" crossorigin href="/assets/index-fixture.css">',
    preload
      ? '<link rel="preload" as="image" href="/assets/avatar-fixture.webp" type="image/webp" />'
      : '<link rel="preload" as="image" href="/assets/gone-fixture.webp" type="image/webp" />',
  ].join("");

  writeFileSync(
    path.join(dist, "index.html"),
    `<html><head>${head}</head><body>home</body></html>`,
  );
  writeFileSync(
    path.join(dist, "guide", "index.html"),
    `<html><head></head><body>${noise(guideBytes)}</body></html>`,
  );
  writeFileSync(
    path.join(dist, "assets", "index-fixture.js"),
    noise(scriptBytes),
  );
  writeFileSync(path.join(dist, "assets", "index-fixture.css"), "body{}");
  writeFileSync(path.join(dist, "assets", "avatar-fixture.webp"), noise(100));

  return { root, dist };
}

/** What the checker counts for a file: gzip -9 for text, bytes for an image. */
function weigh(file) {
  const bytes = readFileSync(file);
  return file.endsWith(".webp")
    ? bytes.length
    : gzipSync(bytes, { level: 9 }).length;
}

/**
 * The fixture's sizes, as the checker will measure them, and budgets set
 * exactly at the ratchet's edge: ceil(measured * 1.1). entry-css has a floor
 * of 1000, as the real stylesheet has one.
 */
const measured = (() => {
  const { root, dist } = site();
  const at = (file) => weigh(path.join(dist, file));
  const js = at("assets/index-fixture.js");
  const css = at("assets/index-fixture.css");
  const sizes = {
    "entry-js": js,
    "entry-css": css,
    "home-initial":
      at("index.html") + js + css + at("assets/avatar-fixture.webp"),
    "html:/": at("index.html"),
    "html:/guide": at("guide/index.html"),
  };
  rmSync(root, { recursive: true, force: true });
  return sizes;
})();
const edge = (bytes) => Math.ceil((bytes * 11) / 10);
const floors = { "entry-css": 1000 };
const roomy = {
  ...Object.fromEntries(
    Object.entries(measured).map(([name, bytes]) => [name, edge(bytes)]),
  ),
  "entry-css": 1000,
};

const cases = [
  {
    name: "a build with every budget exactly 10% above what it measures passes",
    exit: 0,
    says: "all within",
  },
  {
    name: "an entry script over budget fails and is named",
    site: { scriptBytes: 5000 },
    exit: 1,
    says: "entry-js: assets/index-fixture.js is",
  },
  {
    name: "a page over budget fails and is named",
    site: { guideBytes: 5000 },
    exit: 1,
    says: "html:/guide: guide/index.html is",
  },
  {
    name: "a page with no budget fails",
    budgets: { ...roomy, "html:/guide": undefined },
    exit: 1,
    says: "html:/guide: no budget",
  },
  {
    name: "a budget for a page that is not built fails",
    budgets: { ...roomy, "html:/retired": 1000 },
    exit: 1,
    says: "html:/retired: has a budget but is not in the build",
  },
  {
    name: "a budget more than 10% above what it measures fails and prints the number to set",
    budgets: { ...roomy, "html:/guide": edge(measured["html:/guide"]) + 1 },
    exit: 1,
    says: `html:/guide: guide/index.html is ${measured["html:/guide"]} bytes and its budget of ${edge(measured["html:/guide"]) + 1} leaves more than 10% headroom; set it to ${edge(measured["html:/guide"])}`,
  },
  {
    name: "a budget far below its page's size once the page shrinks fails too",
    budgets: { ...roomy, "entry-js": measured["entry-js"] * 2 },
    exit: 1,
    says: `entry-js: assets/index-fixture.js is ${measured["entry-js"]} bytes and its budget of ${measured["entry-js"] * 2} leaves more than 10% headroom; set it to ${edge(measured["entry-js"])}`,
  },
  {
    name: "a budget with headroom tells the author to lower it in this pull request",
    budgets: { ...roomy, "html:/": edge(measured["html:/"]) + 1 },
    exit: 1,
    says: "Lower each number in scripts/page-budgets.json to the one printed, in this same pull request.",
  },
  {
    name: "an entry with a floor may sit at its floor",
    budgets: roomy,
    exit: 0,
    says: "entry-css",
  },
  {
    name: "an entry with a floor fails one byte above it",
    budgets: { ...roomy, "entry-css": 1001 },
    exit: 1,
    says: "entry-css: assets/index-fixture.css is",
  },
  {
    name: "without its floor, the same entry fails",
    floors: {},
    exit: 1,
    says: "entry-css: assets/index-fixture.css is",
  },
  {
    name: "a floor for an entry with no budget fails",
    floors: { ...floors, "html:/retired": 1000 },
    exit: 1,
    says: "html:/retired: has a floor but no budget",
  },
  {
    name: "a preload of a file that is not in the build fails",
    site: { preload: false },
    exit: 1,
    says: "assets/gone-fixture.webp is named by index.html but is not in the build",
  },
];

let wrong = 0;
for (const test of cases) {
  const { root, dist } = site(test.site);
  const budgetsFile = path.join(root, "budgets.json");
  writeFileSync(
    budgetsFile,
    JSON.stringify({
      budgets: test.budgets ?? roomy,
      floors: test.floors ?? floors,
    }),
  );

  const run = spawnSync(process.execPath, [checker, dist, budgetsFile], {
    encoding: "utf8",
  });
  const output = `${run.stdout}${run.stderr}`;
  const ok = run.status === test.exit && output.includes(test.says);

  process.stdout.write(`${ok ? "ok  " : "FAIL"} ${test.name}\n`);
  if (!ok) {
    wrong++;
    process.stdout.write(
      `     expected exit ${test.exit} and "${test.says}"; got exit ${run.status}:\n${output}\n`,
    );
  }

  rmSync(root, { recursive: true, force: true });
}

if (wrong > 0) {
  process.stderr.write(
    `check-page-budgets selftest: ${wrong} of ${cases.length} wrong\n`,
  );
  process.exit(1);
}
process.stdout.write(
  `check-page-budgets selftest: the checker fails when it should (${cases.length} cases)\n`,
);
