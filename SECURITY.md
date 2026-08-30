# Security Policy

## Supported Versions

Only the current production deployment (built from `master`) receives
security updates.

## Reporting a Vulnerability

Please report vulnerabilities privately via
[GitHub's private vulnerability reporting](https://github.com/neb-abera/aberaTech/security/advisories/new)
rather than opening a public issue. If you cannot use GitHub's flow, email
<support@alias.abera.tech> instead. Please include a proof of concept or
reproduction steps where possible.

What to expect:

* an acknowledgement within 7 days,
* coordinated disclosure: we ask that you keep the report private until a
  fix is deployed, and we will credit you in the advisory unless you prefer
  otherwise,
* a fix, or a status update explaining what is taking longer, within 90
  days of the report.

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
