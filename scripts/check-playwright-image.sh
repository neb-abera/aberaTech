#!/usr/bin/env bash
#
# check-playwright-image.sh: the browser image and the runner are one version.
#
#   check-playwright-image.sh [<e2e directory>]
#   check-playwright-image.sh --self-test
#
# e2e/Dockerfile pins the Playwright image by tag and digest, where Dependabot
# can bump it. e2e/package.json pins @playwright/test. A runner newer or older
# than the browsers in the image fails every test with a missing executable.
# Dependabot bumps both in one pull request (the playwright group in
# .github/dependabot.yml). This fails when they differ or when the image
# carries no digest.
set -euo pipefail

check() {
  local dir=$1 image version tag
  image="$(sed -n 's/^FROM \([^ ]*\) AS e2e$/\1/p' "$dir/Dockerfile" | head -1)"
  version="$(sed -n 's|.*"@playwright/test": "\([^"]*\)".*|\1|p' "$dir/package.json" | head -1)"
  if [ -z "$image" ]; then
    echo "error: no 'FROM <image> AS e2e' line in $dir/Dockerfile" >&2
    return 1
  fi
  if [ -z "$version" ]; then
    echo "error: no @playwright/test version in $dir/package.json" >&2
    return 1
  fi
  case "$image" in
    *@sha256:*) ;;
    *)
      echo "error: $image is not pinned by digest" >&2
      return 1
      ;;
  esac
  tag="${image%%@*}"
  tag="${tag##*:}"
  # v<version>-<Ubuntu codename>: the codename may move, the version may not.
  if ! [[ "$tag" =~ ^v${version//./\\.}-[a-z]+$ ]]; then
    echo "error: image tag $tag does not match @playwright/test ${version} (want v${version}-<codename>)" >&2
    return 1
  fi
  echo "playwright image matches @playwright/test ${version}"
}

self_test() {
  local tmp status=0 bad
  local digest="sha256:0000000000000000000000000000000000000000000000000000000000000000"
  tmp="$(mktemp -d)"
  # shellcheck disable=SC2064 # expand $tmp now, while it is set
  trap "rm -rf '$tmp'" EXIT

  fixture() {
    mkdir -p "$tmp/$1"
    printf 'FROM %s\n' "$2" > "$tmp/$1/Dockerfile"
    printf '{ "devDependencies": { "@playwright/test": "%s" } }\n' "$3" > "$tmp/$1/package.json"
  }
  fixture match "mcr.microsoft.com/playwright:v1.2.3-resolute@$digest AS e2e" 1.2.3
  fixture drift "mcr.microsoft.com/playwright:v1.2.4-resolute@$digest AS e2e" 1.2.3
  fixture prefix "mcr.microsoft.com/playwright:v1.2.30-resolute@$digest AS e2e" 1.2.3
  fixture nodigest "mcr.microsoft.com/playwright:v1.2.3-resolute AS e2e" 1.2.3
  fixture nostage "mcr.microsoft.com/playwright:v1.2.3-resolute@$digest" 1.2.3

  if ! check "$tmp/match" > /dev/null 2>&1; then
    echo "self-test: the matching pair failed" >&2
    status=1
  fi
  for bad in drift prefix nodigest nostage; do
    if check "$tmp/$bad" > /dev/null 2>&1; then
      echo "self-test: the $bad fixture passed" >&2
      status=1
    fi
  done
  if [ "$status" -eq 0 ]; then
    echo "check-playwright-image self-test: 5 of 5 verdicts correct"
  fi
  return "$status"
}

case "${1:-}" in
  --self-test) self_test ;;
  *) check "${1:-$(dirname "$0")/../e2e}" ;;
esac
