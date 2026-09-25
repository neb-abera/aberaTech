# The browser image `make e2e` runs the suite in. Nothing builds this file:
# the Makefile reads the image from the FROM line, and Dependabot reads the
# same line to bump it. The tag must match @playwright/test in package.json.
# Dependabot bumps both in one pull request (the playwright group in
# .github/dependabot.yml), and scripts/check-playwright-image.sh fails when
# they differ.
FROM mcr.microsoft.com/playwright:v1.63.0-noble@sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27 AS e2e
