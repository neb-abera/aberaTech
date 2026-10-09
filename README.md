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
- `/playbook` shows the owner's Notion playbook, read live, for a computer
  that can reach this site and not Notion (`Playbook/`). It reads the
  pages under one root page and streams their files as downloads. Notion
  stays the record and nothing is stored here.

- `/alerts` sends one Pushover message before each event on the owner's
  Google Calendar set to Ring until stopped or Ring once
  (`aberaTech.Scheduling/Alerts/`). An event marked `#critical` rings
  until stopped. Any event's type can be set on the page. Every other event sends
  nothing by default. The worker reads the
  secret iCal address and sends each alert at its own time. The alert time
  is the event's earliest popup reminder, or the default lead before the
  start. Cancelled and declined events are skipped. Text and "06:00
  tomorrow" use the calendar's own zone (`X-WR-TIMEZONE`, then the
  settings' zone, then UTC). Mute, Skip, the settings, each event's type
  and a one-send claim per occurrence are rows in the scheduling database.
  A failed calendar read is a red banner at the top of the page, with the
  error and the time of the last good read. Phones paired on the page
  (the Abera Alarms iPhone app) ring each Ring until stopped event
  themselves, and
  Acknowledge on the phone or the page stops Pushover's repeats.
  Acknowledge in the Pushover app reaches the server through Pushover's
  callback and stops the phones and the page too. Ring in
  this browser rings a due Ring until stopped event or routine alarm in an
  open tab, for a computer where
  nothing can be installed. The page and a paired phone can create an
  event, and an event's type is written back to Google Calendar as
  `#critical`. Each change a phone holds sends it a background push, so
  it updates its alarms at once.

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
the time of the last calendar read. Test: ring until stopped proves the
keys with a message that rings until acknowledged. Test: ring once sends
one message with one sound. Send test on a
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
| Ring until stopped (`alarm`) | one message that rings every repeat until acknowledged, then stops at the stop time |
| Ring once (`notification`) | one message at the Ring once priority and sound. Never a retry or an expiry |
| Off (`none`) | nothing. The event is still listed on the page |

The type comes from the first of these that applies:

1. The type set on the page. Each listed alert has Off, Ring once and Ring
   until stopped. The choice is kept under the event's UID, so it holds for every
   occurrence of a repeating event. Use default removes it. A choice for an
   event missing from the feed for 60 days is deleted.
2. `#critical` in the title or description, as a word of its own, in any
   case: Ring until stopped. `#criticality` and `a#critical` do not count. The mark
   is left off the title shown and sent (`AlertPlanner.cs`).
3. The default for unmarked events under Settings: Off unless changed.

Every timed event that starts inside the look-ahead window is planned and
listed. All-day events are left out unless the setting is on. Cancelled
events and invitations the owner declined are left out. The alert goes at
the event's earliest popup notification, else the default lead before the
start (`AlertPlanner.cs`). An event set to Off is asked again at every
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

Four routes write to the owner's Google Calendar, from the page or a paired
phone (`CalendarWrites.cs`). All use the connection made on
`/schedule/admin` with Connect Google Calendar. That grant carries the
`calendar.events` scope.

- Setting an event's type writes it back. Ring until stopped adds
  `#critical` on a line of its own at the end of the description, once.
  Off, Ring once and Use default remove every `#critical` word, in any case, and the blank
  line it leaves. A repeating event is one series in Google, so the series
  is patched once. The choice is stored first. When Google is not changed
  the answer's `calendarWrite` says why, in one sentence, and the page
  shows it as a warning.
- New event on the page creates an event with one popup reminder at the
  lead, and `#critical` in the description for Ring until stopped. The type is
  stored under the new event's UID. The server keeps the event until the
  secret address carries its UID or it starts. Google's feed can lag the
  API by minutes to hours, and the alert does not wait for it
  (`CreatedEvents.cs`, table `AlertCreatedEvents`).
- Edit on a listed event changes its title, start, length, location and
  reminder. Delete removes it. For a repeating event both ask whether the
  change is for this event or all events. The description, `#critical` and
  the stored type are left alone. The server keeps the change until the
  secret address shows it, or the old and new starts have both passed
  (`EventChanges.cs`, table `AlertEventChanges`). A deleted occurrence is
  never sent, and a Pushover repeat still running for it is cancelled.

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
| `Only the organizer can change this event.` | an edit or a deletion of an invitation |
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

Each listed alert carries `recurring`, true when the occurrence belongs to
a series (an RRULE, or a moved occurrence of one), and `endsAt`, ISO 8601
with its offset, or null when the event has no end.

| Route | Request | Answer |
|---|---|---|
| `PUT /api/alerts/events` | `{key, scope, title, startsAt, durationMinutes?, location?, leadMinutes?}` | 200 with the page's whole state, the edit listed |
| `POST /api/alerts/events/delete` | `{key, scope}` | 200 with the page's whole state, the deleted occurrences gone |

`key` is a listed alert's key. It holds the event's UID, so it goes in the
body. A key in the query string is not read. `scope` is `occurrence` or `series`.
`title`, `startsAt`, `location` and `leadMinutes` take the same bounds as a
new event. A missing or null `durationMinutes` keeps the event's length,
and 5 to 1440 sets it. A missing `location` leaves it, and null clears it.
A missing or null `leadMinutes` leaves the reminders alone. A number sets
one popup reminder at that lead. An unknown key is 404. A refused field is
a 400 keyed by its name. An invitation, no connection with edit access, or
a connection to another calendar is a 409 with a `detail`. Any other
Google failure is a 502 with a `detail`. Nothing is kept or pushed on a
failure.

The event is found by `events.list?iCalUID=`. `occurrence` on a repeating
event patches or deletes the one instance: `events.instances` with
`timeMin` at the occurrence's start and `timeMax` one second later lists
it, and its own id is patched or deleted, which Google keeps as an
exception or a cancelled instance. `occurrence` on an event that does not
repeat, and `series`, patch or delete the event itself. A `series` edit
moves the master's start by the edited occurrence's change in date and
wall-clock time, in the series' own zone. A daily 09:00 moved to 10:00
stays at 10:00 across a clock change, as Google Calendar's All events edit
does. Every write carries `sendUpdates=none`.

A `series` edit that moves the date by D days, counted on the series'
clock, also rewrites the recurrence that `events.list` returned, and sends
it (`RecurrenceShift.cs`). A rule that names its days keeps making them
after DTSTART moves (RFC 5545 section 3.3.10). So a weekly Tuesday moved
to Wednesday would go on repeating on Tuesdays.

| Part | Rewrite |
|---|---|
| `BYDAY` | Every weekday moves by D, modulo 7. `MO,WE,FR` a day earlier is `SU,TU,TH`. |
| `BYDAY` with one ordinal, monthly or in a `BYMONTH` | The new start's ordinal when the old one named the old start. `2TU` on 14 July 2026, moved to the 15th, is `3WE`. Otherwise the ordinal stays and the day moves. |
| `BYMONTHDAY`, monthly or yearly | One value that named the old start takes the new start's day. `31` moved a day later is `1`. `-1` stays negative inside its month. Other values move by D, clamped to 1 to 31. |
| `BYMONTH`, yearly | One value that named the old start's month takes the new month. |
| `EXDATE`, `RDATE` | Each date moves by the same change in date and wall-clock time, so a cancelled occurrence stays cancelled. RFC 5545 section 3.8.5.1 matches an EXDATE by its exact start. |
| `UNTIL`, `COUNT`, `INTERVAL`, `WKST`, `BYSETPOS`, `BYYEARDAY`, `BYWEEKNO` | Kept. |

A rule with none of these parts, such as `FREQ=DAILY` or `FREQ=WEEKLY`
with no `BYDAY`, takes its days from DTSTART and is not sent. A move
within the same day sends no recurrence, unless an `EXDATE` or `RDATE`
must move with the time. Google does not document what its editor does
with exceptions, so the server keeps them on the series as moved.
`occurrence` never sends a recurrence.

A moved start gives the occurrence a new key. Its skip goes with it. When
the new alert time has already come and the alert went, the send claim
goes with it too, so the alert is not sent twice.

### Calendar alerts: acknowledged in Pushover

Every alarm sent to Pushover names a callback,
`<Alerts:PublicOrigin>/api/alerts/pushover/acknowledged`. Production sets
`Alerts:PublicOrigin` to `https://abera.tech` in
`appsettings.Production.json`. Empty sends no callback.

When the owner presses Acknowledge in the Pushover app, Pushover posts
`receipt`, `acknowledged`, `acknowledged_at`, `acknowledged_by` and
`acknowledged_by_device`, form-encoded, with no session. The route is
anonymous and believes nothing in the body alone. The receipt must be one
the server stored for a send, and
`GET https://api.pushover.net/1/receipts/{receipt}.json` must say
`acknowledged` is 1. The acknowledgement is then recorded with `via`
`pushover`, and the phones are pushed. The page shows "Acknowledged in
Pushover".

| Answer | When |
|---|---|
| 200 `{"acknowledged":true}` | Recorded now, or already acknowledged anywhere. A second post changes nothing |
| 400 | Not a form, or not exactly one receipt of letters and digits |
| 403 | Pushover says the receipt is not acknowledged |
| 404 | A receipt this server never sent. Ten a minute per address, then 429 |
| 503 | Pushover could not be asked. Pushover posts again a minute later |

Thirty posts a minute per address. Each refusal is logged as 4017.
`POST /api/alerts/ack` still takes `phone` or `browser` alone.

### Calendar alerts: routine alarms

Routine alarms are the phone's everyday alarms, kept on abera.tech so they
can be read and changed from any computer. The paired iPhone rings them
as its own alarms. The server works out the same rings
(`RoutineRings.cs`) and rings each one as an alarm: through Pushover
after the backup delay, and in an open tab with Ring in this browser on.
One acknowledgement anywhere stops all of them (`AlertRoutines.cs`, table
`AlertRoutines`).

The rings follow the phone's rules. A routine with days rings on those
weekdays. One with none rings once, at the next `hour:minute` after it was
saved. A time a clock change skips rings at the first time that exists
after it, and a time that happens twice rings the first time. An edit
changes later rings only: a ring that is ringing when the edit is saved
keeps ringing as it was (table `AlertHeldRings`), unless the edit turns
the routine off. Turning it off or deleting it stops its rings and
cancels Pushover's repeats. The rings are in the zone of the paired phone
seen most recently that sent `X-Time-Zone`, else the settings' zone.

`GET /api/alerts/status` lists them under `routineRings`, from the one
ringing now to 24 hours ahead. They are kept out of `alerts`, so an older
phone app does not schedule them as calendar alarms. Each is:

```json
{
  "key": "routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-28T06:30",
  "routineId": "0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10",
  "label": "Wake up",
  "alertAt": "2026-10-28T03:30:00+00:00",
  "startsAt": "2026-10-28T06:30:00+00:00",
  "acknowledged": false,
  "acknowledgedAt": null,
  "acknowledgedVia": null
}
```

`key` is the routine's id and the scheduled local date and time, never a
snoozed one. `startsAt` is the ring and the saved Pushover stop time.
`POST /api/alerts/ack` takes a listed key, or one Pushover sent.

A routine is `{id, label, hour, minute, days, enabled, snoozeMinutes,
updatedAt}`. `hour` is 0 to 23 and `minute` 0 to 59, in wall-clock time.
The server never converts them. `days` are ISO weekdays, Monday 1 to
Sunday 7, stored sorted. Empty rings once at the next `hour:minute`, and
the phone then turns it off with a `PUT` setting `enabled` false. `label`
is trimmed, at most 60 characters, with no control characters. Empty,
blank or missing is stored as `Alarm`. `snoozeMinutes` is 1 to 30, 9 by
default. `updatedAt` is ISO 8601 with its offset. At most 50 are kept.

`GET /api/alerts/status` lists every routine under `routines`, by hour,
minute, then label. Each route below needs the owner or a paired phone,
shares the actions rate limit, and answers with the page's whole state.
Each change pushes the phones.

| Route | Request | Answer |
|---|---|---|
| `POST /api/alerts/routines` | `{label?, hour, minute, days, enabled?, snoozeMinutes?}`. `enabled` is true when left out | 201. 400 by field. 409 with a `detail` past 50 |
| `PUT /api/alerts/routines/{id}` | the whole body: `{label, hour, minute, days, enabled, snoozeMinutes}` | 200. 400 by field. 404 for an unknown id |
| `DELETE /api/alerts/routines/{id}` | none | 200. 404 for an unknown id |

### Playbook: connecting Notion

`/playbook` needs two values. Without both, the page says Notion is not
connected and the rest of the site runs as before.

| Environment variable | Kind | Value |
|---|---|---|
| `Notion__Token` | secret `notion-token` | an internal integration's secret, from notion.so/profile/integrations |
| `Notion__PlaybookPageId` | setting | the root page's id: the 32 characters at the end of its link |

In Notion, open the root page, then ••• > Connections, and add the
integration. It then reads that page and everything under it.

Run these on the devbox as neb, or anywhere `az` is signed in. Put the
values between the quotes.

```bash
app=aberatechserver-app-202412211749
group=aberatechserver-app-202412211749ResourceGroup

az containerapp secret set -n "$app" -g "$group" \
  --secrets notion-token='<integration secret>'

az containerapp update -n "$app" -g "$group" --container-name aberatechserver \
  --set-env-vars Notion__Token=secretref:notion-token \
  Notion__PlaybookPageId='<root page id>'
```

The update starts a new revision. The deploy workflow changes only the
image, so both stay set across deploys.

The server sends `Notion-Version: 2026-03-11`. It keeps Notion's answers
in memory for 5 minutes (`Notion__CacheSeconds`), waits out a 429 as
Retry-After says for up to 10 s and 3 tries, and follows `has_more` to the
end of every list. A file's block is read fresh on every download, since
its signed address lasts an hour.

| Route | Answer |
|---|---|
| `GET /api/playbook` | `{configured, root}`: the tree of pages and databases under the root |
| `GET /api/playbook/pages/{id}` | `{id, title, blocks}`. 404 for a page outside the root |
| `GET /api/playbook/files/{blockId}` | the file as an attachment. 404 for a block outside the root or one that is not a file Notion holds |

Each route is the owner's alone. 503 with Retry-After when Notion asks for
a longer wait. 502 naming Notion's status for any other failure.

### Dates and countdowns

`/dates` counts the days between two dates and adds or subtracts years,
months, weeks and days from a date. It runs in the browser on plain dates,
with no time and no zone, and sends nothing (`features/dates/core/dateMath.ts`).
The phone app carries the same arithmetic and the same test table.

Below the calculator the owner keeps countdowns. Each clock shows the time
left, then the time since once the date passes. They are kept with the
alerts, so the paired phone shows the same list (`AlertCountdowns.cs`, table
`AlertCountdowns`). Nothing on the server fires them. A visitor's browser
asks who is signed in first and never asks for them.

A countdown is `{id, label, targetAt, timeZone, updatedAt}`. `targetAt` is
ISO 8601 with its offset, between 1900 and 2200, and may be in the past.
`timeZone` is an IANA zone the tz database knows, and the target's date is
written in it. `label` follows the routine rules, and empty is stored as
`Countdown`. At most 50 are kept.

`GET /api/alerts/status` lists every countdown under `countdowns`, by
target, then label. Each route below needs the owner or a paired phone,
shares the actions rate limit, and answers with the page's whole state.
Each change pushes the phones.

| Route | Request | Answer |
|---|---|---|
| `POST /api/alerts/countdowns` | `{label?, targetAt, timeZone}` | 201. 400 by field. 409 with a `detail` past 50 |
| `PUT /api/alerts/countdowns/{id}` | `{label?, targetAt, timeZone}` | 200. 400 by field. 404 for an unknown id |
| `DELETE /api/alerts/countdowns/{id}` | none | 200. 404 for an unknown id |

### Calendar alerts: phone pushes

A change that alters what a phone should hold sends every phone with a
push token a background push through Apple's push service
(`AlertPushes.cs`). The changes are an event's type, Skip and Unskip, Mute
and Unmute, an acknowledgement from anywhere, a new event, an event edited
or deleted, a routine alarm or a countdown added, changed or deleted, a change to the
phone's alarm sound or snooze, and a calendar read whose alarms differ
from the last read. The phone then reads
`/api/alerts/status`. iOS can delay or drop a background push, so the
phone also reads when opened and in the background.

Each change adds one to a plan version in the database. Each phone gets at
most one push a minute, carrying the version current when it goes. The
body is `{"aps":{"content-available":1},"v":<version>}`, with
`apns-push-type: background`, `apns-priority: 5`, `apns-topic:
tech.abera.alarms`, `apns-collapse-id: plan` and an expiry an hour out.
Apple's 429 or 5xx is tried once more after 30 s. A 410, or a 400 with
`BadDeviceToken` or `DeviceTokenNotForTopic`, clears that phone's token.

| Route | Who | Request | Answer |
|---|---|---|---|
| `PUT /api/alerts/devices/me/push` | a paired phone's token alone | `{apnsToken, environment}`: lowercase hex, 64 to 200 characters, and `sandbox` or `production` | 204. 400 by field. 403 to the owner's cookie |
| `DELETE /api/alerts/devices/me/push` | a paired phone's token alone | none | 204. The token is cleared |
| `GET /api/alerts/devices` | the owner | none | each phone with `push`: true when it has a token. Never the token |

Three secrets switch pushes on. Without all three, phones still register
and the Phones section names the missing ones.

| Environment variable | Secret | Value |
|---|---|---|
| `Alerts__ApnsKeyP8` | `alerts-apns-key` | the text of `AuthKey_<KEYID>.p8` |
| `Alerts__ApnsKeyId` | `alerts-apns-key-id` | the key's 10-character id |
| `Alerts__ApnsTeamId` | `alerts-apns-team-id` | `9K44N8AGF7` |

Run these on the MacBook, where the key was downloaded, with `az` signed
in. Put the key id in place of `<KEYID>`.

```bash
az containerapp secret set -n aberatechserver-app-202412211749 \
  -g aberatechserver-app-202412211749ResourceGroup \
  --secrets alerts-apns-key="$(cat ~/Downloads/AuthKey_<KEYID>.p8)" \
  alerts-apns-key-id=<KEYID> alerts-apns-team-id=9K44N8AGF7

az containerapp update -n aberatechserver-app-202412211749 \
  -g aberatechserver-app-202412211749ResourceGroup \
  --container-name aberatechserver \
  --set-env-vars Alerts__ApnsKeyP8=secretref:alerts-apns-key \
  Alerts__ApnsKeyId=secretref:alerts-apns-key-id \
  Alerts__ApnsTeamId=secretref:alerts-apns-team-id
```

The deploy workflow changes only the image, so these stay set across
deploys, like the Pushover secrets. The Phones section then shows no
missing names.

### Calendar alerts: settings

The Settings section on `/alerts` changes these without a deploy. Save
stores one row. The replica that took the save plans with it at once, and
every other replica reads it at the start of its next pass.

| Setting | Default | Bounds |
|---|---|---|
| Pushover repeats every | 60 s | 30 to 10800 s |
| Pushover stops after | 180 min | 1 to 180 min |
| Sound (Ring until stopped) | the phone's default | one of Pushover's 23 built-in sounds |
| Ring once priority | Normal | Normal: one sound, follows the phone's settings. High: one sound, even during Pushover's quiet hours |
| Ring once sound | the phone's default | one of Pushover's 23 built-in sounds |
| Events with no mark and no type set here | Off | Off or Ring once |
| Default lead | 10 min | 0 to 1440 min |
| Check calendar every | 5 min | 1 to 60 min |
| Look ahead | 48 h | 1 to 336 h |
| Alert for all-day events | off | |
| Time zone | blank, UTC | a time zone database name |
| Your addresses | none | up to 10 |
| Pushover backup after | 0 s | 0 to 900 s. A Ring until stopped message waits this long, so a paired phone rings first. It is not sent if the alert is acknowledged by then, and never later than 1 minute before the start |
| Alarm sound (On the phone) | iPhone default | iPhone default, Pulse, Chime, Rise, Siren or Beacon |
| Snooze (On the phone) | 9 min | 1 to 30 min |

Ring until stopped has no priority setting. Every such alert goes to Pushover at its
priority 2 with `retry` set to the repeat and `expire` set to the stop, so
it rings until acknowledged. Pushover stops such a message after 50 sounds,
so the stop is the smaller of the limit and 50 × the repeat. At 60 s that is
50 min. The page shows the arithmetic.

Pushover refuses a repeat under 30 s (pushover.net/api#priority). A paired
phone rings as an iPhone alarm until Stop is pressed. Ring in this browser
beeps every second until the alert is acknowledged. Nonstop sets 30 s and `persistent`,
one of the five sounds pushover.net/api#sounds marks long (alien, climb,
persistent, echo, updown). iOS plays a notification sound for up to 30 s
(developer.apple.com/documentation/usernotifications/unnotificationsound),
so a long sound plays into each 30 s gap. At 30 s Pushover's 50 sounds last
25 min. Until the first save the defaults come from the
`Alerts__` settings of the same names (`AlertsOptions.cs`).

The paired phone plays the alarm sound for every alarm it rings. The
sounds are audio files in the app, and the page only names them. Ring in
this browser keeps its own tone. Snooze delays a calendar alarm on the
phone by that many minutes. A snooze is not an acknowledgement, and
nothing on the server changes for it.

`GET /api/alerts/status` carries them as `settings.phoneSound` and
`settings.phoneSnoozeMinutes`. `bounds.phoneSounds` lists
`[{value, label}]`: `default` (iPhone default), `pulse`, `chime`, `rise`,
`siren` and `beacon`. `bounds.phoneSnoozeMinutes` is `{min: 1, max: 30}`.

| Route | Who | Request | Answer |
|---|---|---|---|
| `PUT /api/alerts/settings` | the owner | the whole form, `phoneSound` and `phoneSnoozeMinutes` included | 200 with the page's whole state. 400 by field. 403 to a paired phone |
| `PUT /api/alerts/phone-settings` | the owner or a paired phone | `{sound, snoozeMinutes}`, both required | 200 with the page's whole state. 400 by field. Every other setting is left as saved |

Both share the actions rate limit. A save that changes the sound or the
snooze pushes the phones once. The same values again change nothing and
push nothing. Until the first save the two read `default` and 9.

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
