#!/usr/bin/env node
/**
 * summary.mjs — the timings table for a job summary and the pull request
 * comment. Numbers only. Nothing here passes or fails on a timing.
 *
 *   node summary.mjs medians <.lighthouseci dir> <out.json> [--median-run-only] [--of-median-run]
 *       Per-route medians of FCP, LCP, TBT, CLS, Speed Index and score over
 *       every run, written to out.json. With --of-median-run, the numbers
 *       of each route's median run instead. With --median-run-only, every
 *       report but each route's median run is then deleted, so the upload
 *       is 5 reports rather than 25 (docs/lighthouse.md).
 *
 *   node summary.mjs report <medians.json>
 *       Markdown on stdout: the table, and the change against the newest
 *       build on LHCI_BASE_BRANCH, read back from the server. Exits 1 when
 *       the build this job uploaded is not on the server.
 *
 *   node summary.mjs table <medians.json>
 *       The table alone, for a run with nothing uploaded.
 *
 * report reads LHCI_SERVER_BASE_URL, LHCI_BASIC_AUTH__PASSWORD,
 * LHCI_PROJECT (the slug), LHCI_BUILD_CONTEXT__CURRENT_HASH,
 * LHCI_BUILD_CONTEXT__CURRENT_BRANCH and LHCI_BASE_BRANCH. Both read
 * LHCI_TITLE, the heading, and LHCI_TABLE_NOTE, what the numbers are.
 */
import { readFileSync, rmSync, writeFileSync } from "node:fs";
import process from "node:process";
import {
  byRoute,
  medianRunIndex,
  medians,
  metrics,
  readReports,
} from "./lhr.mjs";

export const MARKER = "<!-- lighthouse-ci -->";

const COLUMNS = [
  ["fcp", "FCP", "ms"],
  ["lcp", "LCP", "ms"],
  ["tbt", "TBT", "ms"],
  ["cls", "CLS", ""],
  ["si", "Speed Index", "ms"],
  ["score", "Score", ""],
];

export function computeMedians(dir, { medianRunOnly, ofMedianRun }) {
  const groups = byRoute(readReports(dir));
  const out = {};
  for (const [route, entries] of groups) {
    const lhrs = entries.map(([, lhr]) => lhr);
    const keep = medianRunIndex(lhrs);
    out[route] = {
      runs: lhrs.length,
      ...(ofMedianRun ? metrics(lhrs[keep]) : medians(lhrs)),
    };
    if (medianRunOnly) {
      entries.forEach(([file], i) => {
        if (i === keep) return;
        rmSync(file);
        rmSync(file.replace(/\.json$/, ".html"), { force: true });
      });
    }
  }
  return out;
}

const round = (key, v) =>
  key === "cls" ? v.toFixed(3) : String(Math.round(v));

function cell(key, unit, current, base) {
  if (!Number.isFinite(current)) return "n/a";
  const value = `${round(key, current)}${unit ? ` ${unit}` : ""}`;
  if (!base || !Number.isFinite(base[key])) return value;
  const delta = current - base[key];
  const shown = round(key, Math.abs(delta));
  const sign = Number(shown) === 0 ? "±" : delta > 0 ? "+" : "−";
  return `${value} (${sign}${shown})`;
}

export function table(current, base) {
  const head = `| Route | ${COLUMNS.map(([, h]) => h).join(" | ")} |`;
  const rule = `|${" --- |".repeat(COLUMNS.length + 1)}`;
  const rows = Object.entries(current).map(
    ([route, m]) =>
      `| \`${route}\` | ${COLUMNS.map(([k, , unit]) => cell(k, unit, m[k], base?.[route])).join(" | ")} |`,
  );
  return [head, rule, ...rows].join("\n");
}

function heading(current) {
  const runs = Math.max(...Object.values(current).map((m) => m.runs));
  return [
    `#### ${process.env.LHCI_TITLE ?? "Lighthouse"}`,
    "",
    `Mobile, ${runs} runs a route. ${process.env.LHCI_TABLE_NOTE ?? ""}`.trim(),
  ].join("\n");
}

async function api(path) {
  const server = process.env.LHCI_SERVER_BASE_URL;
  const auth = Buffer.from(
    `lhci:${process.env.LHCI_BASIC_AUTH__PASSWORD ?? ""}`,
  ).toString("base64");
  const response = await fetch(`${server}${path}`, {
    headers: { authorization: `Basic ${auth}` },
  });
  if (!response.ok) throw new Error(`GET ${path}: ${response.status}`);
  return response.json();
}

async function report(mediansPath) {
  const env = (name) => {
    const v = process.env[name];
    if (!v) throw new Error(`${name} is not set`);
    return v;
  };
  const server = env("LHCI_SERVER_BASE_URL");
  const slug = env("LHCI_PROJECT");
  const hash = env("LHCI_BUILD_CONTEXT__CURRENT_HASH");
  const branch = env("LHCI_BUILD_CONTEXT__CURRENT_BRANCH");
  const baseBranch = env("LHCI_BASE_BRANCH");
  const current = JSON.parse(readFileSync(mediansPath, "utf8"));

  const project = await api(`/v1/projects/slug:${encodeURIComponent(slug)}`);
  const q = (params) => new URLSearchParams(params).toString();
  const [build] = await api(
    `/v1/projects/${project.id}/builds?${q({ hash, branch, limit: 1 })}`,
  );
  if (!build)
    throw new Error(`build ${hash} on ${branch} is not on the server`);

  const candidates = await api(
    `/v1/projects/${project.id}/builds?${q({ branch: baseBranch, lifecycle: "sealed", limit: 2 })}`,
  );
  const baseBuild = candidates.find((b) => b.id !== build.id);
  let base;
  if (baseBuild) {
    const runs = await api(
      `/v1/projects/${project.id}/builds/${baseBuild.id}/runs`,
    );
    const byUrl = new Map();
    for (const run of runs) {
      const lhr = JSON.parse(run.lhr);
      const route = new URL(lhr.requestedUrl).pathname;
      if (!byUrl.has(route)) byUrl.set(route, []);
      byUrl.get(route).push(lhr);
    }
    base = Object.fromEntries(
      [...byUrl].map(([route, lhrs]) => [route, medians(lhrs)]),
    );
  }

  const projectUrl = `${server}/app/projects/${slug}`;
  const compareUrl = `${projectUrl}/compare/${build.id}${baseBuild ? `?baseBuild=${baseBuild.id}` : ""}`;
  const lines = [
    heading(current),
    baseBuild
      ? `The change in brackets is against the newest \`${baseBranch}\` build, \`${baseBuild.hash.slice(0, 7)}\` from ${baseBuild.runAt ?? baseBuild.createdAt}.`
      : `No \`${baseBranch}\` build is on the server yet, so there is no change to show.`,
    "",
    table(current, base),
    "",
    `[Compare on the Lighthouse CI server](${compareUrl}) · [all builds](${projectUrl}). The server asks for the \`lhci\` basic auth password (docs/lighthouse.md).`,
  ];
  return lines.join("\n");
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [command, ...args] = process.argv.slice(2);
  try {
    if (command === "medians") {
      const medianRunOnly = args.includes("--median-run-only");
      const ofMedianRun = args.includes("--of-median-run");
      const [dir, out] = args.filter((a) => !a.startsWith("--"));
      if (!dir || !out)
        throw new Error(
          "usage: summary.mjs medians <dir> <out.json> [--median-run-only] [--of-median-run]",
        );
      const result = computeMedians(dir, { medianRunOnly, ofMedianRun });
      writeFileSync(out, `${JSON.stringify(result, null, 2)}\n`);
    } else if (command === "table") {
      if (!args[0]) throw new Error("usage: summary.mjs table <medians.json>");
      const current = JSON.parse(readFileSync(args[0], "utf8"));
      process.stdout.write(`${heading(current)}\n\n${table(current)}\n`);
    } else if (command === "report") {
      if (!args[0]) throw new Error("usage: summary.mjs report <medians.json>");
      process.stdout.write(`${await report(args[0])}\n`);
    } else {
      throw new Error("usage: summary.mjs medians|report ...");
    }
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exit(1);
  }
}
