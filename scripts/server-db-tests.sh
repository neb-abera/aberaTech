#!/usr/bin/env bash
#
# The server tests that need a real Postgres, run against the compose one.
#
# The hermetic servertest image stage runs inside `docker build`, where no
# database is reachable, so it skips every test marked [PostgresFact]. This is
# where they run instead: `make dbtest` locally, and the "Compose database
# boots healthy" job in CI, which already has the database up.
#
# A test runner that matched nothing exits 0, and so does one whose tests all
# skipped. Either would make this script an advisory check wearing a costume,
# so it reads the summary and fails unless something actually passed and
# nothing was skipped. Prove the failure path with:
#
#     DBTEST_FILTER='Category=NoSuchCategory' ./scripts/server-db-tests.sh
set -euo pipefail

cd "$(dirname "$0")/.."

filter="${DBTEST_FILTER:-Category=Postgres}"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT

docker compose build servertest

docker compose run --rm servertest \
  dotnet test aberaTech.Server.Tests/aberaTech.Server.Tests.csproj \
  --nologo -p:SkipClientProject=true --filter "$filter" 2>&1 | tee "$log"

count() {
  grep -Eo "$1:[[:space:]]+[0-9]+" "$log" | grep -Eo '[0-9]+' | tail -1 || true
}

passed="$(count Passed)"
skipped="$(count Skipped)"

if [ "${passed:-0}" -eq 0 ]; then
  echo "error: no database-backed test ran (filter: ${filter})." >&2
  exit 1
fi

if [ "${skipped:-0}" -ne 0 ]; then
  echo "error: ${skipped} database-backed test(s) skipped; the compose database was promised here." >&2
  exit 1
fi

echo "database-backed server tests: ${passed} passed"
