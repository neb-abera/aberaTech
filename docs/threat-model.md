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
  the Pushover keys send to his phone. Apple's push key wakes the paired
  phones.
- The dev box: an Azure VM the site can start, and the agent channel that
  tells it to hold or park.
- The container image and the supply chain that builds and deploys it.
- Secrets on the container app: the Google client secret, the Twilio
  credentials, the agent token, the calendar address and the Pushover keys,
  Apple's push key (`Alerts__ApnsKeyP8`) with its key id and team id, the
  Cloudflare purge token in CI.
- The paired phones' push tokens: each one lets whoever holds it and the
  push key wake that phone's app.

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
| The site to Google Calendar's API | events.list, events.patch and events.insert on the connected calendar, with the stored refresh token of the `/schedule/admin` connection. Only for a type change or a new event on `/alerts` | Google |
| The site to Pushover | One POST per alarm or notification. An alarm at priority 2, with retry and expire. A notification at priority 0 or 1, with neither. An event set to Off sends nothing | Pushover |
| The site to Apple's push service | A background push over HTTP/2 to `api.push.apple.com` or `api.sandbox.push.apple.com` after a change a phone holds, at most one per phone a minute. The body is `{"aps":{"content-available":1},"v":<plan version>}` and nothing else. A JWT signed with the push key, reused for 50 minutes | Apple |
| A paired phone to the site | `PUT` and `DELETE /api/alerts/devices/me/push` with the phone's own token: Apple's push token and its environment | The phone, or whoever holds its token |
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
| A stranger silences an alarm, or turns every event into one | Tampering | `PUT /api/alerts/event-type` needs the owner or a paired phone, shares the alerts' rate limit, accepts only none, notification, alarm or default, and only for an event on the current list. `POST /api/alerts/test-notification` needs the owner and shares the same limit | `AlertsRouteTests`: 401 and 403 on both routes, 400 naming the field, 404 for a key off the list, 429 past the limit, `RouteTableTests` |
| A paired phone's token is guessed | Spoofing | 256 random bits. A malformed token is refused before hashing. Ten wrong tokens a minute per address, then 429, logged as 4011 without the token | `AlertDeviceRouteTests`: 401 for no, malformed, unknown and revoked tokens, 429 after ten, the log read for the token |
| A stolen phone or a leaked token keeps working | Spoofing | The owner revokes it on `/alerts` and the next request is 401. The token is shown once and stored as its SHA-256, so the database and the list cannot give it back | `AlertDeviceRouteTests`: revoked 401, the list without the token, the stored hash |
| A token does more than a phone needs | Elevation | Eleven routes accept it: status, mute, unmute, skip, unskip, ack, event type, new event, and a routine alarm's create, update and delete. Every other `/api/alerts` route is the owner's cookie alone and answers the token with 403, logged as 4012. Settings, pairing, the tests and the development routes stay the owner's | `RouteTableTests` names the eleven, `AlertDeviceRouteTests` gets 403 on each of the rest, `DevelopmentGoogleTests` on the development routes |
| A stranger or a leaked token adds, silences or deletes routine alarms | Tampering | `POST`, `PUT` and `DELETE /api/alerts/routines` need the owner or a paired phone and share the actions rate limit, 10 a minute per address. Every field is checked against its bound before a row is written. The id is the server's, and an unknown id is 404. A routine is a phone alarm only: it never reaches Pushover or the browser ring. Revoking the phone stops its token on the next request | `AlertRoutineRouteTests`: 401 without a cookie or token, 401 for a revoked token, 400 naming each field, 404, `RouteTableTests` |
| Routine alarms fill the database | Denial | At most 50. The count and the insert run under one advisory lock, so creates that race cannot pass 50. The 51st is a 409 | `DatabaseAlertStoreTests`: 20 creates at once on 45 kept make 50. `AlertRoutineRouteTests`: 409 at 51 |
| A leaked phone token writes to the owner's Google Calendar | Tampering | The token can add or remove `#critical` in the description of an event already on the alert list, and create an event with a title, a time, a location and one reminder. It cannot read, delete or move an event, or change attendees. Every write carries `sendUpdates=none`, so nobody is emailed. Both routes share the actions rate limit, 10 a minute per address. Revoking the phone stops it on the next request | `AlertEventRouteTests`: a phone reaches both routes, bounds per field, 429 past the limit |
| A write lands on a calendar the alerts do not read | Tampering | Before any call the server compares the connection's calendar (the address it was connected with, for `primary`) with the id in the secret address's path. A mismatch writes nothing: a 409 for a new event, a `calendarWrite` message for a type change | `CalendarWriteTests`, `AlertEventRouteTests` |
| A Google failure leaks a token or the secret address to the page or the phone | Disclosure | `calendarWrite` and the 409 or 502 `detail` are fixed sentences. The only variable part is Google's HTTP status. The client has no request logging and logs the status or the exception type only | `AlertEventRouteTests` read every failure's body for the token and the address, `CalendarWriteTests` |
| The alert for a new event waits for Google's feed | Denial | The event is stored in `AlertCreatedEvents` when Google answers, and planned from the row until the feed carries its UID or it starts. Every replica reads the table on every pass, and the claim sends once | `CalendarAlertWorkerTests` send the created alarm at its time, `DatabaseAlertStoreTests` |
| A forged acknowledgement silences an alarm | Tampering | `POST /api/alerts/ack` needs the owner or a paired phone, takes a key on the list or one already sent, shares the actions rate limit, and is stored once. It cancels only the receipt Pushover gave for that key | `AlertDeviceRouteTests`: 404 for an unknown key, 400 naming the field, one cancel for two acknowledgements |
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
| The push key leaks through a log, a trace or the page | Disclosure | The key is a container secret, read only when a token is minted. Failures log the exception type only. The page lists missing names, never values | `AlertPushRouteTests` read every log line for the key, the provider token and the push token, and read the status body |
| A push token leaks and a stranger wakes the phone | Disclosure | The token is in the URL path, so the Apple client logs nothing and traces leave out both hosts. It is never answered: the phones list carries a `push` bool. Events carry the phone's id | `AlertPushRouteTests`, `AlertsRouteTests` pin the trace filter, `ApnsClientTests` refuse a token that is not lowercase hex before it reaches a URL |
| A stranger registers a push token for a phone | Tampering | The routes take a paired phone's token alone and act on that phone's row. The owner's cookie is 403. Input is lowercase hex of 64 to 200 characters and `sandbox` or `production` | `AlertPushRouteTests`, `RouteTableTests` |
| Two replicas push the same change twice, or a burst of changes floods the phone | Denial | One plan version row, bumped by one statement. A push is claimed by one `UPDATE` on the phone's row that matches only when the phone is behind and its last push is 60 s old. Apple's 429 or 5xx is retried once after 30 s | `DatabaseAlertDeviceStoreTests` race ten claims and ten bumps, `AlertPushRouteTests` coalescing and retry |
| Apple refuses the provider token for refreshing too often | Denial | One token per process for 50 minutes. A refresh Apple asks for waits until the token is 20 minutes old | `ApnsClientTests` |
