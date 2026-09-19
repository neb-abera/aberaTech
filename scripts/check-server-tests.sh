#!/usr/bin/env bash
#
# check-server-tests.sh — the servertest stage's verdict, read from what the
# run wrote rather than from its exit code alone.
#
#   check-server-tests.sh <test log> <cobertura xml> <deferred count> <minimum line %>
#   check-server-tests.sh --self-test
#
# The hermetic servertest image stage runs inside `docker build`, where no
# database is reachable. It used to run every test and let the [PostgresFact]
# tests skip themselves — and a test runner counts a skip as not-a-failure, so
# a stray `Skip = "..."` on any other test, or a misconfiguration that skipped
# a whole class, would have passed the gate behind the skips that were
# expected. The stage now excludes the Postgres tests by their trait
# (Category=Postgres, the same filter scripts/server-db-tests.sh selects them
# with), counts how many it left to that script, and tolerates no skip at all.
# One line carries every number a reader wants, and this exits non-zero when:
#
#   - the runner's summary is missing, nothing passed, or anything failed;
#   - anything was skipped (a skip in this stage has no legitimate cause);
#   - no test was deferred (the trait selects nothing, so either the Postgres
#     tests are running here and skipping, or server-db-tests.sh runs nothing);
#   - line coverage is below the floor, or was not collected.
#
# --self-test feeds it a fixture for each of those and one clean run, and
# fails if any verdict is wrong. The Dockerfile runs it before the real one,
# every time, so a checker that has stopped failing is caught there.
set -euo pipefail

usage() {
  echo "usage: $0 <test log> <cobertura xml> <deferred count> <minimum line %> | --self-test" >&2
}

# One number from the runner's summary line:
# "Passed!  - Failed:     0, Passed:   766, Skipped:     0, Total:   766, Duration: 28 s".
count() {
  grep -Eo "$2:[[:space:]]+[0-9]+" "$1" | grep -Eo '[0-9]+' | tail -1 || true
}

verdict() {
  local log=$1 report=$2 deferred=$3 minimum=$4
  local passed failed skipped rate percent status=0

  passed="$(count "$log" Passed)"
  failed="$(count "$log" Failed)"
  skipped="$(count "$log" Skipped)"
  rate="$(sed -n 's/.*<coverage[^>]*line-rate="\([0-9.]*\)".*/\1/p' "$report" 2>/dev/null | head -1 || true)"

  if [ -z "$passed" ]; then
    echo "error: no test summary in $log; the run did not finish" >&2
    return 1
  fi
  if [ -z "$rate" ]; then
    echo "error: no line-rate in $report; coverage was not collected" >&2
    return 1
  fi

  percent="$(awk -v r="$rate" 'BEGIN { printf "%.1f", r * 100 }')"
  printf 'server line coverage: %s%% (minimum %s%%); tests: %s passed, %s failed, %s skipped; %s [PostgresFact] tests deferred to the compose database (scripts/server-db-tests.sh)\n' \
    "$percent" "$minimum" "$passed" "${failed:-0}" "${skipped:-0}" "$deferred"

  if [ "$passed" -eq 0 ]; then
    echo "error: no test passed" >&2
    status=1
  fi
  if [ "${failed:-0}" -ne 0 ]; then
    echo "error: ${failed} test(s) failed" >&2
    status=1
  fi
  if [ "${skipped:-0}" -ne 0 ]; then
    echo "error: ${skipped} test(s) skipped; this stage tolerates none (a [PostgresFact] is excluded by its trait, not skipped)" >&2
    status=1
  fi
  if ! [ "$deferred" -gt 0 ] 2> /dev/null; then
    echo "error: no [PostgresFact] test was deferred; the Category=Postgres trait selects nothing" >&2
    status=1
  fi
  if ! awk -v p="$percent" -v m="$minimum" 'BEGIN { exit (p + 0 >= m + 0) ? 0 : 1 }'; then
    echo "error: line coverage ${percent}% is below the ${minimum}% floor" >&2
    status=1
  fi
  return "$status"
}

self_test() {
  local dir
  dir="$(mktemp -d)"
  trap 'rm -rf "$dir"' RETURN

  printf 'Passed!  - Failed:     0, Passed:   766, Skipped:     0, Total:   766, Duration: 28 s\n' > "$dir/clean.log"
  printf 'Passed!  - Failed:     0, Passed:   766, Skipped:     1, Total:   767, Duration: 28 s\n' > "$dir/skipped.log"
  printf 'Failed!  - Failed:     2, Passed:   764, Skipped:     0, Total:   766, Duration: 28 s\n' > "$dir/failed.log"
  printf 'Passed!  - Failed:     0, Passed:     0, Skipped:     0, Total:     0, Duration: 1 s\n' > "$dir/zero.log"
  printf 'No test matches the given testcase filter\n' > "$dir/none.log"
  printf '<?xml version="1.0"?>\n<coverage line-rate="0.714" branch-rate="0.5"></coverage>\n' > "$dir/report.xml"
  printf '<?xml version="1.0"?>\n<coverage line-rate="0.42" branch-rate="0.5"></coverage>\n' > "$dir/low.xml"

  local failures=0
  expect() { # <expected exit> <what> <verdict arguments...>
    local want=$1 what=$2 got=0
    shift 2
    verdict "$@" > /dev/null 2>&1 || got=$?
    if [ "$got" -ne "$want" ]; then
      echo "self-test: $what: expected exit $want, got $got" >&2
      failures=$((failures + 1))
    else
      echo "self-test: $what: exit $got, as it should be"
    fi
  }

  expect 0 "a clean run above the floor" "$dir/clean.log" "$dir/report.xml" 10 60
  expect 1 "one skipped test" "$dir/skipped.log" "$dir/report.xml" 10 60
  expect 1 "a failed test" "$dir/failed.log" "$dir/report.xml" 10 60
  expect 1 "nothing passed" "$dir/zero.log" "$dir/report.xml" 10 60
  expect 1 "no summary at all" "$dir/none.log" "$dir/report.xml" 10 60
  expect 1 "nothing deferred" "$dir/clean.log" "$dir/report.xml" 0 60
  expect 1 "coverage below the floor" "$dir/clean.log" "$dir/low.xml" 10 60
  expect 1 "coverage one tenth under the floor" "$dir/clean.log" "$dir/report.xml" 10 71.5
  expect 0 "coverage exactly at the floor" "$dir/clean.log" "$dir/report.xml" 10 71.4
  expect 1 "no coverage report" "$dir/clean.log" "$dir/missing.xml" 10 60

  if [ "$failures" -ne 0 ]; then
    echo "self-test: $failures wrong verdict(s); this checker cannot be trusted" >&2
    return 1
  fi
  echo "self-test: the checker fails when it should"
}

case "${1:-}" in
  --self-test)
    self_test
    ;;
  "")
    usage
    exit 2
    ;;
  *)
    if [ $# -ne 4 ]; then
      usage
      exit 2
    fi
    verdict "$@"
    ;;
esac
