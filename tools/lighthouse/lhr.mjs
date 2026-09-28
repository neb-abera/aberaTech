// Reading Lighthouse reports: shared by gate.mjs and summary.mjs.
import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";

// Cloudflare adds these to abera.tech: the email obfuscation script and the
// Web Analytics beacon with its report. Neb keeps both on. The production
// image in a pull request has neither. They are left out of every check
// and of the byte count.
export const ALLOWED_THIRD_PARTY = [
  /^https:\/\/static\.cloudflareinsights\.com\//,
  /\/cdn-cgi\/scripts\/[^/]+\/cloudflare-static\/email-decode\.min\.js$/,
  /\/cdn-cgi\/rum(?:\?|$)/,
];

export const allowed = (url) =>
  typeof url === "string" && ALLOWED_THIRD_PARTY.some((re) => re.test(url));

/** Every lhr-*.json in a directory, as [file, report] pairs. */
export function readReports(dir) {
  return readdirSync(dir)
    .filter((name) => /^lhr-.*\.json$/.test(name))
    .sort()
    .map((name) => {
      const file = path.join(dir, name);
      return [file, JSON.parse(readFileSync(file, "utf8"))];
    });
}

/** The route a report measured: its requested URL's path. */
export const routeOf = (lhr) => new URL(lhr.requestedUrl).pathname;

/** Reports grouped by route, in the order routes first appear. */
export function byRoute(reports) {
  const groups = new Map();
  for (const entry of reports) {
    const route = routeOf(entry[1]);
    if (!groups.has(route)) groups.set(route, []);
    groups.get(route).push(entry);
  }
  return groups;
}

export function median(values) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  if (sorted.length === 0) return Number.NaN;
  return sorted[Math.floor(sorted.length / 2)];
}

/** The numbers the summary reports. Timings in ms, CLS unitless, score 0 to 100. */
export function metrics(lhr) {
  const value = (id) => lhr.audits[id]?.numericValue ?? Number.NaN;
  const score = lhr.categories?.performance?.score;
  return {
    fcp: value("first-contentful-paint"),
    lcp: value("largest-contentful-paint"),
    tbt: value("total-blocking-time"),
    cls: value("cumulative-layout-shift"),
    si: value("speed-index"),
    score: typeof score === "number" ? score * 100 : Number.NaN,
  };
}

/** Per-metric medians over a route's runs. */
export function medians(lhrs) {
  const all = lhrs.map(metrics);
  const out = {};
  for (const key of Object.keys(all[0])) {
    out[key] = median(all.map((m) => m[key]));
  }
  return out;
}

/**
 * The run closest to the median, by the rule Lighthouse CI's server uses
 * for its representative run: distance from the median first contentful
 * paint and time to interactive. Returns the index into lhrs.
 */
export function medianRunIndex(lhrs) {
  const fcp = lhrs.map((l) => l.audits["first-contentful-paint"]?.numericValue);
  const tti = lhrs.map((l) => l.audits.interactive?.numericValue);
  const mFcp = median(fcp);
  const mTti = median(tti);
  let best = 0;
  let bestDistance = Number.POSITIVE_INFINITY;
  lhrs.forEach((_, i) => {
    const d = Math.abs(fcp[i] - mFcp) + Math.abs(tti[i] - mTti);
    if (d < bestDistance) {
      bestDistance = d;
      best = i;
    }
  });
  return best;
}

/** Bytes transferred by the page, less the allowed Cloudflare requests. */
export function pageBytes(lhr) {
  const items = lhr.audits["network-requests"]?.details?.items ?? [];
  return items
    .filter((item) => !allowed(item.url))
    .reduce((sum, item) => sum + (item.transferSize ?? 0), 0);
}
