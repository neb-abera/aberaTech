# Security Policy

## Supported Versions

Only the current production deployment (built from `master`) receives
security updates.

## Reporting a Vulnerability

Please report vulnerabilities privately via
[GitHub's private vulnerability reporting](../../security/advisories/new)
rather than opening a public issue. You should receive a response within a
week. Please include a proof of concept or reproduction steps where possible.

## How this repository searches for vulnerabilities

* CodeQL static analysis (C#, JavaScript/TypeScript, workflows) on every
  pull request and weekly
* trivy scans the production image for fixable HIGH/CRITICAL CVEs on every
  pull request and weekly (`security-scan.yml`)
* Dependabot alerts, security updates and weekly version updates across
  nuget, npm, docker and actions
* dependency review blocks pull requests that introduce high-severity
  vulnerable dependencies
* OpenSSF Scorecard grades the repository's supply-chain posture weekly
* secret scanning with push protection
