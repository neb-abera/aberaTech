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

* an acknowledgement within 7 days.
* coordinated disclosure: we ask that you keep the report private until a
  fix is deployed, and we will credit you in the advisory unless you prefer
  otherwise.
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
the `Route` *pattern* and the `Status`, and nothing else. It never carries
a path (queue and booking paths contain the capability id), a query string,
a header, a cookie, a key, a phone number, an email address or the
signed-in account. The event ids are stable, so alerts can be written
against them. None are configured yet.

| EventId | Name | Level | Meaning |
|---|---|---|---|
| 4001 | `RateLimited` | Warning | 429: a rate limit, or the failed-guess limit on the digest key or the Twilio webhook, refused the request |
| 4002 | `SignInRequired` | Information | 401 from a route behind Google sign-in: no session, or an expired one |
| 4003 | `AllowlistRefused` | Warning | 403 for a signed-in caller: a Google account that is not on the allowlist (the account is not named) |
| 4004 | `DigestKeyRejected` | Warning | 401 from `/api/fitness/digest.txt`: a missing or wrong bearer key |
| 4005 | `WebhookSignatureRejected` | Warning | 403 from `/api/scheduling/sms-status`: a missing or wrong Twilio signature |
| 4006 | `UnknownCapability` | Warning | 404 from a queue-place or booking route: an id nobody was given, or a stale one |
| 4007 | `PublicWriteRefused` | Information | 400 from joining the queue or booking: input the form would not have sent |
| 4008 | `AgentTokenRejected` | Warning | 401 from `/api/devbox/heartbeat`: a missing or wrong dev box agent token |

In Application Insights these are `traces` rows. Filter on
`customDimensions.EventId` or `customDimensions.CategoryName`.

## Standards this repository is checked against

The gates above are the machinery. This table says which published
control each one answers, so a reviewer with the standard in hand can
find the evidence. A control with no machinery is written down as a
deviation. The application STIG is DISA's
Application Security and Development STIG V6R4 (2025-09-09). The
identity controls are NIST SP 800-63B. The host running the dev box is
checked separately against the Ubuntu 24.04 STIG (see
repos-conventions, `devbox/README.md`).

| Control | Requirement | Here | Evidence |
|---|---|---|---|
| ASD V-222425, V-222426 | Enforce approved authorizations per object | Met | Every endpoint declares its policy or `AllowAnonymous`. `RouteTableTests` fails the build on one that declares nothing. Owner documents and the fitness console are per-user |
| ASD V-222430 | Execute without excessive permissions | Met | Non-root chiseled image. The runtime database role has DML only (`aberaTech.Postgres/Sql`). The container app's identity holds one custom role on one VM (`DevBox/`) |
| ASD V-222432, V-222433 | Lock out after three invalid logons in 15 minutes | Not applicable | There is no password. Sign-in is Google OIDC. Google enforces its own lockout. The sign-in start is rate limited (10 a minute per address, `RateLimits.cs`) |
| ASD V-222536 to V-222548 | Password composition and lifetime | Not applicable | No passwords are stored or checked by this application (SP 800-63B §5.1.1 does not apply. The verifier is Google) |
| ASD V-222522, V-222526 | Unique identification, multifactor for network access | Met by the identity provider | Google account with the owner's second step. The allowlist names the account (`AdminOptions.AllowedEmails`) |
| ASD V-222575, V-222576, V-222577, V-222578, V-222581, V-222583 | Session cookies: HttpOnly, Secure, not exposed, destroyed on logoff, never in URLs, random ids | Met | `__Host-abera.admin`: `HttpOnly`, `Secure`, `SameSite=Strict`, no `Domain`. Sign-out clears it. The ticket is a data-protected blob, not a lookup id (`AdminAuth.cs`). `AdminRouteTests` pin the flags |
| ASD V-222389, V-222390 | Terminate sessions after 15 minutes (users) and 10 minutes (admins) of idle time | Deviation, accepted | The owner's cookie slides on use and expires after 12 hours. One owner, second factor at the identity provider, cookie flags above. A 10-minute idle limit on a page that is edited in place is the reason the deviation is accepted. Revisit if a second account is ever allowed |
| ASD V-222387 | Limit concurrent sessions per user | Deviation, accepted | Not enforced. The cookie is stateless. A second device is the owner's phone |
| ASD V-222441 to V-222449, V-222462 | Audit session events, logon attempts, with time, source address and outcome | Partly met | Refusals are events 4001 to 4008 with the resolved client address, method, route pattern and status (`SecurityEvents.cs`). Shipped to Application Insights. Successful sign-in and sign-out are not events yet: a deviation, tracked |
| ASD V-222444 | No sensitive data in logs | Met | Events carry no path, query, header, cookie, key, phone number, email or account. `SecurityEventLoggingTests` assert what is omitted |
| ASD V-222481, V-222482 | Off-load audit records to a central repository | Met | OpenTelemetry to Azure Monitor (`Program.cs`, `APPLICATIONINSIGHTS_CONNECTION_STRING`) |
| ASD V-222596, V-222597 | Protect transmitted information | Met | HTTPS only at the edge. HSTS with `includeSubDomains. Preload`. `upgrade-insecure-requests` |
| ASD V-222602 | Protect from XSS | Met | React escapes by default. CSP with no `unsafe-inline` for scripts, inline bootstrap allowed by hash only (`CspInlineScripts.cs`). ZAP baseline on every push |
| ASD V-222603 | Protect from CSRF | Met | `SameSite=Strict` cookie. JSON bodies only. No cross-origin writes. No CORS |
| ASD V-222604, V-222607, V-222608, V-222609, V-222612 | Command injection, SQL injection, XML attacks, input handling, overflows | Met | EF Core parameterised queries. No shell, no XML parser. 64 KB global body limit and 256 KB for owner documents (`RequestLimits.cs`). The bookmark parser is property-tested under generated input (`features/links/core/__tests__/properties.test.ts`) and the heartbeat under seeded random bodies (`DevBoxDevelopmentTests`) |
| ASD V-222594, V-222667 | Restrict denial of service | Met | Rate limits on every unauthenticated write and on the agent channel. Failure-counting limits on key-guessing routes. Body limits. Cloudflare in front |
| ASD V-222610, V-222611 | Error messages reveal nothing exploitable | Met | Azure failures answer 502 with the exception type only. The framework's developer page is Development only |
| ASD V-222642 | No embedded authentication data | Met | Secrets are container app secrets. `DevBox__HeartbeatToken` is a secret reference. CodeQL and push protection on the repository |
| ASD V-222614, V-222658 | Patches current, products supported | Met | Dependabot on every ecosystem with auto-merge for non-majors. The held-majors gate (`scripts/check-held-majors.sh`) finds a bump Dependabot cannot offer. .NET and Node LTS |
| ASD V-222645 | Application files hashed before deployment | Met | Build provenance attestation and SBOM on every deploy (`aberatechserver-app-*.yml`) |
| ASD V-222648, V-222650 | Code review, flaws tracked | Met | Every change is a pull request with CodeQL, Trivy, ZAP, dependency review and Scorecard. Findings are issues or `.zap/rules.tsv` entries with reasons |
| ASD V-222655 | Threat model per release | Deviation, tracked | No written threat model. The application security baseline in the abera-standards skill is the closest artefact |
| ASD V-222515, V-222624 | Vulnerability assessment, active testing | Met | ZAP baseline DAST on every push, Trivy on the image, CodeQL, weekly schedules |
| SP 800-63B §7 | Session management: reauthentication, binding | Partly met | Bound to the browser by cookie. No reauthentication for sensitive operations. Starting or parking the dev box needs the session only |
| SP 800-63B §4.2 | AAL2 for the owner | Met by the identity provider | Google with a second factor |

Deviations, in order of value to close: audit events for successful
sign-in and sign-out (4009, 4010), then a written threat model, then a
shorter idle timeout if a second account is ever allowed.

## Hardening deliberately left for the owner

* **HSTS `preload`.** The header already sends `includeSubDomains`. `preload`
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
