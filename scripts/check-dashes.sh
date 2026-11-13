#!/usr/bin/env bash
#
# check-dashes.sh: dashes used as punctuation in the code's own text.
#
#   scripts/check-dashes.sh               the paths below
#   scripts/check-dashes.sh --self-test   prove it fails and passes
#
# Neb's writing rules allow no dashes: one claim per sentence, full stops.
# Vale's rules read the Markdown and the prerendered pages. The owner pages
# are not prerendered, so their strings, the server's messages, and the
# comments are read here. An em dash, or an en dash with a space on each
# side, is a finding. A hyphen in a word ("2-mile") and an unspaced range
# ("5–10") are not.
#
# The fitness feature was cleaned on 2026-11-13. Another part of the repo
# joins this list in the pull request that cleans it.

set -euo pipefail
cd "$(dirname "$0")/.."

PATHS=(
  aberatech.client/src/features/fitness
  aberaTech.Fitness
  aberaTech.Server.Tests/Fitness
)
EXCLUDE=(':!*/Migrations/*' ':!*.json')
# The last form is a JSX dash before a {" "} that renders as a spaced dash.
PATTERN='—| – |–\{" "\}'

if [ "${1:-}" = "--self-test" ]; then
  grep -q -E "$PATTERN" <<<'the anchor — a time trial' \
    || { echo "self-test: an em dash was not caught" >&2; exit 1; }
  grep -q -E "$PATTERN" <<<'12:30 – 13:10' \
    || { echo "self-test: a spaced en dash was not caught" >&2; exit 1; }
  grep -q -E "$PATTERN" <<<'{low} –{" "}' \
    || { echo "self-test: a JSX spaced en dash was not caught" >&2; exit 1; }
  if grep -q -E "$PATTERN" <<<'a 2-mile run, 5–10 beats, 12:30 to 13:10'; then
    echo "self-test: a hyphen or an unspaced range was flagged" >&2; exit 1
  fi
  echo "self-test passed: dashes are caught, hyphens and ranges pass"
  exit 0
fi

found="$(git grep -n -E "$PATTERN" -- "${PATHS[@]}" "${EXCLUDE[@]}" || true)"
if [ -n "$found" ]; then
  printf '%s\n' "$found"
  echo "A dash in the text above. Write it as separate sentences." >&2
  exit 1
fi
echo "Dashes: none in ${PATHS[*]}."
