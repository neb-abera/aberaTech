#!/usr/bin/env node
/**
 * gate.mjs — fail on the Lighthouse findings that do not depend on timing.
 *
 *   node gate.mjs <page-budgets.json> <.lighthouseci dir>... [--live] [--allowlist=<file>]
 *
 * Every run of every route in every directory is checked. run.sh passes
 * two: the runs under DevTools throttling and the simulated ones. Timings (FCP, LCP, TBT, Speed Index,
 * the score) are never gated here. A shared runner makes them noise.
 * summary.mjs reports them as numbers.
 *
 *   render-blocking   render-blocking-insight lists no request
 *   chains            no chain in network-dependency-tree-insight is longer
 *                     than 3 requests: the document, what it names, and
 *                     what those fetch
 *   cls               cumulative layout shift below 0.01
 *   images            unsized-images and image-delivery-insight list nothing
 *   console           errors-in-console lists nothing but the entries in
 *                     allowlist.json, each with its reason. An entry that
 *                     matches nothing fails, so it cannot outlive its cause.
 *   csp               inspector-issues lists no Content Security Policy issue
 *   bytes             the median bytes transferred, in the first directory,
 *                     are within the route's
 *                     `lighthouse:<route>` budget in page-budgets.json, and
 *                     the budget is at most 10% above them, the same rule
 *                     scripts/check-page-budgets.mjs applies to the build.
 *                     With --live (the nightly run against abera.tech,
 *                     where Cloudflare compresses and adds headers) only
 *                     the upper bound applies.
 *
 * Cloudflare's email obfuscation script and analytics beacon are left out
 * of every check (lhr.mjs). A report with an error, or without one of these
 * audits, fails: a Lighthouse release that renames an audit must not turn
 * this gate into a pass.
 *
 * Exit 0 clean, 1 on a finding, 2 on bad input. gate.selftest.mjs shows each
 * check failing on a planted defect.
 */
import { readFileSync } from "node:fs";
import process from "node:process";
import {
  allowed,
  byRoute,
  median,
  pageBytes,
  readReports,
  routeOf,
} from "./lhr.mjs";

export const MAX_CHAIN_LENGTH = 3;
export const MAX_CLS = 0.01;

const ceilingFor = (actual) => Math.ceil((actual * 11) / 10);

function audit(lhr, id, findings, where) {
  const a = lhr.audits?.[id];
  if (!a) {
    findings.push(`${where}: no ${id} audit in the report`);
    return undefined;
  }
  if (a.scoreDisplayMode === "error") {
    findings.push(`${where}: ${id} errored: ${a.errorMessage ?? "no message"}`);
    return undefined;
  }
  return a;
}

const items = (a) =>
  (a?.details?.items ?? []).filter(
    (i) => !allowed(i.url ?? i.sourceLocation?.url),
  );

/** Each chain as a list of URLs, root first, allowed requests cut out. */
export function chains(tree, prefix = []) {
  const out = [];
  for (const node of Object.values(tree ?? {})) {
    if (allowed(node.url)) continue;
    const path = [...prefix, node.url];
    const below = chains(node.children, path);
    if (below.length === 0) out.push(path);
    else out.push(...below);
  }
  return out;
}

function networkTree(a) {
  for (const item of a?.details?.items ?? []) {
    if (item?.value?.type === "network-tree") return item.value.chains;
  }
  return undefined;
}

/**
 * Whether an allowlist entry covers a console error: same route, same
 * address path, and the message matches. Marks the entry used.
 */
function allowedConsole(allow, lhr, item) {
  const url = item.sourceLocation?.url;
  if (!url) return false;
  const entry = (allow.console ?? []).find(
    (e) =>
      e.route === routeOf(lhr) &&
      new URL(url).pathname === e.path &&
      new RegExp(e.pattern).test(item.description ?? ""),
  );
  if (entry) entry.used = true;
  return Boolean(entry);
}

/** Findings for one report. */
export function checkReport(lhr, where, allow = {}) {
  const findings = [];
  if (lhr.runtimeError) {
    findings.push(`${where}: Lighthouse failed: ${lhr.runtimeError.message}`);
    return findings;
  }

  for (const i of items(
    audit(lhr, "render-blocking-insight", findings, where),
  )) {
    findings.push(`${where}: render-blocking request ${i.url}`);
  }

  const tree = audit(lhr, "network-dependency-tree-insight", findings, where);
  if (tree) {
    const found = networkTree(tree);
    if (!found)
      findings.push(
        `${where}: network-dependency-tree-insight has no network tree`,
      );
    for (const chain of chains(found)) {
      if (chain.length > MAX_CHAIN_LENGTH) {
        findings.push(
          `${where}: request chain of ${chain.length}, over ${MAX_CHAIN_LENGTH}: ${chain.join(" -> ")}`,
        );
      }
    }
  }

  const cls = audit(lhr, "cumulative-layout-shift", findings, where);
  if (cls && !(cls.numericValue < MAX_CLS)) {
    findings.push(
      `${where}: cumulative layout shift ${cls.numericValue}, not below ${MAX_CLS}`,
    );
  }

  for (const id of ["unsized-images", "image-delivery-insight"]) {
    for (const i of items(audit(lhr, id, findings, where))) {
      findings.push(
        `${where}: ${id} lists ${i.url ?? i.node?.snippet ?? JSON.stringify(i)}`,
      );
    }
  }

  for (const i of items(audit(lhr, "errors-in-console", findings, where))) {
    if (allowedConsole(allow, lhr, i)) continue;
    const from = i.sourceLocation?.url ?? i.source ?? "?";
    findings.push(`${where}: console error from ${from}: ${i.description}`);
  }

  for (const i of audit(lhr, "inspector-issues", findings, where)?.details
    ?.items ?? []) {
    if (/content security policy/i.test(i.issueType ?? "")) {
      const urls = (i.subItems?.items ?? [])
        .map((s) => s.url ?? s.directive)
        .join(", ");
      findings.push(`${where}: CSP violation ${urls}`);
    }
  }

  if (
    !audit(lhr, "network-requests", findings, where)?.details?.items?.length
  ) {
    findings.push(`${where}: network-requests lists no request`);
  }
  return findings;
}

/** Findings for the byte budgets, one per route. */
export function checkBytes(groups, budgetsFile, { live }) {
  const findings = [];
  const rows = [];
  const { budgets, floors = {} } = budgetsFile;
  for (const [route, entries] of groups) {
    const name = `lighthouse:${route}`;
    const actual = median(entries.map(([, lhr]) => pageBytes(lhr)));
    const budget = budgets[name];
    rows.push({ name, actual, budget });
    if (typeof budget !== "number") {
      findings.push(
        `${name}: no budget in page-budgets.json (the page moved ${actual} bytes)`,
      );
      continue;
    }
    if (actual > budget) {
      findings.push(
        `${name}: ${actual} bytes, over its budget of ${budget} by ${actual - budget}`,
      );
      continue;
    }
    const ceiling = Math.max(ceilingFor(actual), floors[name] ?? 0);
    if (!live && budget > ceiling) {
      findings.push(
        `${name}: ${actual} bytes and a budget of ${budget} leaves more than 10% headroom; set it to ${ceiling}`,
      );
    }
  }
  return { findings, rows };
}

export function gate(budgetsPath, dirs, { live, allow }) {
  const findings = [];
  let first;
  let runs = 0;
  for (const dir of dirs) {
    const reports = readReports(dir);
    if (reports.length === 0) throw new Error(`no lhr-*.json in ${dir}`);
    runs += reports.length;
    const groups = byRoute(reports);
    first ??= groups;
    for (const [route, entries] of groups) {
      entries.forEach(([, lhr], i) => {
        findings.push(
          ...checkReport(lhr, `${dir} ${route} run ${i + 1}`, allow),
        );
      });
    }
  }
  for (const e of allow.console ?? []) {
    if (!e.used) {
      findings.push(
        `allowlist.json: the console entry for ${e.route} ${e.path} matched nothing; remove it`,
      );
    }
  }
  const budgetsFile = JSON.parse(readFileSync(budgetsPath, "utf8"));
  const bytes = checkBytes(first, budgetsFile, { live });
  findings.push(...bytes.findings);
  return { findings, rows: bytes.rows, routes: [...first.keys()], runs };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const args = process.argv.slice(2);
  const live = args.includes("--live");
  const allowlistPath =
    args.find((a) => a.startsWith("--allowlist="))?.slice(12) ??
    new URL("./allowlist.json", import.meta.url);
  const [budgetsPath, ...dirs] = args.filter((a) => !a.startsWith("--"));
  if (!budgetsPath || dirs.length === 0) {
    process.stderr.write(
      "usage: gate.mjs <page-budgets.json> <.lighthouseci dir>... [--live] [--allowlist=<file>]\n",
    );
    process.exit(2);
  }
  let result;
  try {
    const allow = JSON.parse(readFileSync(allowlistPath, "utf8"));
    result = gate(budgetsPath, dirs, { live, allow });
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exit(2);
  }
  for (const row of result.rows) {
    process.stdout.write(
      `${row.name}  ${row.actual} / ${row.budget ?? "none"} bytes\n`,
    );
  }
  process.stdout.write(
    `${result.runs} reports, ${result.routes.length} routes checked\n`,
  );
  if (result.findings.length > 0) {
    // One line per finding, with the runs it appeared in.
    const grouped = new Map();
    for (const f of result.findings) {
      const run = f.match(/ run (\d+):/)?.[1];
      const key = f.replace(/ run \d+:/, ":");
      if (!grouped.has(key)) grouped.set(key, []);
      if (run) grouped.get(key).push(run);
    }
    process.stderr.write(`\nlighthouse gate: ${grouped.size} findings\n`);
    for (const [key, runs] of grouped) {
      const text = key.length > 300 ? `${key.slice(0, 300)}...` : key;
      const where = runs.length ? ` (runs ${runs.join(", ")})` : "";
      process.stderr.write(`  ${text}${where}\n`);
    }
    process.exit(1);
  }
}
