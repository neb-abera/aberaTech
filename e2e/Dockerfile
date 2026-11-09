# The browser image `make e2e` runs the suite in. Nothing builds this file:
# the Makefile reads the image from the FROM line, and Dependabot reads the
# same line to bump it. The tag must match @playwright/test in package.json.
# Dependabot bumps both in one pull request (the playwright group in
# .github/dependabot.yml), and scripts/check-playwright-image.sh fails when
# they differ.
FROM mcr.microsoft.com/playwright:v1.64.1-resolute@sha256:f76885029244e5a384243a930ff283cef67d90653978ee27a77dd4cff8882fb4 AS e2e
