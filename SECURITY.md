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
* a held-majors check on every pull request fails when an npm dependency's
  next major cannot install or a NuGet dependency's next major ships no
  framework the project can consume, the two cases Dependabot stays silent
  about (accepted cases live in `.held-majors` with their reasoning)
* dependency review blocks pull requests that introduce high-severity
  vulnerable dependencies
* OpenSSF Scorecard grades the repository's supply-chain posture weekly
* secret scanning with push protection

## Security events in the application log

The server writes one structured log entry, in the category
`aberaTech.Security`, whenever it refuses something a stranger might be
probing (`aberaTech.Server/SecurityEvents.cs`). Each entry carries the
resolved client address (`ClientIp`, see `ClientAddress.cs`), the `Method`,
the `Route` *pattern* and the `Status` — and nothing else. It never carries
a path (queue and booking paths contain the capability id), a query string,
a header, a cookie, a key, a phone number, an email address or the
signed-in account. The event ids are stable, so alerts can be written
against them; none are configured yet.

| EventId | Name | Level | Meaning |
|---|---|---|---|
| 4001 | `RateLimited` | Warning | 429: a rate limit, or the failed-guess limit on the digest key or the Twilio webhook, refused the request |
| 4002 | `SignInRequired` | Information | 401 from a route behind Google sign-in: no session, or an expired one |
| 4003 | `AllowlistRefused` | Warning | 403 for a signed-in caller: a Google account that is not on the allowlist (the account is not named) |
| 4004 | `DigestKeyRejected` | Warning | 401 from `/api/fitness/digest.txt`: a missing or wrong bearer key |
| 4005 | `WebhookSignatureRejected` | Warning | 403 from `/api/scheduling/sms-status`: a missing or wrong Twilio signature |
| 4006 | `UnknownCapability` | Warning | 404 from a queue-place or booking route: an id nobody was given, or a stale one |
| 4007 | `PublicWriteRefused` | Information | 400 from joining the queue or booking: input the form would not have sent |

In Application Insights these are `traces` rows; filter on
`customDimensions.EventId` or `customDimensions.CategoryName`.

## Hardening deliberately left for the owner

* **HSTS `preload`.** The header already sends `includeSubDomains`; `preload`
  is a one-way door that makes every present and future subdomain of
  `abera.tech` HTTPS-only in browsers' shipped lists. Add it only after
  confirming that is true of every subdomain, then submit at hstspreload.org.
* **CSP `report-to` / `report-uri`.** There is no report sink today, and a
  reporting endpoint on this origin would be one more unauthenticated write.
  Add the directive when a sink exists (Cloudflare's or a hosted collector).
* **Host filtering** is `HostAllowlist` in `appsettings.Production.json`, not
  the framework's `AllowedHosts`, so that `/healthz` and `/readyz` keep
  answering a platform probe that addresses the container by IP. The list
  names the container app's own default domain exactly, never
  `*.azurecontainerapps.io`, and an empty list refuses to start outside
  Development: recreating the app or its environment changes that domain,
  and the list goes with it.
