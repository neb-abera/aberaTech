# Contributing

Thanks for wanting to improve this project. The short version: everything
runs in Docker, everything is tested, and every change goes through a pull
request gated on CI. A merge to `master` deploys straight to the live site,
so nothing lands until the checks are green.

## Getting started

The host needs only Docker and git — the toolchain lives in containers.
Run `make` on its own to list every target.

```bash
make ports      # which compose project and host ports this copy uses
make up         # the whole site and its database
make dev        # hot reloading dev server
make test       # client unit tests, against the working tree
make servertest # server tests, against the working tree and the compose Postgres
make dbtest     # only the tests that need Postgres
make lint       # biome lint and format check
make budget     # page weight, in bytes, against scripts/page-budgets.json
make check      # the hermetic gate CI runs, built from the Dockerfile alone
```

Ports and container names are derived from the directory, so several worktrees
of this repository run side by side without colliding and `make clean` only
takes down the copy you are standing in. `make ports` says where yours is.

`make check` builds the same Dockerfile stages CI builds (`clienttest`,
`clientlint`, `servertest`, `clientbudget`), so if it is green on your
machine, CI will agree — both run the same containers. Tests marked
`[PostgresFact]` need a real database, which a `docker build` cannot reach, so
the hermetic stage skips them and `scripts/server-db-tests.sh` runs them
against the compose Postgres instead — in `make check`, and in CI on the job
that boots the compose database.

`make check` also runs `scripts/check-held-majors.sh`, which fails when a
dependency's next major cannot be taken: an npm major that cannot install
beside the rest of its manifest, or a NuGet major that ships no framework the
referencing project can consume. Dependabot only offers a bump the project can
take, so without it such a pin ages with nothing red. The fix is a manifest of
its own for an npm package or a target framework move for a NuGet one; a case
you accept goes in `.held-majors` with its reason, and the check tells you
when that entry can be dropped.

`make budget` fails when the production client build outgrows
`scripts/page-budgets.json`: the entry script and stylesheet, what the home
page fetches first, and each prerendered page, gzipped. The numbers sit about
10% above what was measured when they were set, so ordinary work does not trip
them and a heavy dependency or an inlined blob does. When growth is
deliberate, raise the number in the same pull request and say why in its
description; a new prerendered page needs a budget of its own, and the
failure message prints the size to start from.

The server tests are build-and-run gates, never timing gates: a shared machine
makes wall-clock numbers noise. The tests that time a fit print what they
measured and assert a budget only when `ABERA_ENFORCE_TIMING` is set, so
profile on a quiet machine with `ABERA_ENFORCE_TIMING=1 make servertest`.

## Making a change

* Write tests first, from the entry point a user actually hits (an HTTP
  request, a page interaction), not from internals outward. A change in
  behavior needs a test that fails without it.
* Keep pull requests small and single-purpose, and fill in the pull request
  template (`.github/PULL_REQUEST_TEMPLATE.md`).
* Dependency lock files are enforced: `npm ci` and locked-mode NuGet
  restore fail the build when a manifest and its lock file disagree, so
  commit the regenerated lock file with any dependency change.
* Nothing merges on a red check. The `master` ruleset requires every
  PR-gating job — the container test/lint/build jobs and the workflow lint
  from `Checks`, the three CodeQL `analyze` legs, the trivy container scan,
  the ZAP baseline scan and dependency review — plus signed commits, so a
  failing check is the review: fix it rather than working around it. The
  required list is mirrored in `.github/required-contexts.txt`, and
  `scripts/check-required-contexts.sh` fails CI if it drifts from the
  workflows.

## Licensing

By contributing you agree that your contributions are licensed under the
repository's [license](LICENSE.txt) (inbound = outbound). There is no CLA.

## Security issues

Do not open a public issue for a vulnerability — use the private reporting
flow described in [SECURITY.md](SECURITY.md).
