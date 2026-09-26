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
- The dev box: an Azure VM the site can start, and the agent channel that
  tells it to hold or park.
- The container image and the supply chain that builds and deploys it.
- Secrets on the container app: the Google client secret, the Twilio
  credentials, the agent token, the Cloudflare purge token in CI.

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
| The site to Postgres | Parameterised queries as the runtime role, passwordless | The application |

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
| The runtime identity changes the schema | Elevation | Migrations run as their own step. The runtime role has DML only | `least-privilege.sql` and its test |

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
