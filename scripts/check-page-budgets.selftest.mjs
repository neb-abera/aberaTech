#!/usr/bin/env node
/**
 * check-page-budgets.selftest.mjs — show check-page-budgets.mjs failing.
 *
 * A gate that has only ever been seen to pass is not yet a gate. This builds
 * small fixture sites and runs the real script against them as the Dockerfile
 * does, asserting on the exit code and on the message: within budget passes;
 * an entry script over budget fails and names the file; so does a page over
 * budget; a page nobody budgeted for fails; a budget for a page that is gone
 * fails; and a preload that points at nothing fails.
 *
 * Exits non-zero, naming the case, if the checker gets any of them wrong.
 */
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

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

const roomy = {
  "entry-js": 1000,
  "entry-css": 1000,
  "home-initial": 3000,
  "html:/": 1000,
  "html:/guide": 1000,
};

const cases = [
  { name: "a build within its budgets passes", exit: 0, says: "all within" },
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
    JSON.stringify({ budgets: test.budgets ?? roomy }),
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
