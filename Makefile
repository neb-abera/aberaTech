# Entry points for working on this repository. Every target runs inside a
# container: none of them needs node, npm or dotnet installed on the machine,
# only Docker.
#
# Run `make` on its own to list them.

COMPOSE    ?= docker compose
DOCKER     ?= docker
DOCKERFILE := aberaTech.Server/Dockerfile

.DEFAULT_GOAL := help
.PHONY: help up dev db test test-watch servertest lint fmt check image run clean

help: ## List the available targets
	@grep -hE '^[a-z-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk -F':.*?## ' '{printf "  \033[36m%-11s\033[0m %s\n", $$1, $$2}'

up: ## The whole site and its database on http://localhost:8080
	$(COMPOSE) up --build app

dev: ## Hot reloading dev server on http://localhost:3000
	$(COMPOSE) up --build dev

db: ## Postgres for the scheduling feature, on 127.0.0.1:5433
	$(COMPOSE) up -d db

test: ## Client unit tests, against the working tree
	$(COMPOSE) build test
	$(COMPOSE) run --rm test

test-watch: ## Unit tests, re-run on every change
	$(COMPOSE) build test
	$(COMPOSE) run --rm test npm run test:watch

servertest: ## Scheduling unit tests, against the working tree
	$(COMPOSE) build servertest
	$(COMPOSE) run --rm servertest

lint: ## eslint and prettier, against the working tree
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint

fmt: ## Rewrite files to match prettier
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint npx prettier --write .

check: ## The gate CI runs: type check, unit tests, lint and format
	$(DOCKER) build --target clienttest -f $(DOCKERFILE) .
	$(DOCKER) build --target clientlint -f $(DOCKERFILE) .
	$(DOCKER) build --target servertest -f $(DOCKERFILE) .

image: ## Build the production image the deploy pipeline builds
	$(DOCKER) build --build-arg IN_DOCKER=true -t abera-tech -f $(DOCKERFILE) .

run: image ## Build and run the production image on http://localhost:8080
	$(DOCKER) run --rm -p 8080:8080 abera-tech

clean: ## Remove the compose containers and their volumes, including the database
	$(COMPOSE) down --volumes --remove-orphans
