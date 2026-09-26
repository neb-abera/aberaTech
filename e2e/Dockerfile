# The browser image `make e2e` runs the suite in. Nothing builds this file:
# the Makefile reads the image from the FROM line, and Dependabot reads the
# same line to bump it. The tag must match @playwright/test in package.json.
# Dependabot bumps both in one pull request (the playwright group in
# .github/dependabot.yml), and scripts/check-playwright-image.sh fails when
# they differ.
FROM mcr.microsoft.com/playwright:v1.63.0-resolute@sha256:b022639ae9197f864040f92eef7b57c6d4b47db2190f77c909d8a5d902dd4b7e AS e2e
