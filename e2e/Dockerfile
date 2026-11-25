# The browser image `make e2e` runs the suite in. Nothing builds this file:
# the Makefile reads the image from the FROM line, and Dependabot reads the
# same line to bump it. The tag must match @playwright/test in package.json.
# Dependabot bumps both in one pull request (the playwright group in
# .github/dependabot.yml), and scripts/check-playwright-image.sh fails when
# they differ.
FROM mcr.microsoft.com/playwright:v1.65.1-resolute@sha256:dcf385f7a31aa1cbb0a7a0c97a539142bf70480b661f89f26a774ec1fe2edd1d AS e2e
