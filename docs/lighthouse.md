# Lighthouse

Lighthouse runs on every pull request against the production image, and
every night against https://abera.tech. Every run is stored on a
Lighthouse CI server on Azure.

Server: https://abera-lhci.purpleocean-f7e5c55d.eastus.azurecontainerapps.io

- [aberaTech CI](https://abera-lhci.purpleocean-f7e5c55d.eastus.azurecontainerapps.io/app/projects/aberatech-ci):
  pull requests and master.
- [abera.tech production](https://abera-lhci.purpleocean-f7e5c55d.eastus.azurecontainerapps.io/app/projects/abera.tech-production):
  the nightly runs.

The server asks for a username and password. The username is `lhci`. The
password is a container app secret. On the dev box as neb:

```
az containerapp secret show -g aberatechserver-app-202412211749ResourceGroup \
  -n abera-lhci --secret-name basic-auth-password --query value -o tsv
```

## What runs

| Where | Trigger | Site | Uploads to |
|---|---|---|---|
| `Lighthouse, in the container`, a job in Checks | every pull request, every push to master | the production image on the compose network, as `make e2e` runs it | aberaTech CI, branch = the pull request's branch or `master` |
| Lighthouse nightly | 05:23 UTC, and on demand | https://abera.tech | abera.tech production, branch `production` |

`make lighthouse` and `make lighthouse-live` run the same on the dev box.
Without `LHCI_TOKEN` they upload nothing and print the tables to
`lighthouse-results/summary.md`.

Each run collects 5 runs of `/`, `/guides`, `/planner`, `/schedule` and
`/transition` (`tools/lighthouse/routes.json`) on Lighthouse's mobile
device, twice: under DevTools throttling and under simulated throttling.
Chromium is the one in the Playwright image `e2e/Dockerfile` pins.

In a pull request the site is plain HTTP, so Chrome is told to treat that
one origin as secure. Otherwise it ignores Cross-Origin-Opener-Policy and
logs a console error that abera.tech over HTTPS never shows.

## The gate

`tools/lighthouse/gate.mjs` fails the job on findings that do not depend on
timing, in every run:

- a request in `render-blocking-insight`
- a chain longer than 3 requests in `network-dependency-tree-insight`: the
  document, what it names, and what those fetch
- cumulative layout shift of 0.01 or more
- an item in `unsized-images` or `image-delivery-insight`
- an item in `errors-in-console`, except the entries in
  `tools/lighthouse/allowlist.json`, each with its reason. An entry that
  matches nothing fails, so it goes when its cause goes.
- a Content Security Policy issue in `inspector-issues`
- the median bytes a route transfers above its `lighthouse:<route>` budget
  in `scripts/page-budgets.json`, or a budget more than 10% above them. That
  is the rule `scripts/check-page-budgets.mjs` applies to the build. A
  budget is set 5% above the measurement, since response headers move a
  network count by a few bytes between runs. The nightly run checks only
  the upper bound, since Cloudflare compresses differently.

A report with an error, or missing one of these audits, fails too.
Cloudflare's email obfuscation script and its Web Analytics beacon are left
out of every check. Neb keeps both on. `gate.selftest.mjs` plants each
defect and runs before the gate every time.

Timings are never gated. A shared runner makes them noise.

## The timings

The job summary, and one comment on the pull request that each push edits,
carry two tables. Each has FCP, LCP, TBT, CLS, Speed Index and the
performance score per route, and the change against the newest master
build on the server. The nightly summary compares with the night before.

- DevTools throttling, the median of each metric over 5 runs. These lead.
  Since #262 each page modulepreloads its route chunks. Lighthouse's
  simulation counts those as blocking paint and reads FCP 600 to 1,800 ms
  worse than Chrome paints it. Under DevTools throttling Chrome painted `/`
  in 728 ms against 1,310 ms simulated.
- Simulated throttling, the median run. This is how PageSpeed Insights
  scores the site, so the score stays comparable with it.

## What the server stores

A pull request sends each route's median run under each method: 10
reports. Master and the nightly run send all 5 DevTools runs, since the
next comparison needs their medians, and the simulated median run: 30
reports. A report is about 315 KB, after dropping the screenshot
thumbnails and the full page screenshot. Pull request and master builds
are deleted after 14 days, production builds after 90
(`tools/lhci-server/server.mjs`).

## Package overrides

- `tools/lighthouse` pins Lighthouse 13 for `@lhci/cli` 0.15.1, which asks
  for 12. The gate reads Lighthouse 13 audits (the insight audits).
- `tools/lhci-server` does the same for `@lhci/server`. Lighthouse 12
  brought `extract-zip` 2.0.1, which has two high advisories and no fix
  (GHSA-jmr9-qjv8-65gv, GHSA-7pqw-9j4j-h8q3). Lighthouse 13 does not use it.
- `tools/lighthouse` pins `tmp` to 0.2.7 over the 0.0.33 that `@lhci/cli`
  asks for, past two high advisories (GHSA-ph9p-34f9-6g65,
  GHSA-7c78-jf6q-g5cm).

Dependabot bumps `lighthouse` as a direct dependency. It does not bump an
override. Check `tmp` when `@lhci/cli` moves.

## The server

- Image: `tools/lhci-server/Dockerfile`, `@lhci/server` on the Node 26
  slim image, as the `node` user. The published `patrickhulce/lhci-server`
  image runs as root on Node 18.
- Container app `abera-lhci` in the environment the site runs in:
  0.25 vCPU, 0.5 GiB, 0 to 1 replicas, scale in after 30 idle seconds. It
  pulls from the site's registry as the identity `abera-lhci` (AcrPull).
- Storage: SQLite on the Azure Files share `lhci` in the storage account
  `aberatechlhci`, mounted at `/data` with `nobrl` so SQLite's locks work
  over SMB. SMB 3.1.1 with AES-GCM only.
- Deploys: the lighthouse-server workflow, on a merge that changes
  `tools/lhci-server`. The Lighthouse job builds and starts the image on
  every pull request first.
- Everything above: `scripts/provision-lhci-server.sh`, safe to run again.

SQLite on the share rather than the shared Postgres server: Lighthouse CI
can only reach Postgres with a stored password, and database access here is
passwordless. The share needs no password the app holds. The platform
mounts it with the storage account key.

## Backup and restore

The nightly workflow's backup job takes one snapshot of the share and
deletes the one before it. It signs in as `abera-lhci-backup`, whose only
role is `LHCI share snapshots` on the storage account. Share soft delete
keeps a deleted share, with its snapshots, 7 days.

To restore, on the dev box as neb: find the snapshot, download the file, check it,
and upload it over the live one while the app is scaled to zero.

```
rg=aberatechserver-app-202412211749ResourceGroup
key=$(az storage account keys list -g $rg -n aberatechlhci --query '[0].value' -o tsv)
snap=$(az storage share-rm list -g $rg --storage-account aberatechlhci --include-snapshot \
  --query "[?snapshotTime != null].snapshotTime" -o tsv)
az storage file download --account-name aberatechlhci --account-key "$key" --share-name lhci \
  --snapshot "$snap" -p lhci.db --dest lhci.db
python3 -c "import sqlite3; c = sqlite3.connect('lhci.db'); print(c.execute('pragma integrity_check').fetchone(), c.execute('select count(*) from builds').fetchone())"
az containerapp update -g $rg -n abera-lhci --max-replicas 0
az storage file upload --account-name aberatechlhci --account-key "$key" --share-name lhci \
  --source lhci.db -p lhci.db
az containerapp update -g $rg -n abera-lhci --max-replicas 1
```

## Cost

Neb approved the server at an estimate of about $0 a month. Checked on
2026-09-28 against the invoices and the price list, the bound is $0.89 a
month and the likely figure about half that.

The Container Apps free grant is 180,000 vCPU seconds and 360,000 GiB
seconds a month for the subscription. The site and Facewoof each keep one
replica of 0.5 vCPU running, 1,296,000 vCPU seconds a month each, so the
grant is spent before this app starts. August 2026 billed the site 1,163,158
vCPU seconds and 2,326,493 GiB seconds, at the list rates: $0.000024 a vCPU
second active, $0.000003 idle, $0.000003 a GiB second.

| Item | Arithmetic | A month |
|---|---|---|
| Replica time | 610 wakes × 110 s × (0.25 vCPU × $0.000024 + 0.5 GiB × $0.000003) | $0.50 |
| File operations | 610 × 250 × $0.015 per 10,000 | $0.23 |
| Stored reports | (0.55 + 0.92 + 0.88) GB × $0.06 | $0.14 |
| Requests | 610 × 60 × $0.40 per million | $0.01 |
| Snapshot | under 0.1 GB changed a day × $0.06 | $0.01 |
| Image | 80 MB inside the registry's included 10 GB | $0 |
| Total | | $0.89 |

- 610 wakes: 371 pull request runs and 208 master runs of Checks in the 31
  days to 2026-09-28, and 31 nights, each counted as its own wake.
- 110 s a wake: a 7 s cold start (measured), about 45 s of uploads and
  reads, 30 s of cooldown and up to 30 s until the scaler polls. All of it
  counted at the active rate. The cooldown is billed at the idle rate when
  the replica is idle.
- 250 file operations a wake: two builds of 30 reports took 227 (measured
  on 2026-09-28 from the share's Transactions metric), each priced as a
  write.
- Stored reports: pull requests 371 × 14/30 × 10 × 315 KB, master 208 ×
  14/30 × 30 × 315 KB, production 31 × 3 × 30 × 315 KB.

## Follow-ups

- A custom domain such as `lighthouse.abera.tech` needs a Cloudflare API
  token on the dev box. None exists. The server is reached at its Azure
  hostname until then.
- The Lighthouse CI GitHub App would add a status per URL. It is optional,
  and installing it needs Neb's approval.
