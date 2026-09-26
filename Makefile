# Entry points for working on this repository. Every target runs inside a
# container: none of them needs node, npm or dotnet installed on the machine,
# only Docker.
#
# Run `make` on its own to list them.

COMPOSE    ?= docker compose
DOCKER     ?= docker
DOCKERFILE := aberaTech.Server/Dockerfile

# Several sessions work on this repository at once, each in its own worktree,
# and every compose service here publishes a host port. Two copies on one port
# is a bind failure at best; at worst the second fails to start and its owner
# browses the first one's build believing it to be theirs.
#
# scripts/worktree-env.sh writes this copy's ports to .env, which compose reads
# on every invocation — so `docker compose` by hand is isolated too, not only
# what goes through here. Generated at parse time so the include below has
# something to read; it leaves an existing .env alone, so an override typed by
# hand survives.
#
#     make ports        what this copy uses
#     APP_PORT=9001 make up   one run on a different port
#     rm .env           derive them again
#
WORKTREE     := $(notdir $(CURDIR))
$(shell ./scripts/worktree-env.sh)
-include .env
APP_PORT     ?= 8080
DEV_PORT     ?= 3000
DB_PORT      ?= 5433
export APP_PORT
export DEV_PORT
export DB_PORT

# Whatever a container writes into the bind-mounted tree is owned by the user
# inside it. Running as root leaves bin/, obj/ and coverage/ owned by root, so
# removing the worktree after a merge needs sudo and `repos-clean` cannot do
# it at all. These are passed to compose, which hands them to every service
# that mounts this directory.
HOST_UID := $(shell id -u)
HOST_GID := $(shell id -g)
export HOST_UID
export HOST_GID

# The local image is tagged per worktree for the same reason: one shared
# `abera-tech` tag is how a reader ends up running whichever session built
# last, believing it to be theirs.
IMAGE        := abera-tech:$(shell printf '%s' '$(notdir $(CURDIR))' | tr 'A-Z' 'a-z')

.DEFAULT_GOAL := help
.PHONY: help ports up dev db queue-open queue-close test test-watch servertest dbtest lint lint-ci fmt budget prose e2e check image run clean

help: ## List the available targets
	@grep -hE '^[a-z0-9-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk -F':.*?## ' '{printf "  \033[36m%-11s\033[0m %s\n", $$1, $$2}'

ports: ## Which compose project and host ports this worktree uses
	@printf 'worktree   %s\n' '$(WORKTREE)'
	@printf 'project    %s\n' "$$($(COMPOSE) config --format json | sed -n 's/.*"name": *"\([^"]*\)".*/\1/p' | head -1)"
	@printf 'site       http://localhost:%s   (make up)\n' '$(APP_PORT)'
	@printf 'dev server http://localhost:%s   (make dev)\n' '$(DEV_PORT)'
	@printf 'database   127.0.0.1:%s\n' '$(DB_PORT)'
	@printf 'image      %s\n' '$(IMAGE)'

up: ## The whole site and its database; `make ports` says where
	@printf 'this worktree serves on http://localhost:%s\n' '$(APP_PORT)'
	$(COMPOSE) up --build app

dev: ## Hot reloading dev server; `make ports` says where
	@printf 'this worktree serves on http://localhost:%s\n' '$(DEV_PORT)'
	$(COMPOSE) up --build dev

db: ## Postgres for the scheduling feature; `make ports` says where
	$(COMPOSE) up -d db

queue-open: ## Open a queue on the local site, so /schedule shows the queue
	@$(COMPOSE) exec -T db psql -U scheduling -d scheduling -c \
	  "INSERT INTO \"QueueSessions\" (\"Id\",\"Name\",\"OpensAt\",\"ClosesAt\",\"DefaultDuration\",\"Open\") \
	   SELECT gen_random_uuid(), 'Local test queue', now(), now() + interval '8 hours', interval '15 minutes', true \
	   WHERE NOT EXISTS (SELECT 1 FROM \"QueueSessions\" WHERE \"Open\");"
	@printf 'Queue open. Reload http://localhost:%s/schedule\n' '$(APP_PORT)'

queue-close: ## Close it again, so /schedule goes back to showing slots
	@$(COMPOSE) exec -T db psql -U scheduling -d scheduling -c \
	  "UPDATE \"QueueSessions\" SET \"Open\" = false WHERE \"Open\";"
	@printf 'Queue closed. Reload http://localhost:%s/schedule\n' '$(APP_PORT)'

test: ## Client unit tests, against the working tree
	$(COMPOSE) build test
	$(COMPOSE) run --rm test

test-watch: ## Unit tests, re-run on every change
	$(COMPOSE) build test
	$(COMPOSE) run --rm test npm run test:watch

servertest: ## Server tests, against the working tree and the compose Postgres
	$(COMPOSE) build servertest
	$(COMPOSE) run --rm servertest

dbtest: ## Only the server tests that need Postgres; CI runs this too
	./scripts/server-db-tests.sh

lint: ## biome lint and format check, against the working tree
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint

# actionlint and the shellcheck it bundles come from the `actionlint` stage of
# the Dockerfile, so the version lives in one FROM line Dependabot bumps.
LINT_IMAGE := $(shell sed -n 's|^FROM \(rhysd/actionlint:[^ ]*\) AS actionlint$$|\1|p' $(DOCKERFILE))

lint-ci: ## actionlint on the workflows and their run: blocks, shellcheck on scripts, workflow concurrency
	@test -n "$(LINT_IMAGE)" || { echo "error: no 'FROM rhysd/actionlint:... AS actionlint' stage in $(DOCKERFILE)" >&2; exit 1; }
	$(DOCKER) run --rm --user $(HOST_UID):$(HOST_GID) -v $(CURDIR):/repo:ro -w /repo --entrypoint actionlint $(LINT_IMAGE) -color
	$(DOCKER) run --rm --user $(HOST_UID):$(HOST_GID) -v $(CURDIR):/repo:ro -w /repo --entrypoint shellcheck $(LINT_IMAGE) scripts/*.sh
	./scripts/check-concurrency.sh --self-test
	./scripts/check-concurrency.sh

fmt: ## Rewrite files to match biome
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint npx biome check --write .

budget: ## Page weight: the production client build against scripts/page-budgets.json
	$(DOCKER) build --target clientbudget -f $(DOCKERFILE) .

prose: ## Writing rules (.vale/styles/Abera): the Markdown, then the prerendered pages
	./scripts/check-prose.sh --self-test
	./scripts/check-prose.sh
	$(DOCKER) build --target clientprose -f $(DOCKERFILE) .

# The Playwright image is the FROM line of e2e/Dockerfile, pinned by tag and
# digest where Dependabot can bump it. The browsers in the image and the
# runner in e2e/package.json have to be the same version: Dependabot bumps
# both in one pull request, and scripts/check-playwright-image.sh fails first
# when they differ.
PLAYWRIGHT_IMAGE := $(shell sed -n 's/^FROM \([^ ]*\) AS e2e$$/\1/p' e2e/Dockerfile)
E2E_CONTAINER    := $(subst :,-,$(IMAGE))-e2e

# The suite runs on this worktree's compose network against the `app` service
# by its app-under-test alias (compose.yaml says why not `app`), so nothing
# leaves the box and two worktrees never test each other's build. Named
# container rather than --rm so the traces can be copied out when it fails;
# it is removed either way.
e2e: ## Playwright against the production image and its database, on the compose network
	./scripts/check-playwright-image.sh --self-test
	./scripts/check-playwright-image.sh
	$(COMPOSE) up -d --build --wait app
	$(DOCKER) rm -f $(E2E_CONTAINER) > /dev/null 2>&1 || true
	$(DOCKER) run --name $(E2E_CONTAINER) \
	  --network "$$($(COMPOSE) config --format json | sed -n 's/.*"name": *"\([^"]*\)".*/\1/p' | head -1)_default" \
	  -v $(CURDIR)/e2e:/src:ro -v $(subst :,-,$(IMAGE))-npm:/npm-cache \
	  -e npm_config_cache=/npm-cache -e E2E_BASE_URL=http://app-under-test:8080 \
	  -e CI="$${CI:-}" -e GITHUB_ACTIONS="$${GITHUB_ACTIONS:-}" -e GITHUB_WORKSPACE=/w \
	  $(PLAYWRIGHT_IMAGE) bash -c 'set -e; mkdir -p /w && cp -r /src /w/e2e && cd /w/e2e; \
	    for _ in $$(seq 1 60); do curl -sf "$$E2E_BASE_URL/healthz" > /dev/null && break; sleep 2; done; \
	    curl -sf "$$E2E_BASE_URL/healthz" > /dev/null; \
	    npm ci --no-audit --no-fund && npx playwright test'; \
	status=$$?; \
	if [ $$status -ne 0 ]; then \
	  rm -rf e2e-test-results; \
	  $(DOCKER) cp $(E2E_CONTAINER):/w/e2e/test-results e2e-test-results > /dev/null 2>&1 \
	    && echo 'playwright traces copied to e2e-test-results/'; \
	  echo '--- app logs (last 40 lines):'; $(COMPOSE) logs --no-log-prefix app 2>&1 | tail -40; \
	fi; \
	$(DOCKER) rm -f $(E2E_CONTAINER) > /dev/null 2>&1; \
	exit $$status

check: ## The gate CI runs: type check, unit tests, coverage, lint, format, page weight, prose, database and browser suites
	./scripts/check-required-contexts.sh --self-test
	./scripts/check-required-contexts.sh
	$(MAKE) lint-ci
	$(DOCKER) build --target clienttest -f $(DOCKERFILE) .
	$(DOCKER) build --target clientlint -f $(DOCKERFILE) .
	$(DOCKER) build --target servertest -f $(DOCKERFILE) .
	$(DOCKER) build --target clientbudget -f $(DOCKERFILE) .
	$(DOCKER) build --target clientprose -f $(DOCKERFILE) .
	./scripts/check-prose.sh --self-test
	./scripts/check-prose.sh
	./scripts/check-held-majors.sh --self-test
	./scripts/check-held-majors.sh
	./scripts/check-dotnet-major.sh --self-test
	./scripts/check-lts-majors.sh --self-test
	./scripts/check-lts-majors.sh
	./scripts/check-template-parity.sh --self-test
	./scripts/check-template-parity.sh
	./scripts/server-db-tests.sh
	$(MAKE) e2e

image: ## Build the production image the deploy pipeline builds
	$(DOCKER) build --build-arg IN_DOCKER=true -t $(IMAGE) -f $(DOCKERFILE) .

run: image ## Build and run the production image; `make ports` says where
	$(DOCKER) run --rm -p 127.0.0.1:$(APP_PORT):8080 --name $(subst :,-,$(IMAGE)) $(IMAGE)

clean: ## Remove THIS worktree's containers and volumes, including its database
	$(COMPOSE) down --volumes --remove-orphans
