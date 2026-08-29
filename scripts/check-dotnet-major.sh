#!/usr/bin/env bash
#
# check-dotnet-major.sh — detect a newer GA .NET major and rewrite every
# version site that must move in lockstep with it:
#
#   1. <TargetFramework> in every .csproj
#   2. the dotnet/sdk and dotnet/aspnet base images in
#      aberaTech.Server/Dockerfile
#   3. every PackageReference whose version tracks the framework major
#      (Microsoft.AspNetCore.*, Microsoft.EntityFrameworkCore.*, the Npgsql
#      EF providers — anything currently versioned <major>.x)
#
# Dependabot keeps everything current within a major but never crosses one,
# because the TargetFramework gates it; this script makes the cross-major
# jump. Run monthly by .github/workflows/dotnet-major-upgrade.yml, and safe
# to run locally: it only edits files, never commits.
#
# Environment:
#   SUMMARY_FILE  optional path; a Markdown summary (used as the PR body)
#                 is written there.
#
# Exits 0 whether or not changes were made; non-zero only on failure.

set -euo pipefail
cd "$(dirname "$0")/.."

RELEASES_INDEX_URL="https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json"
SUMMARY_FILE="${SUMMARY_FILE:-/dev/null}"

replace() { perl -pi -e "$2" "$1"; }

current="$(sed -n 's/.*<TargetFramework>net\([0-9][0-9.]*\)<.*/\1/p' aberaTech.Server/aberaTech.Server.csproj)"
[ -n "$current" ] || { echo "error: could not read TargetFramework" >&2; exit 1; }

latest="$(curl -fsSL "$RELEASES_INDEX_URL" | jq -r '
  ."releases-index"
  | map(select(."support-phase" == "active" or ."support-phase" == "maintenance"))
  | max_by(."channel-version" | split(".") | map(tonumber))
  | ."channel-version"')"
[ -n "$latest" ] && [ "$latest" != "null" ] || { echo "error: could not determine latest GA .NET version" >&2; exit 1; }

cur_major="${current%%.*}"
new_major="${latest%%.*}"

if [ "$new_major" -le "$cur_major" ]; then
  echo "net${current} is the latest GA major (index says ${latest}); nothing to do."
  echo "Already on the latest GA .NET major (net${current})." > "$SUMMARY_FILE"
  exit 0
fi

echo "GA .NET ${latest} is out; currently on net${current}. Rewriting the lockstep sites."

projects="$(git ls-files '*.csproj')"
for p in $projects; do
  replace "$p" "s|<TargetFramework>net\Q${current}\E<|<TargetFramework>net${latest}<|"
done

replace aberaTech.Server/Dockerfile "s{dotnet/(sdk|aspnet):\Q${current}\E}{dotnet/\${1}:${latest}}g"

# Framework-tracking packages: anything versioned <cur_major>.x moves to the
# latest stable of the new major. Packages without one yet are left alone
# and called out in the summary.
pending=""
bumped=""
while read -r pkg; do
  lower="$(echo "$pkg" | tr '[:upper:]' '[:lower:]')"
  new_ver="$(curl -fsSL "https://api.nuget.org/v3-flatcontainer/${lower}/index.json" \
    | jq -r --arg m "${new_major}." '.versions | map(select(startswith($m) and (contains("-") | not))) | last // empty')"
  if [ -n "$new_ver" ]; then
    for p in $projects; do
      replace "$p" "s|(Include=\"\Q${pkg}\E\" Version=\")\Q${cur_major}\E\.[^\"]+|\${1}${new_ver}|"
    done
    bumped="${bumped}- \`${pkg}\` → ${new_ver}\n"
  else
    pending="${pending}- \`${pkg}\` has no stable ${new_major}.x release yet\n"
  fi
done < <(cat $projects | sed -n "s/.*PackageReference Include=\"\([^\"]*\)\" Version=\"${cur_major}\..*/\1/p" | sort -u)

{
  echo "Moves the repo from **net${current}** to **net${latest}**, the latest GA .NET major."
  echo
  echo "Every lockstep site moves together:"
  echo
  echo "- \`<TargetFramework>\` in every .csproj"
  echo "- \`dotnet/sdk:${latest}\` and \`dotnet/aspnet:${latest}\` in aberaTech.Server/Dockerfile"
  printf '%b' "$bumped"
  if [ -n "$pending" ]; then
    echo
    echo "Left for a human (re-run the workflow once these ship):"
    printf '%b' "$pending"
  fi
  echo
  echo "Review the [breaking changes for .NET ${new_major}](https://learn.microsoft.com/dotnet/core/compatibility/${latest}) before merging."
  echo
  echo "Opened by \`dotnet-major-upgrade.yml\`. CI does not run automatically on PRs opened with the workflow token — close and reopen this PR (or push an empty commit) to run the checks."
} > "$SUMMARY_FILE"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "new-version=${latest}" >> "$GITHUB_OUTPUT"
fi

echo "done: net${current} -> net${latest}"
