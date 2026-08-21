# aberaTech

## Development

Everything runs in a container. Docker is the only thing that needs to be
installed on your machine: there is no local node, npm or dotnet toolchain to
set up, and no version of them to keep in step with the server.

```
make            # list every target
make dev        # hot reloading dev server on http://localhost:3000
make test       # unit tests, against your working tree
make lint       # eslint and prettier, against your working tree
make fmt        # rewrite files to match prettier
make check      # the gate CI runs: type check, unit tests, lint and format
make run        # build and run the production image on http://localhost:8080
make clean      # remove the compose containers and their volumes
```

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
