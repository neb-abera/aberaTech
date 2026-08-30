# Contributing

Thanks for wanting to improve this project. The short version: everything
runs in Docker, everything is tested, and every change goes through a pull
request gated on CI. A merge to `master` deploys straight to the live site,
so nothing lands until the checks are green.

## Getting started

The host needs only Docker and git — the toolchain lives in containers.
Run `make` on its own to list every target.

```bash
make up         # the whole site and its database on http://localhost:8080
make dev        # hot reloading dev server on http://localhost:3000
make test       # client unit tests, against the working tree
make servertest # scheduling unit tests, against the working tree
make lint       # biome lint and format check
make check      # the hermetic gate CI runs, built from the Dockerfile alone
```

`make check` builds the same Dockerfile stages CI builds (`clienttest`,
`clientlint`, `servertest`), so if it is green on your machine, CI will
agree — both run the same containers.

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
