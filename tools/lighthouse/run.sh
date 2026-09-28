#!/usr/bin/env bash
#
# run.sh — collect, gate, upload and summarise, inside the Playwright image
# (its Chromium, the one e2e/Dockerfile pins). `make lighthouse` and
# `make lighthouse-live` start it. docs/lighthouse.md explains the design.
#
# Mounts: /src (tools/lighthouse, read-only), /budgets.json
# (scripts/page-budgets.json), /out (writable, results land here).
#
# Environment:
#   LHCI_BASE_URL     the site under test
#   LHCI_MODE         ci (the production image in a pull request or on
#                     master) or live (https://abera.tech, the nightly run)
#   LHCI_TOKEN        the project's build token. Unset: nothing is uploaded
#                     and the tables have no comparison. LHCI_REQUIRE_UPLOAD=1
#                     makes an unset token a failure, as CI sets it.
#   LHCI_KEEP_ALL_RUNS=1  upload all 5 runs a route under DevTools
#                     throttling. The builds a later run compares against
#                     (master, production) need them. Otherwise only the
#                     median run is uploaded.
#   LHCI_SERVER_BASE_URL, LHCI_BASIC_AUTH__PASSWORD, LHCI_PROJECT,
#   LHCI_BASE_BRANCH, LHCI_BUILD_CONTEXT__*   passed to lhci and summary.mjs
#
# Exit 1 when the gate finds something or an upload or the summary fails.
# The upload and the summary still run after a gate finding, so the server
# holds every build.
set -uo pipefail

: "${LHCI_BASE_URL:?}" "${LHCI_MODE:?ci or live}"
CHROME_PATH=$(find /ms-playwright -path '*/chrome-linux64/chrome' -type f | head -1)
[ -x "$CHROME_PATH" ] || { echo "no Chromium in /ms-playwright" >&2; exit 1; }
export CHROME_PATH

work=/tmp/lighthouse
rm -rf "$work"
cp -r /src "$work"
cd "$work" || exit 1
npm ci --no-audit --no-fund > /dev/null || exit 1
lhci="$work/node_modules/.bin/lhci"

for _ in $(seq 1 60); do
  curl -sf "$LHCI_BASE_URL/healthz" > /dev/null && break
  sleep 2
done
curl -sf "$LHCI_BASE_URL/healthz" > /dev/null || { echo "$LHCI_BASE_URL/healthz does not answer" >&2; exit 1; }

rm -rf /out/devtools /out/simulate /out/summary.md
status=0

# DevTools throttling first: the numbers the tables lead with. Lighthouse's
# simulation counts modulepreloaded route chunks as blocking paint, and
# reads FCP on this site 600 to 1,800 ms worse than Chrome paints it
# (aberaTech #262). The simulated runs follow, so the score stays
# comparable with PageSpeed Insights.
for method in devtools simulate; do
  mkdir -p "/out/$method"
  (cd "/out/$method" && LHCI_THROTTLING=$method "$lhci" collect --config="$work/lighthouserc.cjs") || {
    echo "lhci collect failed ($method)" >&2
    exit 1
  }
done

live=()
[ "$LHCI_MODE" = live ] && live=(--live)
node gate.selftest.mjs || exit 1
node gate.mjs /budgets.json /out/devtools/.lighthouseci /out/simulate/.lighthouseci "${live[@]}" || status=1

keep=(--median-run-only)
[ "${LHCI_KEEP_ALL_RUNS:-}" = 1 ] && keep=()
node summary.mjs medians /out/devtools/.lighthouseci /out/devtools.json "${keep[@]}" || exit 1
node summary.mjs medians /out/simulate/.lighthouseci /out/simulate.json --median-run-only --of-median-run || exit 1

devtools_note="DevTools throttling (Chrome slowed to a mid-range phone on slow 4G), the median of each metric. Timings are reported here and never gated."
simulate_note="Simulated throttling, as PageSpeed Insights scores, the median run. Reported for comparison with PageSpeed. Its FCP and LCP read this site too slow."

if [ -z "${LHCI_TOKEN:-}" ]; then
  if [ "${LHCI_REQUIRE_UPLOAD:-}" = 1 ]; then
    echo "LHCI_TOKEN is not set, and this run must upload" >&2
    exit 1
  fi
  {
    LHCI_TITLE="Lighthouse, DevTools throttling" LHCI_TABLE_NOTE="$devtools_note" node summary.mjs table /out/devtools.json
    echo
    LHCI_TITLE="Lighthouse, simulated throttling" LHCI_TABLE_NOTE="$simulate_note" node summary.mjs table /out/simulate.json
  } > /out/summary.md
  exit $status
fi

branch=$LHCI_BUILD_CONTEXT__CURRENT_BRANCH
base=$LHCI_BASE_BRANCH
for method in devtools simulate; do
  suffix=""
  [ "$method" = simulate ] && suffix=-simulated
  (cd "/out/$method" &&
    LHCI_BUILD_CONTEXT__CURRENT_BRANCH="$branch$suffix" "$lhci" upload --target=lhci --ignoreDuplicateBuildFailure) || {
    echo "lhci upload failed ($method)" >&2
    exit 1
  }
done

{
  LHCI_TITLE="Lighthouse, DevTools throttling" LHCI_TABLE_NOTE="$devtools_note" \
    node summary.mjs report /out/devtools.json || exit 1
  echo
  LHCI_TITLE="Lighthouse, simulated throttling" LHCI_TABLE_NOTE="$simulate_note" \
    LHCI_BUILD_CONTEXT__CURRENT_BRANCH="$branch-simulated" LHCI_BASE_BRANCH="$base-simulated" \
    node summary.mjs report /out/simulate.json || exit 1
} > /out/summary.md || exit 1

exit $status
