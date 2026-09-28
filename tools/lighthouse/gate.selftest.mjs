#!/usr/bin/env node
// gate.selftest.mjs — each check in gate.mjs fails on a planted defect, a
// clean report passes, and Cloudflare's allowed requests never count. Runs
// the CLI too, so the exit code is part of what is proven. run.sh runs this
// before trusting the gate.
import { spawnSync } from "node:child_process";
import { mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";
import { checkBytes, checkReport, gate } from "./gate.mjs";
import { byRoute } from "./lhr.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const BEACON = "https://static.cloudflareinsights.com/beacon.min.js/v1";
const EMAIL =
  "https://abera.tech/cdn-cgi/scripts/5c5dd728/cloudflare-static/email-decode.min.js";

function clean(route = "/") {
  const url = `https://abera.tech${route}`;
  return {
    requestedUrl: url,
    audits: {
      "render-blocking-insight": {
        score: 1,
        details: { type: "table", items: [] },
      },
      "network-dependency-tree-insight": {
        score: 1,
        details: {
          type: "list",
          items: [
            {
              type: "list-section",
              value: {
                type: "network-tree",
                chains: {
                  a: {
                    url,
                    children: {
                      b: {
                        url: "https://abera.tech/assets/index.js",
                        children: {
                          c: { url: "https://abera.tech/api/me", children: {} },
                        },
                      },
                      d: {
                        url: BEACON,
                        children: {
                          e: {
                            url: "https://abera.tech/cdn-cgi/rum?",
                            children: {
                              f: { url: "https://abera.tech/x", children: {} },
                            },
                          },
                        },
                      },
                    },
                  },
                },
              },
            },
          ],
        },
      },
      "cumulative-layout-shift": { score: 1, numericValue: 0.001 },
      "unsized-images": { score: 1, details: { items: [] } },
      "image-delivery-insight": { score: 1, details: { items: [] } },
      "errors-in-console": {
        score: 1,
        details: {
          items: [
            {
              source: "exception",
              description: "beacon",
              sourceLocation: { url: BEACON },
            },
          ],
        },
      },
      "inspector-issues": { score: 1, details: { items: [] } },
      "network-requests": {
        details: {
          items: [
            { url, transferSize: 1000 },
            { url: BEACON, transferSize: 5000 },
            { url: EMAIL, transferSize: 700 },
          ],
        },
      },
    },
  };
}

const plants = {
  "render-blocking request": (l) => {
    l.audits["render-blocking-insight"].details.items.push({
      url: "https://abera.tech/a.css",
    });
  },
  "request chain of 4": (l) => {
    const c =
      l.audits["network-dependency-tree-insight"].details.items[0].value.chains
        .a.children.b.children.c;
    c.children = {
      g: { url: "https://abera.tech/assets/late.js", children: {} },
    };
  },
  "cumulative layout shift": (l) => {
    l.audits["cumulative-layout-shift"].numericValue = 0.01;
  },
  "unsized-images lists": (l) => {
    l.audits["unsized-images"].details.items.push({
      url: "https://abera.tech/a.png",
    });
  },
  "image-delivery-insight lists": (l) => {
    l.audits["image-delivery-insight"].details.items.push({
      url: "https://abera.tech/b.png",
    });
  },
  "console error": (l) => {
    l.audits["errors-in-console"].details.items.push({
      source: "network",
      description: "404",
    });
  },
  "CSP violation": (l) => {
    l.audits["inspector-issues"].details.items.push({
      issueType: "Content security policy",
      subItems: { items: [{ url: "https://evil.example/x.js" }] },
    });
  },
  "no cumulative-layout-shift audit": (l) => {
    delete l.audits["cumulative-layout-shift"];
  },
  "render-blocking-insight errored": (l) => {
    l.audits["render-blocking-insight"] = {
      scoreDisplayMode: "error",
      errorMessage: "x",
    };
  },
  "Lighthouse failed": (l) => {
    l.runtimeError = { message: "NO_FCP" };
  },
};

let failed = 0;
const fail = (message) => {
  process.stderr.write(`FAIL ${message}\n`);
  failed += 1;
};

const cleanFindings = checkReport(clean(), "clean");
if (cleanFindings.length > 0)
  fail(`a clean report has findings: ${cleanFindings.join("; ")}`);

for (const [expected, plant] of Object.entries(plants)) {
  const lhr = clean();
  plant(lhr);
  const findings = checkReport(lhr, "planted");
  if (!findings.some((f) => f.includes(expected))) {
    fail(
      `planted "${expected}" was not found (got: ${findings.join("; ") || "nothing"})`,
    );
  }
}

// The console allowlist: an entry covers its route, path and message only,
// and an entry that covers nothing is a finding.
const withError = (description) => {
  const lhr = clean();
  lhr.audits["errors-in-console"].details.items.push({
    source: "network",
    description,
    sourceLocation: { url: "https://abera.tech/api/progress/planner" },
  });
  return lhr;
};
const entry = () => ({
  route: "/",
  path: "/api/progress/planner",
  pattern: "status of 401",
  reason: "self-test",
});
const allowOne = { console: [entry()] };
if (
  checkReport(withError("a status of 401 ()"), "allowed", allowOne).length > 0
)
  fail("an allowlisted console error was reported");
if (!allowOne.console[0].used)
  fail("a matching allowlist entry was not marked used");
if (
  checkReport(withError("a status of 500 ()"), "other", { console: [entry()] })
    .length !== 1
)
  fail("a console error the allowlist does not name passed");
const other = withError("a status of 401 ()");
other.requestedUrl = "https://abera.tech/guides";
if (checkReport(other, "other route", { console: [entry()] }).length !== 1)
  fail("an allowlist entry covered another route");

// Bytes: 1000 counted, the beacon and the email script not.
const groups = byRoute([["x", clean()]]);
const bytes = (budget, live, floors = {}) =>
  checkBytes(
    groups,
    { budgets: budget === undefined ? {} : { "lighthouse:/": budget }, floors },
    { live },
  ).findings;
if (bytes(1100, false).length > 0)
  fail(`1000 bytes against 1100 failed: ${bytes(1100, false)}`);
if (!bytes(999, false).some((f) => f.includes("over its budget")))
  fail("999 did not fail as over");
if (!bytes(1101, false).some((f) => f.includes("set it to 1100")))
  fail("1101 did not ask for 1100");
if (bytes(1101, true).length > 0) fail("--live applied the headroom rule");
if (bytes(1500, false, { "lighthouse:/": 1500 }).length > 0)
  fail("a floor was not honoured");
if (!bytes(undefined, false).some((f) => f.includes("no budget")))
  fail("a missing budget passed");

// The CLI: exit 0 clean, 1 on a finding, 2 on no reports.
const dir = mkdtempSync(path.join(tmpdir(), "gate-"));
const budgets = path.join(dir, "budgets.json");
const emptyAllow = path.join(dir, "allow-empty.json");
const staleAllow = path.join(dir, "allow-stale.json");
writeFileSync(emptyAllow, JSON.stringify({ console: [] }));
writeFileSync(staleAllow, JSON.stringify({ console: [entry()] }));
writeFileSync(budgets, JSON.stringify({ budgets: { "lighthouse:/": 1100 } }));
const reports = path.join(dir, "clean");
const planted = path.join(dir, "planted");
const empty = path.join(dir, "empty");
for (const d of [reports, planted, empty]) spawnSync("mkdir", ["-p", d]);
writeFileSync(path.join(reports, "lhr-1.json"), JSON.stringify(clean()));
const bad = clean();
plants["render-blocking request"](bad);
writeFileSync(path.join(planted, "lhr-1.json"), JSON.stringify(bad));
const cli = (allowlist, ...args) =>
  spawnSync(
    process.execPath,
    [path.join(here, "gate.mjs"), budgets, `--allowlist=${allowlist}`, ...args],
    { encoding: "utf8" },
  ).status;
if (cli(emptyAllow, reports) !== 0) fail("the CLI failed a clean directory");
if (cli(emptyAllow, reports, planted) !== 1)
  fail("the CLI passed a planted render-blocking request");
if (cli(staleAllow, reports) !== 1)
  fail("the CLI passed an allowlist entry that matched nothing");
if (cli(emptyAllow, empty) !== 2)
  fail("the CLI did not exit 2 on a directory with no reports");
if (typeof gate !== "function") fail("gate is not exported");

if (failed > 0) {
  process.stderr.write(`gate self-test: ${failed} failures\n`);
  process.exit(1);
}
process.stdout.write(
  `gate self-test: ${Object.keys(plants).length} planted defects caught, the allowlist, byte rules and exit codes hold\n`,
);
