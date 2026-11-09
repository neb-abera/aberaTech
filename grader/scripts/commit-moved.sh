#!/usr/bin/env bash
#
# commit-moved.sh <path>...: commit the files under <path> that changed in the
# working tree onto $BRANCH, signed by GitHub, through the GraphQL API. Used
# by .github/workflows/grader-toolchain.yml on Dependabot branches, with
# GH_TOKEN, REPO, BRANCH, HEAD_SHA, HEADLINE, and BODY in the environment.
# Exits 0 with nothing to commit.

set -euo pipefail

changed="$(git status --porcelain -- "$@" | awk '{print $2}')"
if [ -z "$changed" ]; then
  echo "Nothing moved; nothing to commit."
  exit 0
fi
echo "Files that moved:"
echo "$changed"
if [ -z "${GH_TOKEN:-}" ]; then
  echo "::error::DEPENDABOT_AUTOMERGE_TOKEN is empty, so the moved files cannot be committed. Store it as a Dependabot secret (Settings, Secrets and variables, Dependabot) with contents:write."
  exit 1
fi
additions="$(for f in $changed; do
  jq -n --arg path "$f" --arg contents "$(base64 < "$f" | tr -d '\n')" '{path: $path, contents: $contents}'
done | jq -s .)"
jq -n \
  --arg repo "$REPO" --arg branch "$BRANCH" --arg head "$HEAD_SHA" \
  --arg headline "$HEADLINE" --arg body "$BODY" \
  --argjson additions "$additions" \
  '{
    query: "mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid url } } }",
    variables: { input: {
      branch: { repositoryNameWithOwner: $repo, branchName: $branch },
      expectedHeadOid: $head,
      message: { headline: $headline, body: $body },
      fileChanges: { additions: $additions }
    } }
  }' > "${RUNNER_TEMP:-/tmp}/commit.json"
gh api graphql --input "${RUNNER_TEMP:-/tmp}/commit.json" --jq '.data.createCommitOnBranch.commit.url'
