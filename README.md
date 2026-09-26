[![Checks](https://github.com/neb-abera/aberaTech/actions/workflows/client-checks.yml/badge.svg?branch=master)](https://github.com/neb-abera/aberaTech/actions/workflows/client-checks.yml)
[![CodeQL](https://github.com/neb-abera/aberaTech/actions/workflows/codeql.yml/badge.svg?branch=master)](https://github.com/neb-abera/aberaTech/security/code-scanning)
[![codecov](https://codecov.io/gh/neb-abera/aberaTech/graph/badge.svg)](https://codecov.io/gh/neb-abera/aberaTech)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/neb-abera/aberaTech/badge)](https://scorecard.dev/viewer/?uri=github.com/neb-abera/aberaTech)

# aberaTech

The source of [abera.tech](https://abera.tech): a .NET 10 server, a React
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
  shipped HTML at startup (`CspInlineScripts.cs`, `CspInlineStyles.cs`).
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

- `/alerts` sends one Pushover message, priority 1, before each event on
  the owner's Google Calendar (`aberaTech.Scheduling/Alerts/`). The worker
  reads the secret iCal address every 5 minutes and sends each alert at its
  own time. The alert time is the event's earliest popup reminder, or 10
  minutes before the start. All-day, cancelled and declined events are
  skipped. Text and "06:00 tomorrow" use the calendar's own zone
  (`X-WR-TIMEZONE`, then `Alerts__TimeZone`, then UTC). Mute, Skip and a
  one-send claim per occurrence are rows in the scheduling database.

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
the time of the last calendar read. Send test alert proves the keys.

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
