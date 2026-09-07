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

# The local image is tagged per worktree for the same reason: one shared
# `abera-tech` tag is how a reader ends up running whichever session built
# last, believing it to be theirs.
IMAGE        := abera-tech:$(shell printf '%s' '$(notdir $(CURDIR))' | tr 'A-Z' 'a-z')

.DEFAULT_GOAL := help
.PHONY: help ports up dev db queue-open queue-close test test-watch servertest lint fmt check image run clean

help: ## List the available targets
	@grep -hE '^[a-z-]+:.*?## ' $(MAKEFILE_LIST) \
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

servertest: ## Scheduling unit tests, against the working tree
	$(COMPOSE) build servertest
	$(COMPOSE) run --rm servertest

lint: ## biome lint and format check, against the working tree
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint

fmt: ## Rewrite files to match biome
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint npx biome check --write .

check: ## The gate CI runs: type check, unit tests, coverage, lint and format
	./scripts/check-required-contexts.sh
	$(DOCKER) build --target clienttest -f $(DOCKERFILE) .
	$(DOCKER) build --target clientlint -f $(DOCKERFILE) .
	$(DOCKER) build --target servertest -f $(DOCKERFILE) .

image: ## Build the production image the deploy pipeline builds
	$(DOCKER) build --build-arg IN_DOCKER=true -t $(IMAGE) -f $(DOCKERFILE) .

run: image ## Build and run the production image; `make ports` says where
	$(DOCKER) run --rm -p 127.0.0.1:$(APP_PORT):8080 --name $(subst :,-,$(IMAGE)) $(IMAGE)

clean: ## Remove THIS worktree's containers and volumes, including its database
	$(COMPOSE) down --volumes --remove-orphans
