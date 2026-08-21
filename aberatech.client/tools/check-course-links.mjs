/**
 * Request every course link and check it against the catalog.
 *
 * This exists because the previous per-course link was constructed from a
 * guessed pattern and never opened, so all 138 were dead. Nothing here asserts
 * that a link works; it asks, and writes down the answer.
 *
 *   node tools/check-course-links.mjs            # catalogue links only
 *   node tools/check-course-links.mjs --all      # also ep.jhu.edu, needs open egress
 *   node tools/check-course-links.mjs --write    # refresh data/link-check.json
 *
 * Exits non-zero if any catalogue link fails to resolve or returns a course
 * block whose title differs from the one in data/catalog.json.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const dataDir = join(here, '..', 'src', 'features', 'planner', 'data');
const { courses } = JSON.parse(readFileSync(join(dataDir, 'catalog.json'), 'utf8'));

const alsoOfferings = process.argv.includes('--all');
const write = process.argv.includes('--write');

const BLOCK =
  /detail-code[^>]*><strong>([A-Z]{2}\.\d{3}\.\d{3})\.?<\/strong><\/span>[\s\S]*?detail-title[^>]*><strong>([\s\S]*?)\.?<\/strong><\/span>/;

const norm = (s) =>
  s
    .replace(/&amp;/g, '&')
    .replace(/&/g, 'and')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim();

const catalogueUrl = (code) => `https://e-catalogue.jhu.edu/search/?P=${encodeURIComponent(code)}`;
const offeringsUrl = (code, title) =>
  `https://ep.jhu.edu/courses/${code.replace(/^EN\./, '').replace(/\./g, '')}-${title
    .toLowerCase()
    .replace(/&/g, ' ')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')}/`;

// The catalogue sits behind a filter that refuses unfamiliar agents with a 403,
// which looks exactly like a missing page. Identify as a browser so a real 404
// stays distinguishable from being turned away at the door.
const UA =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36';

async function get(url) {
  const res = await fetch(url, { headers: { 'User-Agent': UA, Accept: 'text/html' } });
  return { status: res.status, body: res.ok ? await res.text() : '' };
}

const codes = Object.keys(courses).sort();
const results = {};
let failures = 0;

for (const code of codes) {
  const expected = courses[code].title;
  const { status, body } = await get(catalogueUrl(code));
  const m = BLOCK.exec(body);
  const catalogueTitle = m
    ? m[2]
        .replace(/<[^>]+>/g, '')
        .replace(/&amp;/g, '&')
        .trim()
    : null;
  const titleMatches = Boolean(catalogueTitle) && norm(catalogueTitle) === norm(expected);
  const row = { httpStatus: status, catalogueTitle, titleMatches };

  if (alsoOfferings) {
    const o = await get(offeringsUrl(code, expected));
    row.offeringsStatus = o.status;
    if (o.status !== 200) failures++;
  }

  if (!titleMatches) {
    failures++;
    console.error(`FAIL ${code}: status ${status}, catalogue says ${catalogueTitle ?? 'nothing'}, we say ${expected}`);
  }
  results[code] = row;
  await new Promise((r) => setTimeout(r, 120));
}

const summary = {
  checkedOn: new Date().toISOString().slice(0, 10),
  source: 'https://e-catalogue.jhu.edu/search/?P=<code>',
  total: codes.length,
  resolvedWithMatchingTitle: codes.filter((c) => results[c].titleMatches).length,
  courses: results
};

if (write) {
  writeFileSync(join(dataDir, 'link-check.json'), `${JSON.stringify(summary, null, 2)}\n`);
}

console.log(`${summary.resolvedWithMatchingTitle} of ${summary.total} course links resolve with a matching title`);
process.exit(failures ? 1 : 0);
