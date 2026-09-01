# aberaTech

Visit https://abera.tech for a demonstration

## How it stays fast

The public pages — home, the index pages, the guides — are rendered to real
HTML at build time (`aberatech.client/tools/prerender.mjs` +
`src/entry-server.tsx`) and hydrated in the browser, so first paint does not
wait for the React bundle; the client-rendered app pages fall back to the
empty `spa.html` shell. The server (`StaticAssetCaching.cs`) marks hashed
`/assets` immutable for a year and every `.html` no-cache, and Cloudflare
edge-caches the HTML under a dashboard cache rule that is safe only because
the deploy workflow's `purge-edge-cache` job purges the zone on every merge
to master. Routing is explicit and placed after the static-file middleware
in `Program.cs` — left implicit, the SPA fallback swallows every
extensionless request. The CSP is a response header whose inline-script
hashes are computed from the shipped HTML at startup (`CspInlineScripts.cs`);
Cloudflare's RUM beacon is allowlisted deliberately, and its real-user Core
Web Vitals are the measurement of record.

## How it stays current

Every GitHub Action is pinned by commit SHA with a version comment, and
Dependabot bumps SHA and comment together — minor/patch grouped into one
weekly PR per ecosystem. The `dependabot-automerge` workflow arms auto-merge
on every Dependabot PR, majors included: red CI, not update size, is the
review signal. It needs the repo's Allow auto-merge setting and a
`DEPENDABOT_AUTOMERGE_TOKEN` secret (fine-grained PAT — a PAT so the merge
still triggers the deploy, which `GITHUB_TOKEN` merges do not; expires and
gets recreated quarterly). Scorecard, CodeQL and a Trivy image scan run as
scheduled gates.

## Development

Everything runs in a container. Docker is the only thing that needs to be
installed on your machine: there is no local node, npm or dotnet toolchain to
set up, and no version of them to keep in step with the server.

```
make            # list every target
make ports      # which compose project and host ports this copy uses
make up         # the whole site and its database
make queue-open # switch /schedule into queue mode
make queue-close# switch it back to showing bookable slots
make dev        # hot reloading dev server
make test       # unit tests, against your working tree
make lint       # eslint and prettier, against your working tree
make fmt        # rewrite files to match prettier
make check      # the gate CI runs: type check, unit tests, lint and format
make run        # build and run the production image
make clean      # remove this copy's containers and volumes
```

`make ports` first, because the answer is not the same in every copy of this
repository. Several sessions work here at once, each in its own git worktree,
and every published port and container name is derived from the directory: one
copy on `http://localhost:8080`, the next on `8157`, each with its own compose
project and its own database. `make clean` takes down the copy you are standing
in and nothing else. The main checkout keeps the numbers this file used to
quote. Override any of them for one run — `APP_PORT=9001 make up` — or edit the
`.env` the first `make` writes.

`make dev`, `make test` and `make lint` bind mount the working tree, so they see
uncommitted edits and give a fast loop.

`make check` deliberately does not. It builds the `clienttest` and `clientlint`
stages of `aberaTech.Server/Dockerfile`, which copy the tree into the image, so
it measures what a reviewer would actually get. **Run it before you push**: CI
builds those same two stages, on the same node image, so a green `make check`
and a green CI run mean the same thing.

After changing a dependency, run `make clean` before `make dev`. The dev service
keeps `node_modules` in an anonymous volume that outlives a rebuild otherwise.

### Stages in the Dockerfile

| Stage | What it is |
|---|---|
| `clientbase` | the client dependency tree, installed with `npm ci` |
| `clientbuild` | the production client bundle, copied into the final image |
| `clientdev` | the vite dev server, source bind mounted at run time |
| `clienttest` | `tsc -b` and the unit tests. A leaf; the production build never pays for it |
| `clienttools` | eslint, typescript-eslint and prettier from the root package |
| `clientlint` | `eslint .` and `prettier --check .` over the whole repository. Also a leaf |
| `build`, `publish`, `final` | the .NET server and the deployed image |

`clientbase`, `clientdev`, `clienttest` and `clientlint` all resolve from the
same `NODE_IMAGE` build argument, so the tests cannot pass on a different node
than the one that builds the artifact. Override it to try another version:

```
docker build --build-arg NODE_IMAGE=node:24 --target clienttest -f aberaTech.Server/Dockerfile .
```

### JetBrains

The `make` targets work as they are from the IDE terminal, and Docker Desktop
must be running for any of them.

For the IDE to resolve imports, index dependencies and run tests from the
gutter, point it at the container rather than at a local install:

1. **Settings → Build, Execution, Deployment → Docker**, add a Docker connection
   for Docker Desktop.
2. **Settings → Languages & Frameworks → Node.js**, set the Node interpreter to
   **Add → Docker Compose**, choosing `compose.yaml` and the `test` service.
   Imports and `node_modules` then resolve from the container.
3. **Run → Edit Configurations → Add → Docker → Docker Compose**, with
   `compose.yaml` and service `dev`, for a one-click dev server.

`.idea/` is only partly gitignored, so a run configuration you want to share can
be committed; anything machine specific stays out on its own.

### Without make

```
docker compose up --build dev
docker compose run --rm test
docker build --target clienttest -f aberaTech.Server/Dockerfile .
docker build --target clientlint -f aberaTech.Server/Dockerfile .
```
