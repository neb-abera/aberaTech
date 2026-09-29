[![Checks](https://github.com/neb-abera/aberaTech/actions/workflows/client-checks.yml/badge.svg?branch=master)](https://github.com/neb-abera/aberaTech/actions/workflows/client-checks.yml)
[![CodeQL](https://github.com/neb-abera/aberaTech/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/neb-abera/aberaTech/security/code-scanning)
[![codecov](https://codecov.io/gh/neb-abera/aberaTech/graph/badge.svg)](https://codecov.io/gh/neb-abera/aberaTech)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/neb-abera/aberaTech/badge)](https://scorecard.dev/viewer/?uri=github.com/neb-abera/aberaTech)

# aberaTech

The source of [abera.tech](https://abera.tech): a .NET 11 server, a React
client, and the guides and tools on the site.

## Running it

Docker is the only requirement.

```
make            # list every target
make ports      # this copy's compose project and host ports
make up         # the site and its database
make queue-open # switch /schedule into queue mode
make queue-close# switch it back to bookable slots
make dev        # hot reloading dev server
make test       # client unit tests, against the working tree
make lint       # biome, against the working tree
make fmt        # rewrite files to match biome
make budget     # page weight against scripts/page-budgets.json
make prose      # the writing rules, on the Markdown and the prerendered pages
make e2e        # Playwright against the production image and its database
make lighthouse # Lighthouse against the production image, docs/lighthouse.md
make check      # the gate CI runs
make run        # build and run the production image
make clean      # remove this copy's containers and volumes
```

`make ports` first. Ports, container names and the compose project derive
from the directory, so two worktrees run side by side, each with its own
database. `make clean` takes down the copy you are standing in. Override for
one run with `APP_PORT=9001 make up`, or edit the `.env` the first `make`
writes.

`make dev`, `make test` and `make lint` bind mount the working tree.
`make check` copies it into the image, the way CI does. Run it before you
push.

After changing a dependency, run `make clean` before `make dev`. The dev
service keeps `node_modules` in a volume that outlives a rebuild.

Lighthouse runs on every pull request and every night against the live
site. The history is on the
[Lighthouse CI server](https://abera-lhci.purpleocean-f7e5c55d.eastus.azurecontainerapps.io/app/projects/abera.tech-production)
(docs/lighthouse.md).

## How it is built

- The public pages (home, the indexes, the guides) are prerendered to HTML
  at build time (`aberatech.client/tools/prerender.mjs`,
  `src/entry-server.tsx`) and hydrated in the browser. The app pages are
  client-rendered from `spa.html`.
- Hashed `/assets` are immutable for a year and HTML is `no-cache`
  (`StaticAssetCaching.cs`). Cloudflare caches the HTML. The deploy workflow
  purges the zone on every merge to master.
- Routing is explicit and sits after the static-file middleware in
  `Program.cs`.
- The CSP's inline-script and style-element hashes are computed from the
  shipped HTML at startup (`ContentSecurityPolicy.cs`). Each page is sent
  its own hashes. Before, every page carried all 29, and the home page's
  headers took 2,653 bytes at the origin.
  `tools/prerender.mjs` gathers each page's MUI styles into its head, so a
  page needs three hashes instead of 94. Cloudflare RUM is allowlisted and is the
  measurement of record.
- The fitness console reads the training log once per request
  (`TrainingHistory.cs`). A summary is six database commands at any log
  length. Read-only queries are untracked. The summary, digest and readiness
  outlook sit in the output cache (`FitnessOutputCache.cs`) until a write,
  keyed per user, after authorization, with no `Cache-Control` added. Tests
  count the commands and pin the payloads.
- Images are WebP at the drawn size, with width and height. The home page
  preloads its avatar. `/headshot.jpg` and `og.png` keep their addresses for
  search engines and link previews.
- The server is published ReadyToRun. Cold start roughly halved, for a
  larger image.
- Page weight is a gate in bytes (`make budget`). The checker fails on an
  over-budget fixture before it is trusted to pass the build.
- Prose is a gate (`make prose`): Vale with the rules in
  `.vale/styles/Abera`, over the Markdown and the prerendered pages.
- [docs/threat-model.md](docs/threat-model.md) names the assets, the entry
  points and every threat with the gate that answers it. A new entry point
  is a row there before it is a feature.
- `/devbox` starts the owner's Azure VM from a phone and opens its terminal or desktop in a browser tab (`DevBoxEndpoints.cs`, `BrowserPanel.tsx`).
  The container app's managed identity holds one role on that one VM: start
  and read. `DevBox__SubscriptionId` switches it on.

- `/alerts` sends one Pushover message before each event on the owner's
  Google Calendar that is an alarm or a notification
  (`aberaTech.Scheduling/Alerts/`). An event marked `#critical` is an
  alarm. Any event's type can be set on the page. Every other event sends
  nothing by default. The worker reads the
  secret iCal address and sends each alert at its own time. The alert time
  is the event's earliest popup reminder, or the default lead before the
  start. Cancelled and declined events are skipped. Text and "06:00
  tomorrow" use the calendar's own zone (`X-WR-TIMEZONE`, then the
  settings' zone, then UTC). Mute, Skip, the settings, each event's type
  and a one-send claim per occurrence are rows in the scheduling database.
  A failed calendar read is a red banner at the top of the page, with the
  error and the time of the last good read. Phones paired on the page
  (the Abera Alarms iPhone app) ring every alarm themselves, and
  Acknowledge on the phone or the page stops Pushover's repeats. Ring in
  this browser rings a due alarm in an open tab, for a computer where
  nothing can be installed. The page and a paired phone can create an
  event, and an event's type is written back to Google Calendar as
  `#critical`.

### Calendar alerts: switching them on

Run these on the devbox as neb, or anywhere `az` is signed in. Put the
values between the quotes. Secret names are at most 20 characters.

```bash
app=aberatechserver-app-202412211749
group=aberatechserver-app-202412211749ResourceGroup

az containerapp secret set -n "$app" -g "$group" --secrets \
  alerts-calendar-ics='<secret address in iCal format>' \
  alerts-pushover-app='<Pushover application API token>' \
  alerts-pushover-user='<Pushover user key>'

az containerapp update -n "$app" -g "$group" --container-name aberatechserver \
  --set-env-vars \
  Alerts__CalendarIcsUrl=secretref:alerts-calendar-ics \
  Alerts__PushoverAppToken=secretref:alerts-pushover-app \
  Alerts__PushoverUserKey=secretref:alerts-pushover-user \
  Alerts__TimeZone=Asia/Amman
```

The update starts a new revision. `/alerts` then lists the next alerts and
the time of the last calendar read. Send test alert proves the keys with
an alarm. Send test notification sends one notification. Send test on a
listed alert sends that event's own text, titled `Test: <title>`, as the
event's type. It is off for an event that sends nothing. It claims nothing
and ignores Mute and Skip, so the real alert still goes at its time.

A red banner saying the calendar cannot be read with `HTTP 404` means
Google does not know the address. Copy the secret address in iCal format
again and set `alerts-calendar-ics` with the first command above.

### Calendar alerts: which events alert

Each event has one of three types (`AlertTypes.cs`):

| Type | Sends |
|---|---|
| Alarm | one message that rings every repeat until acknowledged, then stops at the stop time |
| Notification | one message at the notification priority and sound. Never a retry or an expiry |
| None | nothing. The event is still listed on the page |

The type comes from the first of these that applies:

1. The type set on the page. Each listed alert has None, Notification and
   Alarm. The choice is kept under the event's UID, so it holds for every
   occurrence of a repeating event. Use default removes it. A choice for an
   event missing from the feed for 60 days is deleted.
2. `#critical` in the title or description, as a word of its own, in any
   case: an alarm. `#criticality` and `a#critical` do not count. The mark
   is left off the title shown and sent (`AlertPlanner.cs`).
3. The default for unmarked events under Settings: None unless changed.

Every timed event that starts inside the look-ahead window is planned and
listed. All-day events are left out unless the setting is on. Cancelled
events and invitations the owner declined are left out. The alert goes at
the event's earliest popup notification, else the default lead before the
start (`AlertPlanner.cs`). An event set to None is asked again at every
pass until it starts, so switching it on after its alert time still sends.

Google Calendar's menus, from support.google.com/calendar/answer/37242:

- One event: open it, Edit event, then next to Notifications change the
  time or Add notification.
- A calendar's default: Settings, then under Settings for my calendars the
  calendar, then Event notifications.

The secret address carries only notifications set on the event itself.
The calendar's default notifications are not in it. On 2026-09-28 the
owner's feed held 1132 events and 30 of them carried a VALARM, each with
a time set on that event. None of the upcoming events carried one.
Set the default lead to the calendar's default minutes.

### Calendar alerts: writing to Google Calendar

Two routes write to the owner's Google Calendar, from the page or a paired
phone (`CalendarWrites.cs`). Both use the connection made on
`/schedule/admin` with Connect Google Calendar. That grant carries the
`calendar.events` scope.

- Setting an event's type writes it back. Alarm adds `#critical` on a line
  of its own at the end of the description, once. None, Notification and
  Use default remove every `#critical` word, in any case, and the blank
  line it leaves. A repeating event is one series in Google, so the series
  is patched once. The choice is stored first. When Google is not changed
  the answer's `calendarWrite` says why, in one sentence, and the page
  shows it as a warning.
- New event on the page creates an event with one popup reminder at the
  lead, and `#critical` in the description for an alarm. The type is
  stored under the new event's UID. The server keeps the event until the
  secret address carries its UID or it starts. Google's feed can lag the
  API by minutes to hours, and the alert does not wait for it
  (`CreatedEvents.cs`, table `AlertCreatedEvents`).

A write goes only to the calendar the alerts read. The secret address has
the calendar's id in its path, `calendar/ical/<id>/private-…/basic.ics`.
For a primary calendar that id is the account address. The connection's
calendar is `primary`, whose id is the address it was connected with. The
two must match, in any case. `X-WR-CALNAME` is used only for an address of
another shape, and only when it is an address, because the owner can
rename a calendar.

| Answer | Why |
|---|---|
| `Google Calendar is not connected with edit access.` | no connection, or one made before the events scope. Disconnect and connect again on `/schedule/admin` |
| `The connected calendar is not the one alerts read.` | the connection is another account, or the secret address is a secondary calendar |
| `Google refused the stored calendar sign-in. Connect the calendar again.` | the refresh token was revoked or cannot be read |
| `Google Calendar has no such event on the connected calendar.` | the UID is not on that calendar |
| `Google refused the change: this event is organised by someone else.` | an invitation. Only its organiser can edit the description |
| `Google refused the change (HTTP <status>).` | any other refusal |
| `Google Calendar did not answer.` | a timeout or no connection, after 10 s |

`POST /api/alerts/events` takes `{title, startsAt, durationMinutes,
location, type, leadMinutes}`. `title` is 1 to 200 characters. `startsAt`
is ISO 8601 with its offset, in the future and at most 366 days ahead.
`durationMinutes` is 5 to 1440. `location` is optional, at most 200
characters. `type` is `alarm`, `notification` or `none`. `leadMinutes` is
0 to 1440, and left out it is the saved default lead. The answer is 201
with the page's whole state, the new event listed. A refused field is a
400 keyed by its name. No connection with edit access, or a connection to
another calendar, is a 409 whose `detail` says which. Google refusing is a
502 with the same short reason.

### Calendar alerts: settings

The Settings section on `/alerts` changes these without a deploy. Save
stores one row. The replica that took the save plans with it at once, and
every other replica reads it at the start of its next pass.

| Setting | Default | Bounds |
|---|---|---|
| Pushover repeats every | 60 s | 30 to 10800 s |
| Pushover stops after | 180 min | 1 to 180 min |
| Alarm sound | the phone's default | one of Pushover's 23 built-in sounds |
| Notification priority | Normal | Normal: one sound, follows the phone's settings. High: one sound, even during Pushover's quiet hours |
| Notification sound | the phone's default | one of Pushover's 23 built-in sounds |
| Events with no mark and no type set here | None | None or Notification |
| Default lead | 10 min | 0 to 1440 min |
| Check calendar every | 5 min | 1 to 60 min |
| Look ahead | 48 h | 1 to 336 h |
| Alert for all-day events | off | |
| Time zone | blank, UTC | a time zone database name |
| Your addresses | none | up to 10 |
| Pushover backup after | 0 s | 0 to 900 s. An alarm's Pushover message waits this long, so a paired phone rings first. It is not sent if the alarm is acknowledged by then, and never later than 1 minute before the start |

An alarm has no priority setting. Every alarm goes to Pushover at its
priority 2 with `retry` set to the repeat and `expire` set to the stop, so
it rings until acknowledged. Pushover stops such a message after 50 sounds,
so the stop is the smaller of the limit and 50 × the repeat. At 60 s that is
50 min. The page shows the arithmetic.

Pushover refuses a repeat under 30 s (pushover.net/api#priority). A paired
phone rings as an iPhone alarm until Stop is pressed. Ring in this browser
beeps every second until the alarm is acknowledged. Nonstop sets 30 s and `persistent`,
one of the five sounds pushover.net/api#sounds marks long (alien, climb,
persistent, echo, updown). iOS plays a notification sound for up to 30 s
(developer.apple.com/documentation/usernotifications/unnotificationsound),
so a long sound plays into each 30 s gap. At 30 s Pushover's 50 sounds last
25 min. Until the first save the defaults come from the
`Alerts__` settings of the same names (`AlertsOptions.cs`).

The calendar address and the two Pushover keys are not on the page. They
stay container secrets, because a key typed into a web form passes through
the browser and the database.

## How it stays current

GitHub Actions are pinned by commit SHA. Dependabot bumps SHA and comment
together, minor and patch grouped weekly per ecosystem. The
`dependabot-automerge` workflow arms auto-merge on every Dependabot PR,
majors included. It needs Allow auto-merge and a `DEPENDABOT_AUTOMERGE_TOKEN`
secret (a fine-grained PAT, so the merge triggers the deploy, recreated
quarterly). Scorecard, CodeQL and a Trivy image scan run on schedule.

## Stages in the Dockerfile

| Stage | What it is |
|---|---|
| `clientbase` | the client dependencies, `npm ci` |
| `clientbuild` | the production bundle and the prerendered pages |
| `clientbudget` | page weight against `scripts/page-budgets.json`. A leaf |
| `clientprose` | the writing rules over the prerendered pages. A leaf |
| `clientdev` | the vite dev server, source bind mounted at run time |
| `clienttest` | `tsc -b` and the unit tests. A leaf |
| `clienttools`, `clientlint` | biome over the whole repository. A leaf |
| `vale` | the prose linter image, read by `scripts/check-prose.sh` |
| `build`, `servertest`, `publish`, `final` | the .NET server, its tests, and the deployed image |

The client stages resolve from `nodebase`, so the tests run on the node that
builds the artifact.

### JetBrains

The `make` targets work from the IDE terminal. Docker Desktop must be
running.

To resolve imports and run tests from the gutter, point the IDE at the
container:

1. **Settings, Build, Execution, Deployment, Docker**: add a connection for
   Docker Desktop.
2. **Settings, Languages & Frameworks, Node.js**: set the interpreter to
   **Add, Docker Compose**, with `compose.yaml` and the `test` service.
3. **Run, Edit Configurations, Add, Docker, Docker Compose**: `compose.yaml`
   and service `dev`, for a one-click dev server.

`.idea/` is partly gitignored. A run configuration worth sharing can be
committed.

### Without make

```
docker compose up --build dev
docker compose run --rm test
docker build --target clienttest -f aberaTech.Server/Dockerfile .
docker build --target clientlint -f aberaTech.Server/Dockerfile .
docker build --target clientprose -f aberaTech.Server/Dockerfile .
```
