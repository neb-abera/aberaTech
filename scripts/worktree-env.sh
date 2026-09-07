#!/usr/bin/env bash
#
# Give this working copy its own compose ports.
#
# Several sessions work on this repository at once, each in its own git
# worktree, and every compose service here publishes a host port. Two copies on
# one port is a bind failure at best; at worst the second copy fails to start
# and its owner then browses the first one's build, believing it to be theirs.
# That has happened.
#
# Compose reads .env from the project directory on every invocation, so the
# ports land whether the caller went through the Makefile or ran
# `docker compose` by hand. The values are derived from the directory name —
# the same thing that already makes the compose project unique — so they are
# stable across restarts rather than handed out by a counter that would need
# somewhere to live.
#
# Writes nothing if .env already exists: an override typed by hand outlives
# this script.
set -euo pipefail

cd "$(dirname "$0")/.."

if [ -e .env ]; then
  exit 0
fi

worktree="$(basename "$PWD")"

# A linked worktree has `.git` as a file pointing at the real one; the main
# checkout has it as a directory. Testing that rather than the directory name
# keeps this true of a clone under any name.
if [ -d .git ]; then
  offset=0
else
  offset="$(printf '%s' "$worktree" | cksum | awk '{print ($1 % 300) + 1}')"
fi

cat > .env <<ENV
# Written by scripts/worktree-env.sh, and ignored by git: these numbers belong
# to this working copy alone.
#
# Change them freely — the script leaves an existing file alone. Delete the
# file to have it derived again.
APP_PORT=$((8080 + offset))
DEV_PORT=$((3000 + offset))
DB_PORT=$((5433 + offset))
ENV
