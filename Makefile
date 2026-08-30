# Entry points for working on this repository. Every target runs inside a
# container: none of them needs node, npm or dotnet installed on the machine,
# only Docker.
#
# Run `make` on its own to list them.

COMPOSE    ?= docker compose
DOCKER     ?= docker
DOCKERFILE := aberaTech.Server/Dockerfile

.DEFAULT_GOAL := help
.PHONY: help up dev db queue-open queue-close test test-watch servertest lint fmt check image run clean

help: ## List the available targets
	@grep -hE '^[a-z-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk -F':.*?## ' '{printf "  \033[36m%-11s\033[0m %s\n", $$1, $$2}'

up: ## The whole site and its database on http://localhost:8080
	$(COMPOSE) up --build app

dev: ## Hot reloading dev server on http://localhost:3000
	$(COMPOSE) up --build dev

db: ## Postgres for the scheduling feature, on 127.0.0.1:5433
	$(COMPOSE) up -d db

queue-open: ## Open a queue on the local site, so /schedule shows the queue
	@$(COMPOSE) exec -T db psql -U scheduling -d scheduling -c \
	  "INSERT INTO \"QueueSessions\" (\"Id\",\"Name\",\"OpensAt\",\"ClosesAt\",\"DefaultDuration\",\"Open\") \
	   SELECT gen_random_uuid(), 'Local test queue', now(), now() + interval '8 hours', interval '15 minutes', true \
	   WHERE NOT EXISTS (SELECT 1 FROM \"QueueSessions\" WHERE \"Open\");"
	@echo 'Queue open. Reload http://localhost:8080/schedule'

queue-close: ## Close it again, so /schedule goes back to showing slots
	@$(COMPOSE) exec -T db psql -U scheduling -d scheduling -c \
	  "UPDATE \"QueueSessions\" SET \"Open\" = false WHERE \"Open\";"
	@echo 'Queue closed. Reload http://localhost:8080/schedule'

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
	$(DOCKER) build --build-arg IN_DOCKER=true -t abera-tech -f $(DOCKERFILE) .

run: image ## Build and run the production image on http://localhost:8080
	$(DOCKER) run --rm -p 8080:8080 abera-tech

clean: ## Remove the compose containers and their volumes, including the database
	$(COMPOSE) down --volumes --remove-orphans
