#!/usr/bin/env bash
#
# check-required-contexts.sh — assert that .github/required-contexts.txt
# (the in-repo mirror of the master ruleset's required status checks)
# matches the PR-gating job names in the three workflows that run on every
# pull request:
#
#   .github/workflows/client-checks.yml
#   .github/workflows/codeql.yml
#   .github/workflows/security-scan.yml
#
# A required-status-check context is the job's `name:`. A job that runs on
# PRs but is missing from the list does not block a merge — a red scan
# would not stop Dependabot auto-merge. A context with no matching job
# blocks every merge forever, because GitHub waits for a check that never
# reports. Both directions fail this script.
#
# It also asserts each of those workflows triggers on a bare, unfiltered
# `pull_request:` — a branches filter is how the repo once ended up with
# stacked PRs that could never merge (required analyze(...) contexts never
# started).
#
# Deliberately dependency-light (bash + sed/grep): job names are the
# 4-space-indented `name:` lines, and the one matrix variable used in job
# names (`${{ matrix.language }}`) is expanded from the matrix list. Run by
# the lint job in client-checks.yml; exits 0 only when the sets match.

set -euo pipefail
cd "$(dirname "$0")/.."

CONTEXTS_FILE=.github/required-contexts.txt
WORKFLOWS=(
  .github/workflows/client-checks.yml
  .github/workflows/codeql.yml
  .github/workflows/security-scan.yml
)

[ -f "$CONTEXTS_FILE" ] || { echo "error: $CONTEXTS_FILE not found" >&2; exit 1; }
required="$(grep -v '^[[:space:]]*#' "$CONTEXTS_FILE" | grep -v '^[[:space:]]*$')"
[ -n "$required" ] || { echo "error: no contexts listed in $CONTEXTS_FILE" >&2; exit 1; }

# The job names of every workflow that gates PRs.
actual=""
for wf in "${WORKFLOWS[@]}"; do
  [ -f "$wf" ] || { echo "error: $wf not found" >&2; exit 1; }
  # Each workflow must trigger on every pull request, unfiltered: a bare
  # `pull_request:` in the on: block. A filtered trigger would let some PRs
  # merge with the required check permanently pending.
  if ! grep -q '^  pull_request:$' "$wf"; then
    echo "error: $wf has no unfiltered 'pull_request:' trigger" >&2
    exit 1
  fi
  names="$(sed -n 's/^    name: //p' "$wf")"
  langs="$(sed -n 's/.*language: \[\(.*\)\].*/\1/p' "$wf" | tr -d ' ' | tr ',' '\n')"
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    # shellcheck disable=SC2016 # literal '${{ matrix.language }}', not expansion
    if [ "${name#*'${{ matrix.language }}'}" != "$name" ]; then
      [ -n "$langs" ] || { echo "error: $wf uses matrix.language in a job name but has no matrix list" >&2; exit 1; }
      while IFS= read -r lang; do
        # shellcheck disable=SC2016 # literal '${{ matrix.language }}', not expansion
        actual="$actual${name//'${{ matrix.language }}'/$lang}"$'\n'
      done <<< "$langs"
    else
      actual="$actual$name"$'\n'
    fi
  done <<< "$names"
done

required_sorted="$(printf '%s\n' "$required" | sort)"
actual_sorted="$(printf '%s' "$actual" | sort)"

missing_from_list="$(comm -13 <(printf '%s\n' "$required_sorted") <(printf '%s\n' "$actual_sorted"))"
missing_from_workflows="$(comm -23 <(printf '%s\n' "$required_sorted") <(printf '%s\n' "$actual_sorted"))"

status=0
if [ -n "$missing_from_list" ]; then
  echo "PR-gating jobs NOT in $CONTEXTS_FILE (a red run would not block a merge):" >&2
  printf '%s\n' "$missing_from_list" | sed 's/^/  - /' >&2
  status=1
fi
if [ -n "$missing_from_workflows" ]; then
  echo "Contexts in $CONTEXTS_FILE with no matching PR-gating job (would block every merge):" >&2
  printf '%s\n' "$missing_from_workflows" | sed 's/^/  - /' >&2
  status=1
fi

if [ "$status" -eq 0 ]; then
  count="$(printf '%s\n' "$required_sorted" | wc -l | tr -d ' ')"
  echo "required contexts and PR-gating job names agree ($count checks):"
  printf '%s\n' "$required_sorted" | sed 's/^/  - /'
fi
exit "$status"
