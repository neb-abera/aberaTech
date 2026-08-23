# Scheduling

One link — `/schedule` — that books time with the host. The server answers *what
is happening right now* and the page renders it: the queue when a session is
open, bookable slots otherwise. That is deliberate. The link goes in an email
signature and in a text to everyone at once, and it cannot be two links the
reader has to choose between.

## Why this exists rather than a subscription

Two failures drove it, and they are what most of the design is answering.

**Time zones.** Availability rules store *civil* time and an IANA zone, never an
instant, because "Tuesdays nine to five" survives a daylight saving transition
even though the elapsed hours it describes do not. Everything downstream works
in instants. NodaTime throughout rather than `DateTime`, partly because it makes
the civil/elapsed distinction a type error instead of a comment, and partly
because it ships its own copy of the IANA database — so a correct answer does
not depend on how recently the base image was rebuilt.

The tests assert both 2027 transitions against real dates. A 01:00–05:00 window
yields six half-hour slots on the morning the clocks go forward and six on the
morning they go back, where wall-clock arithmetic says eight and four.

**Delivery.** Provider acceptance is not success. A provider taking a message
over HTTP says only that a carrier queued it, and the gap between that and a
handset is where messages disappear — an unregistered 10DLC campaign, a filtered
number, a disconnected line. Only a delivery receipt moves a message to
`Delivered`; anything still unconfirmed when its receipt window closes is
treated as lost and retried.

## Shape

| Piece | Job |
|---|---|
| `Domain/` | Pure. Availability, slot planning, queue projection, notification policy. No clock, no database, no HTTP |
| `Data/` | EF Core entities, the `DbContext`, migrations |
| `Outbox/` | The transactional outbox and the dispatcher that drains it |
| `Sms/` | Twilio sending and delivery-receipt verification |
| `Calendar/` | Free/busy from Google, and the local appointment source |
| `Admin/` | Google sign-in, the allowlist, and the host's queue controls |
| `Api/` | The public endpoints |

The domain is a library rather than a folder in the web project for two reasons.
It is the right shape — it knows nothing about HTTP — and `aberaTech.Server`
references the client `esproj`, so anything referencing the server would need
Node installed to run these tests.

## Guarantees, and where they live

- **No double booking.** A `btree_gist` exclusion constraint on `Appointments`.
  Not application logic: "read the calendar, see it is free, insert" is correct
  only until two people press book at the same moment, and no amount of checking
  first closes that window. The insert is what is wrapped, and SQLSTATE `23P01`
  surfaces as "that time was just taken".
- **A notification is as durable as the change that caused it.** The outbox row
  is written in the same transaction as the booking, so a crash between the two
  cannot happen.
- **No message goes out twice.** A unique idempotency key per logical
  notification, and an idempotency token on the send itself.
- **Nothing forges a receipt.** Twilio signatures are verified with HMAC-SHA1
  against the *configured* callback URL — not one rebuilt from request headers,
  which the caller controls — and compared in fixed time.
- **The dispatcher scales past one replica.** Claiming uses
  `SELECT … FOR UPDATE SKIP LOCKED`.

## Configuration

Every section below is optional, and each one absent degrades rather than
breaks. With no database the tab explains itself; with no Twilio the dispatcher
runs against a logging sender; with no Google the admin surface does not exist
and slots come from rules alone.

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Scheduling` | Postgres. Migrations run at start |
| `Database:UseEntraAuth` | Authenticate as the container's managed identity instead of with a password |
| `Scheduling:HostName`, `HostZoneId` | Whose queue, and the zone rules are written in |
| `Scheduling:DefaultWindowDays`, `HorizonDays` | Days sent up front, and the furthest anybody may book |
| `Twilio:AccountSid`, `AuthToken`, `FromNumber` | Sending |
| `Twilio:StatusCallbackUrl` | Must be the exact public URL — it is what the signature covers |
| `Admin:GoogleClientId`, `GoogleClientSecret` | OAuth client |
| `Admin:AllowedEmails` | Who may run the queue. **Empty means nobody** |

Secrets belong in container app secrets, never `appsettings.json`. The Twilio
auth token in particular is the key that signs delivery receipts.

### Connecting to Postgres without a password

With `Database:UseEntraAuth` the connection string carries a `Username` and no
`Password`, and Npgsql asks Azure for a short-lived token instead — refreshed on
a timer for the whole process. There is then no password anywhere: not in a
container app secret, not in a deploy command, not in anybody's shell history,
and nothing to rotate.

Three things have to be true, and none of them are in this repository:

1. Entra authentication enabled on the server. Additive — password
   authentication keeps working, so anything else on the same server is
   unaffected.
2. An Entra administrator set on the server, so somebody can create the role.
3. A Postgres role for the container app's managed identity, created by that
   administrator, and granted rights on the `scheduling` database.

Off by default, so a developer machine with a plain connection string keeps
working unchanged. When it is on and no identity is available the application
fails immediately with a credential error rather than falling back to anything.

### Sending real SMS

US A2P 10DLC registration gates this, and owning the code does not dodge it. An
individual registers a **Sole Proprietor** brand, capped around 3,000 segments a
day with a 1,000/day T-Mobile sub-limit — ample for a counselling session, and
the ceiling to watch if the list ever reaches the hundreds, since crossing it
means registering as a Standard Brand with an EIN.

## Running it locally

```bash
make db          # Postgres on 127.0.0.1:5433
make servertest  # the scheduling tests
make check       # the whole gate: both suites, lint, format
```

Availability rules seed automatically in Development only, so a fresh database
produces a page with slots rather than an empty state that looks like a bug.
