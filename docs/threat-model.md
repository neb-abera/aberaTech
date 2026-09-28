# Threat model

What abera.tech protects, who it protects it from, and which gate holds
each answer. The application STIG (V-222655) asks for one per release.
Reviewing it is part of every release, and a new entry point is a row here
before it is a feature.

## Assets

- The owner's data: the scheduling queue with visitors' phone numbers, the
  fitness log, the owner documents (links, plan, study progress).
- The owner's session: the Google account on the allowlist and the cookie
  that carries it.
- The owner's calendar: its secret iCal address reads every event, and
  the Pushover keys send to his phone.
- The dev box: an Azure VM the site can start, and the agent channel that
  tells it to hold or park.
- The container image and the supply chain that builds and deploys it.
- Secrets on the container app: the Google client secret, the Twilio
  credentials, the agent token, the calendar address and the Pushover keys,
  the Cloudflare purge token in CI.

## Entry points and trust boundaries

| Boundary | What crosses it | Who is on the far side |
|---|---|---|
| Internet to Cloudflare | Every request, over TLS | Anyone |
| Cloudflare to the container app | Requests from Cloudflare's ranges, two trusted hops | The edge |
| Public forms | Join the queue, book a slot | Anyone with a phone |
| Google sign-in | An OIDC ticket for an allowlisted address | The owner, after Google's second factor |
| Twilio webhook | Signed delivery receipts | Twilio |
| The dev box agent | One heartbeat a minute with a bearer token | The box, or whoever holds the token |
| The site to Azure | Start the VM through the container app's identity | Azure Resource Manager |
| The site to Google Calendar | One GET of the secret iCal address every 5 minutes | Google |
| The site to Pushover | One POST per alarm or notification. An alarm at the alarm priority (2 by default, with retry and expire). A notification at priority 0 or 1, with neither. An event set to None sends nothing | Pushover |
| The site to Postgres | Parameterised queries as the runtime role, passwordless | The application |
| Internet to the Lighthouse CI server | Report uploads with a build token, dashboard reads, both behind basic auth, over TLS | CI, the nightly run, the owner, or whoever holds the password |

## Threats and answers

| Threat | Class | Answer | Gate |
|---|---|---|---|
| A stranger reaches an owner route | Elevation | Deny by default. Every endpoint declares its policy or `AllowAnonymous`. The owner is an allowlist, never "signed in" alone | `RouteTableTests`, the 401 and 403 theories on every owner route |
| A guessed queue id reads or cancels somebody's place | Elevation | The id is never returned to a duplicate request. Recovery goes through the phone | `UnknownCapability` events and the queue tests |
| A visitor's phone number leaks | Disclosure | Responses list their fields. Events never carry a number | `SecurityEventLoggingTests` assert what is omitted |
| A form wired to SMS spends money | Denial | Five public writes a minute per address, destinations limited to +1 | `RateLimits.cs` and its tests |
| A wrong Twilio signature is accepted | Spoofing | Signature check, 403, thirty failures a minute then refused unheard | `WebhookSignatureRejected` tests |
| The agent token is guessed or replayed | Spoofing | Fixed-time compare, ten wrong tokens a minute, ten reports a minute, event 4008 | `DevBoxRouteTests`, the seeded random bodies |
| A stolen agent token orders the box | Tampering | The reply can only hold or park. Start needs the owner's session and Azure's identity | The route table and the role scoped to one VM |
| The site's identity does more than start a VM | Elevation | One custom role, start and read, on one resource | The role definition in repos-conventions |
| A session cookie is stolen | Spoofing | `__Host-`, `Secure`, `HttpOnly`, `SameSite=Strict`. Sign-out bumps the account's session version, so every copy of the cookie is refused after it. Events 4009 and 4010 record sign-in and sign-out with the address | `AdminRouteTests`, `SessionAuditTests`, `SessionRevocationTests` |
| Script in a bookmark title runs in the page | Tampering | React escapes. CSP with no `unsafe-inline` for scripts. The parser is property-tested under generated input | `properties.test.ts`, ZAP on every push |
| A bookmark address runs script when clicked | Tampering | The API refuses a links document holding any address that is not http or https. The page drops one on read | `ProgressEndpointsTests`, `FitnessRouteAuthorizationTests`, `LinksPanel.test.tsx` |
| An upload replaces the owner's data silently | Tampering | A different title, note or folder is a conflict the owner settles | `bookmarks.test.ts`, the owner e2e |
| A probe answered by the edge hides an outage | Denial | `no-store` on probes and `/api`, the edge rule excludes them | The deploy smoke test fails on a HIT |
| A dependency ships a vulnerability | Tampering | Pinned digests and SHAs, locked restores, Dependabot, Trivy, CodeQL, the held-majors gate | Every pull request |
| The image is not what the source says | Tampering | Build provenance attestation and an SBOM on every deploy | The deploy workflow |
| The calendar address leaks through a log or a trace | Disclosure | The HTTP clients have no request logging. Traces leave out calls to the address. Failures log the exception type only. The page lists missing setting names, never values | `CalendarAlertWorkerTests` read every log line. `AlertsRouteTests` pin the trace filter and the status body |
| A stranger changes the alert settings, or a bad value silences the alerts | Tampering | `PUT /api/alerts/settings` needs the owner, shares the alerts' rate limit, and checks every field against its bound before one row is written. The secrets are not settings, so the route cannot read or change them | `AlertsRouteTests`: 401 and 403 on the route, 400 naming the field for each bound, `RouteTableTests` |
| A stranger floods the phone with test sends, or a test send uses up the real alert | Denial | `POST /api/alerts/test-event` needs the owner, shares the alerts' rate limit, and sends only an alert on the current list. It takes no claim, so the real send still happens | `AlertsRouteTests`: 401 and 403 on the route, 404 for a key off the list, the claim table empty after a test, the real alert sent at its time |
| A stranger silences an alarm, or turns every event into one | Tampering | `PUT /api/alerts/event-type` needs the owner, shares the alerts' rate limit, accepts only none, notification, alarm or default, and only for an event on the current list. `POST /api/alerts/test-notification` needs the owner and shares the same limit | `AlertsRouteTests`: 401 and 403 on both routes, 400 naming the field, 404 for a key off the list, 429 past the limit, `RouteTableTests` |
| A restart or a second replica sends an alert twice | Tampering | One claim row per occurrence, keyed in Postgres, taken with `ON CONFLICT DO NOTHING` before the send | `DatabaseAlertStoreTests` with eight concurrent claims |
| A crafted calendar hangs or crashes the worker | Denial | 20 MB read cap, 5000 occurrences per read, a limit on rules that never match. A bad read keeps the last list | Seeded random damage in `AlertPlannerTests` |
| The runtime identity changes the schema | Elevation | Migrations run as their own step. The runtime role has DML only | `least-privilege.sql` and its test |
| A stranger reads or writes the Lighthouse CI server | Spoofing | Basic auth on everything but `/healthz`, a 48-character random password, HTTPS only. The server refuses to start without the password | The Lighthouse job starts the image and requires 401 without it |

## Accepted, and why

- The owner's session slides for 12 hours. One owner, Google's second
  factor, the cookie flags above. The STIG's 10-minute idle limit is a
  deviation, written in `SECURITY.md`.
- No concurrent-session cap. The second device is the owner's phone.
- The heartbeat state lives in memory on one replica. A restart loses the
  last report and the next one is a minute away.

## When to revisit

A second account, a new public form, a new webhook, a second replica, or
any new thing the site can do to the dev box.
